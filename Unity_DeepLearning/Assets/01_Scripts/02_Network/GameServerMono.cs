using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeepLearning.GameData;
using DeepLearning.GameServer.Core;
using DeepLearning.GameServer.GamePlayLogic;
using TMPro;
using UnityEngine;

namespace DeepLearning.GameServer.Network
{
    public sealed class GameServerMono : MonoBehaviour
    {
        private const int MaxCameraFrameWidth = 640;
        private const int MaxCameraFrameHeight = 480;
        private const int MaxCameraPayloadCharacters = 256000;

        [SerializeField] private GameServerSettings settings = new GameServerSettings();
        [SerializeField] private DataBaseSO itemDatabase;
        [SerializeField] private bool startOnAwake = true;

        [Header("Log UI")]
        [SerializeField] private TMP_Text logText;
        [SerializeField, Min(1)] private int maxVisibleLogLines = 30;

        private readonly ConcurrentDictionary<int, ClientConnection> _clients =
            new ConcurrentDictionary<int, ClientConnection>();
        private readonly ConcurrentQueue<LogEntry> _logs = new ConcurrentQueue<LogEntry>();
        private readonly Queue<string> _visibleLogs = new Queue<string>();

        private CancellationTokenSource _shutdown;
        private TcpListener _listener;
        private Thread _acceptThread;
        private UdpClient _discoverySocket;
        private Thread _discoveryThread;
        private GameState _gameState;
        private int _isRunning;

        public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

        private void Awake()
        {
            ClearLogText();

            if (startOnAwake)
            {
                StartServer();
            }
        }

        private void OnValidate()
        {
            settings?.Validate();
        }

        private void Update()
        {
            while (_logs.TryDequeue(out LogEntry entry))
            {
                AppendVisibleLog(entry);

                if (entry.IsError)
                {
                    Debug.LogError(entry.Message, this);
                }
                else
                {
                    Debug.Log(entry.Message, this);
                }
            }
        }

        private void OnDestroy()
        {
            StopServer();
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        [ContextMenu("Start Game Server")]
        public void StartServer()
        {
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            {
                return;
            }

            settings.Validate();

            try
            {
                if (itemDatabase == null || itemDatabase.Count == 0)
                {
                    throw new InvalidOperationException("Item DataBase가 지정되지 않았거나 비어 있습니다.");
                }

                IPAddress address = ResolveBindAddress(settings.host);
                _gameState = new GameState(settings.winScore, itemDatabase.Count);
                _shutdown = new CancellationTokenSource();
                _listener = new TcpListener(address, settings.port);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Start();
                StartDiscoveryResponder();

                _acceptThread = new Thread(AcceptClients)
                {
                    IsBackground = true,
                    Name = "GameServer.Accept"
                };
                _acceptThread.Start();

                EnqueueLog(
                    $"게임 서버 시작 - IP: {settings.host}, PORT: {settings.port}, " +
                    $"검색 PORT: {settings.discoveryPort}, " +
                    $"최대 인원: {settings.maxPlayers}명, " +
                    $"승리 조건: {settings.winScore}점, 아이템: {itemDatabase.Count}개");

                GameStateSnapshot initialState = _gameState.GetSnapshot();
                EnqueueLog(
                    $"Round {initialState.Round} 찾을 아이템: " +
                    $"{GetItemName(initialState.AnswerIndex)} ({initialState.AnswerIndex})");
            }
            catch (Exception exception)
            {
                Interlocked.Exchange(ref _isRunning, 0);
                _listener?.Stop();
                _discoverySocket?.Close();
                _listener = null;
                _discoverySocket = null;
                _shutdown?.Dispose();
                _shutdown = null;
                EnqueueLog($"게임 서버 시작 실패: {exception.Message}", true);
            }
        }

        [ContextMenu("Stop Game Server")]
        public void StopServer()
        {
            if (Interlocked.Exchange(ref _isRunning, 0) == 0)
            {
                return;
            }

            _shutdown?.Cancel();
            _listener?.Stop();
            _discoverySocket?.Close();

            foreach (ClientConnection connection in _clients.Values)
            {
                connection.Dispose();
            }

            _clients.Clear();
            _listener = null;
            _discoverySocket?.Dispose();
            _discoverySocket = null;
            _shutdown = null;
            EnqueueLog("게임 서버 종료");
        }

        private void StartDiscoveryResponder()
        {
            _discoverySocket = new UdpClient(AddressFamily.InterNetwork);
            _discoverySocket.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true);
            _discoverySocket.Client.Bind(new IPEndPoint(IPAddress.Any, settings.discoveryPort));
            _discoverySocket.EnableBroadcast = true;

            _discoveryThread = new Thread(RespondToDiscoveryRequests)
            {
                IsBackground = true,
                Name = "GameServer.Discovery"
            };
            _discoveryThread.Start();
        }

