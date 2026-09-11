using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DeepLearning.GameClient
{
    public sealed class GameClient : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private string serverIp = "10.10.59.205";
        [SerializeField, Min(1)] private int port = 5000;
        [SerializeField] private bool connectOnStart = true;

        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
        private readonly object _stateLock = new object();
        private readonly object _sendLock = new object();
        private readonly Dictionary<int, int> _scores = new Dictionary<int, int>();

        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;
        private Thread _receiveThread;
        private int _connectionVersion;
        private int _connected;
        private int _playerId = -1;
        private int _currentRound;
        private int _answerIndex = -1;
        private int _roundWinner = -1;
        private int _gameWinner = -1;
        private bool _detectionSent;

        public event Action<GameClientEvent> ClientEvent;

        public bool IsConnected => Volatile.Read(ref _connected) == 1;
        public GameClientSnapshot Snapshot => CreateSnapshot();

        private void Start()
        {
            if (connectOnStart)
            {
                Connect();
            }
        }

        private void Update()
        {
            while (_mainThreadActions.TryDequeue(out Action action))
            {
                action.Invoke();
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        [ContextMenu("Connect To Game Server")]
        public void Connect()
        {
            if (IsConnected || (_receiveThread != null && _receiveThread.IsAlive))
            {
                return;
            }

            int version = Interlocked.Increment(ref _connectionVersion);
            _receiveThread = new Thread(() => ConnectAndReceive(version))
            {
                IsBackground = true,
                Name = "GameClient.Receive"
            };
            _receiveThread.Start();
        }

        [ContextMenu("Disconnect From Game Server")]
        public void Disconnect()
        {
            Interlocked.Increment(ref _connectionVersion);
            bool wasConnected = Interlocked.Exchange(ref _connected, 0) == 1;
            CloseTransport();

            if (wasConnected && gameObject != null)
            {
                EnqueueEvent(GameClientEventType.Disconnected, message: "서버 연결 종료");
            }
        }

        public bool SendDetection()
        {
            int round;

            lock (_stateLock)
            {
                if (!IsConnected || _detectionSent || _currentRound <= 0 || _gameWinner >= 0)
                {
                    return false;
                }

                round = _currentRound;
                _detectionSent = true;
            }

            if (!TrySend(ClientJsonProtocol.Detected(round)))
            {
                lock (_stateLock)
                {
                    _detectionSent = false;
                }

                return false;
            }

            EnqueueEvent(GameClientEventType.StateChanged, message: $"인식 성공 전송 / Round {round}");
            return true;
        }

        public bool SendReady()
        {
            return TrySend(ClientJsonProtocol.Ready());
        }

        public bool SendPing()
        {
            return TrySend(ClientJsonProtocol.Ping());
        }

        private void ConnectAndReceive(int version)
        {
            try
            {
                var client = new TcpClient { NoDelay = true };
                client.Connect(serverIp, port);

                if (version != Volatile.Read(ref _connectionVersion))
                {
                    client.Dispose();
                    return;
                }

                NetworkStream stream = client.GetStream();
                var utf8 = new UTF8Encoding(false, true);

                lock (_sendLock)
                {
                    _client = client;
                    _reader = new StreamReader(stream, utf8, false, 4096, true);
                    _writer = new StreamWriter(stream, utf8, 4096, true)
                    {
                        AutoFlush = true,
                        NewLine = "\n"
                    };
                }

                Interlocked.Exchange(ref _connected, 1);

                while (IsConnected && version == Volatile.Read(ref _connectionVersion))
                {
                    string line = _reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    if (!ClientJsonProtocol.TryParseServerMessage(line, out ServerMessage message))
                    {
                        EnqueueEvent(GameClientEventType.Error, message: $"서버 JSON 오류: {line}");
                        continue;
                    }

                    ApplyServerMessage(message);
                }
            }
            catch (Exception exception) when (
                exception is SocketException ||
                exception is IOException ||
                exception is ObjectDisposedException)
            {
                if (version == Volatile.Read(ref _connectionVersion))
                {
                    EnqueueEvent(GameClientEventType.Error, message: $"서버 연결 오류: {exception.Message}");
                }
            }
            finally
            {
                if (version == Volatile.Read(ref _connectionVersion))
                {
                    Interlocked.Exchange(ref _connected, 0);
                    CloseTransport();
                    EnqueueEvent(GameClientEventType.Disconnected, message: "서버 연결 종료");
                }
            }
        }

        private void ApplyServerMessage(ServerMessage message)
        {
            GameClientEventType eventType;
            int subjectPlayerId = -1;

            lock (_stateLock)
            {
                if (message.Scores != null)
                {
                    _scores.Clear();
                    foreach (KeyValuePair<int, int> score in message.Scores)
                    {
                        _scores[score.Key] = score.Value;
                    }
                }

                switch (message.Type)
                {
                    case "connected":
                        _playerId = message.PlayerId;
                        SetRoundFields(message);
                        _detectionSent = false;
                        eventType = GameClientEventType.Connected;
                        subjectPlayerId = message.PlayerId;
                        break;

                    case "round_start":
                        SetRoundFields(message);
                        _roundWinner = -1;
                        _detectionSent = false;
                        eventType = GameClientEventType.RoundStarted;
                        break;

                    case "round_result":
                        SetRoundFields(message);
                        _roundWinner = message.Winner;
                        eventType = GameClientEventType.RoundResult;
                        subjectPlayerId = message.Winner;
                        break;

                    case "game_over":
                        SetRoundFields(message);
                        _gameWinner = message.Winner;
                        eventType = GameClientEventType.GameOver;
                        subjectPlayerId = message.Winner;
                        break;

                    case "player_joined":
                        eventType = GameClientEventType.PlayerJoined;
                        subjectPlayerId = message.PlayerId;
                        break;

                    case "player_left":
                        eventType = GameClientEventType.PlayerLeft;
                        subjectPlayerId = message.PlayerId;
                        break;

                    case "game_state":
                        SetRoundFields(message);
                        if (message.GameFinished)
                        {
                            _gameWinner = message.Winner;
                        }
                        eventType = GameClientEventType.StateChanged;
                        break;

                    case "pong":
                        eventType = GameClientEventType.StateChanged;
                        break;

                    default:
                        eventType = GameClientEventType.Error;
                        break;
                }
            }

            EnqueueEvent(eventType, subjectPlayerId, message.Type);
        }

        private void SetRoundFields(ServerMessage message)
        {
            if (message.Round >= 0)
            {
                _currentRound = message.Round;
            }

            if (message.AnswerIndex >= 0)
            {
                _answerIndex = message.AnswerIndex;
            }
        }

        private bool TrySend(string json)
        {
            if (!IsConnected)
            {
                return false;
            }

            try
            {
                lock (_sendLock)
                {
                    if (_writer == null)
                    {
                        return false;
                    }

                    _writer.WriteLine(json);
                    return true;
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is SocketException ||
                exception is ObjectDisposedException)
            {
                EnqueueEvent(GameClientEventType.Error, message: $"서버 전송 오류: {exception.Message}");
                return false;
            }
        }

        private void CloseTransport()
        {
            lock (_sendLock)
            {
                try
                {
                    _client?.Close();
                }
                catch
                {
                    // 이미 닫힌 소켓입니다.
                }

                _reader = null;
                _writer = null;
                _client = null;
            }
        }

        private GameClientSnapshot CreateSnapshot()
        {
            lock (_stateLock)
            {
                return new GameClientSnapshot(
                    _playerId,
                    _currentRound,
                    new Dictionary<int, int>(_scores),
                    _answerIndex,
                    _roundWinner,
                    _gameWinner,
                    IsConnected,
                    _detectionSent);
            }
        }

        private void EnqueueEvent(
            GameClientEventType type,
            int subjectPlayerId = -1,
            string message = null)
        {
            _mainThreadActions.Enqueue(() =>
            {
                var clientEvent = new GameClientEvent(type, CreateSnapshot(), subjectPlayerId, message);

                if (type == GameClientEventType.Error)
                {
                    Debug.LogError(message, this);
                }
                else if (!string.IsNullOrEmpty(message))
                {
                    Debug.Log(message, this);
                }

                ClientEvent?.Invoke(clientEvent);
            });
        }
    }
}