        private void RespondToDiscoveryRequests()
        {
            byte[] response = Encoding.UTF8.GetBytes(
                GameServerSettings.DiscoveryResponsePrefix + settings.port);

            while (IsRunning && _shutdown != null && !_shutdown.IsCancellationRequested)
            {
                try
                {
                    var sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] request = _discoverySocket.Receive(ref sender);
                    if (Encoding.UTF8.GetString(request) == GameServerSettings.DiscoveryRequest)
                    {
                        _discoverySocket.Send(response, response.Length, sender);
                    }
                }
                catch (SocketException)
                {
                    if (!IsRunning)
                    {
                        return;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        [ContextMenu("Broadcast Current Game State")]
        public void BroadcastCurrentGameState()
        {
            if (IsRunning && _gameState != null)
            {
                Broadcast(JsonLineProtocol.GameState(_gameState.GetSnapshot()));
            }
        }

        [ContextMenu("Clear Server Log")]
        public void ClearLogText()
        {
            _visibleLogs.Clear();

            if (logText != null)
            {
                logText.text = string.Empty;
            }
        }

        private void AcceptClients()
        {
            while (IsRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    client.NoDelay = true;

                    if (_clients.Count >= settings.maxPlayers)
                    {
                        RejectFullClient(client);
                        continue;
                    }

                    int playerId = _gameState.AddPlayer();
                    var connection = new ClientConnection(playerId, client);

                    if (!_clients.TryAdd(playerId, connection))
                    {
                        connection.Dispose();
                        _gameState.RemovePlayer(playerId);
                        continue;
                    }

                    var clientThread = new Thread(() => HandleClient(connection))
                    {
                        IsBackground = true,
                        Name = $"GameServer.Player.{playerId}"
                    };
                    clientThread.Start();
                }
                catch (SocketException exception)
                {
                    if (IsRunning)
                    {
                        EnqueueLog($"클라이언트 접속 대기 오류: {exception.Message}", true);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    if (IsRunning)
                    {
                        EnqueueLog($"클라이언트 접속 처리 오류: {exception.Message}", true);
                    }
                }
            }
        }

        private void RejectFullClient(TcpClient client)
        {
            EndPoint remoteEndPoint = client.Client.RemoteEndPoint;

            try
            {
                NetworkStream stream = client.GetStream();
                var utf8 = new UTF8Encoding(false, true);

                using (var writer = new StreamWriter(stream, utf8, 4096, true))
                {
                    writer.AutoFlush = true;
                    writer.NewLine = "\n";
                    writer.WriteLine(JsonLineProtocol.ServerFull(settings.maxPlayers));
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is SocketException ||
                exception is ObjectDisposedException)
            {
                EnqueueLog($"정원 초과 응답 전송 실패 - 주소: {remoteEndPoint}, {exception.Message}", true);
            }
            finally
            {
                client.Dispose();
            }

            EnqueueLog(
                $"접속 거절(서버 정원 {settings.maxPlayers}명 초과) - 주소: {remoteEndPoint}");
        }

        private void HandleClient(ClientConnection connection)
        {
            int playerId = connection.PlayerId;
            EndPoint remoteEndPoint = connection.RemoteEndPoint;
            EnqueueLog($"Player {playerId} 접속 - 주소: {remoteEndPoint}");

            try
            {
                GameStateSnapshot state = _gameState.GetSnapshot();
                connection.Send(JsonLineProtocol.Connected(playerId, state));
                Broadcast(JsonLineProtocol.PlayerJoined(playerId, state));

                while (IsRunning && connection.TryReadLine(out string line))
                {
                    if (!JsonLineProtocol.TryParseClientMessage(line, out ClientMessage message))
                    {
                        EnqueueLog($"Player {playerId} JSON 오류: {line}", true);
                        continue;
                    }

                    HandleMessage(connection, message);
                }
            }
            catch (IOException exception)
            {
                if (IsRunning)
                {
                    EnqueueLog($"Player {playerId} 연결 종료: {exception.Message}");
                }
            }
            catch (SocketException exception)
            {
                if (IsRunning)
                {
                    EnqueueLog($"Player {playerId} 소켓 오류: {exception.Message}", true);
                }
            }
            catch (ObjectDisposedException)
            {
                // 서버 종료 또는 다른 전송 스레드에서 연결을 정리한 경우입니다.
            }
            catch (Exception exception)
            {
                if (IsRunning)
                {
                    EnqueueLog($"Player {playerId} 오류: {exception.Message}", true);
                }
            }
            finally
            {
                RemovePlayer(playerId, true);
            }
        }

        private void HandleMessage(ClientConnection connection, ClientMessage message)
        {
            switch (message.type)
            {
                case ClientMessageType.Detected:
                    HandleDetection(connection.PlayerId, message.round);
                    break;

                case ClientMessageType.Ready:
                    EnqueueLog($"Player {connection.PlayerId} Ready");
                    break;

                case ClientMessageType.Ping:
                    connection.Send(JsonLineProtocol.Pong());
                    break;

                case ClientMessageType.CameraFrame:
                    RelayCameraFrame(connection.PlayerId, message);
                    break;

                case ClientMessageType.SetNickname:
                    HandleNickname(connection.PlayerId, message.nickname);
                    break;

                default:
                    EnqueueLog($"알 수 없는 메시지 Player {connection.PlayerId}: {message.type}");
                    break;
            }
        }

        private void HandleNickname(int playerId, string requestedNickname)
        {
            if (!_gameState.SetPlayerNickname(
                    playerId,
                    requestedNickname,
                    out string nickname,
                    out GameStateSnapshot state))
            {
                return;
            }

            EnqueueLog($"Player {playerId} 닉네임 설정: {nickname}");
            Broadcast(JsonLineProtocol.PlayerUpdated(playerId, nickname, state));
        }

        private void RelayCameraFrame(int playerId, ClientMessage message)
        {
            bool invalidFrame = string.IsNullOrEmpty(message.image) ||
                                message.image.Length > MaxCameraPayloadCharacters ||
                                message.width <= 0 ||
                                message.width > MaxCameraFrameWidth ||
                                message.height <= 0 ||
                                message.height > MaxCameraFrameHeight;

            if (invalidFrame)
            {
                EnqueueLog($"Player {playerId} 카메라 프레임 거절: 잘못된 크기 또는 데이터", true);
                return;
            }

            BroadcastExcept(playerId, JsonLineProtocol.CameraFrame(playerId, message));
        }

        private void HandleDetection(int playerId, int round)
        {
            DetectionResult result = _gameState.TryRecordDetection(playerId, round);

            switch (result.Status)
            {
                case DetectionStatus.Accepted:
                    EnqueueLog($"Round {result.Snapshot.Round} 승자: Player {playerId}");
                    break;
                case DetectionStatus.InvalidRound:
                    EnqueueLog(
                        $"Player {playerId} 잘못된 라운드 요청 " +
                        $"(받음: {round}, 현재: {result.Snapshot.Round})");
                    return;
                case DetectionStatus.RoundAlreadyFinished:
                    EnqueueLog($"Player {playerId} 인식 성공했지만 늦음");
                    return;
                default:
                    return;
            }

            if (result.Snapshot.GameFinished)
            {
                EnqueueLog($"게임 최종 승자: Player {playerId}");
                Broadcast(JsonLineProtocol.GameOver(playerId, result.Snapshot));
                return;
            }

            Broadcast(JsonLineProtocol.RoundResult(playerId, result.Snapshot));
            CancellationTokenSource shutdown = _shutdown;
            if (shutdown != null)
            {
                _ = StartNextRoundAfterDelayAsync(result.Snapshot.Round, shutdown.Token);
            }
        }

        private async Task StartNextRoundAfterDelayAsync(int completedRound, CancellationToken cancellationToken)
        {
            try
            {
                int delayMilliseconds = (int)Math.Round(settings.roundResultDelaySeconds * 1000d);
                await Task.Delay(delayMilliseconds, cancellationToken);

                if (!IsRunning || !_gameState.TryStartNextRound(completedRound, out GameStateSnapshot state))
                {
                    return;
                }

                EnqueueLog($"===== Round {state.Round} 시작 =====");
                EnqueueLog(
                    $"찾을 아이템: {GetItemName(state.AnswerIndex)} " +
                    $"({state.AnswerIndex})");
                Broadcast(JsonLineProtocol.RoundStart(state));
            }
            catch (OperationCanceledException)
            {
                // 서버가 종료되었습니다.
            }
        }

        private void Broadcast(string json)
        {
            foreach (ClientConnection connection in _clients.Values)
            {
                try
                {
                    connection.Send(json);
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is SocketException ||
                    exception is ObjectDisposedException)
                {
                    HandleFailedSend(connection, exception);
                }
            }
        }

        private void BroadcastExcept(int excludedPlayerId, string json)
        {
            foreach (ClientConnection connection in _clients.Values)
            {
                if (connection.PlayerId == excludedPlayerId)
                {
                    continue;
                }

                try
                {
                    connection.Send(json);
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is SocketException ||
                    exception is ObjectDisposedException)
                {
                    HandleFailedSend(connection, exception);
                }
            }
        }

        private void HandleFailedSend(ClientConnection connection, Exception exception)
        {
            if (!_clients.ContainsKey(connection.PlayerId))
            {
                return;
            }

            EnqueueLog(
                $"Player {connection.PlayerId} 연결이 끊겨 전송 대상에서 제거합니다: {exception.Message}");
            RemovePlayer(connection.PlayerId, true);
        }

        private void RemovePlayer(int playerId, bool notifyPlayers)
        {
            if (!_clients.TryRemove(playerId, out ClientConnection connection))
            {
                return;
            }

            connection.Dispose();
            _gameState.RemovePlayer(playerId);
            EnqueueLog($"Player {playerId} 연결 종료");

            if (notifyPlayers && IsRunning)
            {
                Broadcast(JsonLineProtocol.PlayerLeft(playerId, _gameState.GetSnapshot()));
            }
        }

        private void EnqueueLog(string message, bool isError = false)
        {
            _logs.Enqueue(new LogEntry(message, isError));
        }

        private string GetItemName(int index)
        {
            return itemDatabase != null && itemDatabase.TryGetItem(index, out ItemData item)
                ? item.Name
                : "Unknown";
        }

        private void AppendVisibleLog(LogEntry entry)
        {
            string level = entry.IsError ? "[ERROR] " : string.Empty;
            _visibleLogs.Enqueue($"[{DateTime.Now:HH:mm:ss}] {level}{entry.Message}");

            int lineLimit = Math.Max(1, maxVisibleLogLines);
            while (_visibleLogs.Count > lineLimit)
            {
                _visibleLogs.Dequeue();
            }

            if (logText == null)
            {
                return;
            }

            var builder = new StringBuilder();
            foreach (string line in _visibleLogs)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(line);
            }

            logText.text = builder.ToString();
        }

        private static IPAddress ResolveBindAddress(string host)
        {
            if (host == "0.0.0.0" || host == "*")
            {
                return IPAddress.Any;
            }

            if (IPAddress.TryParse(host, out IPAddress address))
            {
                return address;
            }

            IPAddress[] addresses = Dns.GetHostAddresses(host);
            foreach (IPAddress candidate in addresses)
            {
                if (candidate.AddressFamily == AddressFamily.InterNetwork)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException($"IPv4 주소를 찾을 수 없습니다: {host}");
        }

        private readonly struct LogEntry
        {
            public LogEntry(string message, bool isError)
            {
                Message = message;
                IsError = isError;
            }

            public string Message { get; }
            public bool IsError { get; }
        }

        private sealed class ClientConnection : IDisposable
        {
            private readonly object _sendLock = new object();
            private readonly TcpClient _client;
            private readonly StreamReader _reader;
            private readonly StreamWriter _writer;
            private int _disposed;

            public ClientConnection(int playerId, TcpClient client)
            {
                PlayerId = playerId;
                _client = client;
                NetworkStream stream = client.GetStream();
                var utf8 = new UTF8Encoding(false, true);
                _reader = new StreamReader(stream, utf8, false, 4096, true);
                _writer = new StreamWriter(stream, utf8, 4096, true)
                {
                    AutoFlush = true,
                    NewLine = "\n"
                };
            }

            public int PlayerId { get; }
            public EndPoint RemoteEndPoint => _client.Client.RemoteEndPoint;

            public bool TryReadLine(out string line)
            {
                line = _reader.ReadLine();
                return line != null;
            }

            public void Send(string json)
            {
                lock (_sendLock)
                {
                    if (Volatile.Read(ref _disposed) == 1)
                    {
                        throw new ObjectDisposedException(nameof(ClientConnection));
                    }

                    _writer.WriteLine(json);
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                try
                {
                    _client.Client.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                    // 이미 연결이 끊어진 소켓입니다.
                }

                _reader.Dispose();
                _writer.Dispose();
                _client.Dispose();
            }
        }
    }
}
