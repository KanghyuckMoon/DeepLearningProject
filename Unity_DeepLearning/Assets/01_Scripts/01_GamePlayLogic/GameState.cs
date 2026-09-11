using System;
using System.Collections.Generic;
using DeepLearning.GameServer.Core;

namespace DeepLearning.GameServer.GamePlayLogic
{
    public enum DetectionStatus
    {
        Accepted,
        GameAlreadyFinished,
        UnknownPlayer,
        InvalidRound,
        RoundAlreadyFinished
    }

    public sealed class DetectionResult
    {
        public DetectionResult(DetectionStatus status, int playerId, GameStateSnapshot snapshot)
        {
            Status = status;
            PlayerId = playerId;
            Snapshot = snapshot;
        }

        public DetectionStatus Status { get; }
        public int PlayerId { get; }
        public GameStateSnapshot Snapshot { get; }
        public bool Accepted => Status == DetectionStatus.Accepted;
    }

    public sealed class GameState
    {
        private readonly object _syncRoot = new object();
        private readonly Dictionary<int, int> _scores = new Dictionary<int, int>();
        private readonly Dictionary<int, string> _nicknames = new Dictionary<int, string>();
        private readonly Random _random = new Random();
        private readonly int _winScore;
        private readonly int _itemCount;

        private int _nextPlayerId = 1;
        private int _currentRound = 1;
        private bool _roundFinished;
        private bool _gameFinished;
        private int _winnerPlayerId = -1;
        private int _answerIndex;

        public GameState(int winScore, int itemCount)
        {
            _winScore = Math.Max(1, winScore);
            _itemCount = Math.Max(1, itemCount);
            _answerIndex = SelectAnswerIndex();
        }

        public int AddPlayer()
        {
            lock (_syncRoot)
            {
                int playerId = _nextPlayerId++;
                _scores[playerId] = 0;
                _nicknames[playerId] = $"Player {playerId}";
                return playerId;
            }
        }

        public bool RemovePlayer(int playerId)
        {
            lock (_syncRoot)
            {
                _nicknames.Remove(playerId);
                return _scores.Remove(playerId);
            }
        }

        public bool SetPlayerNickname(
            int playerId,
            string requestedNickname,
            out string nickname,
            out GameStateSnapshot snapshot)
        {
            lock (_syncRoot)
            {
                if (!_scores.ContainsKey(playerId))
                {
                    nickname = string.Empty;
                    snapshot = CreateSnapshot();
                    return false;
                }

                nickname = SanitizeNickname(requestedNickname, playerId);
                _nicknames[playerId] = nickname;
                snapshot = CreateSnapshot();
                return true;
            }
        }

        public GameStateSnapshot GetSnapshot()
        {
            lock (_syncRoot)
            {
                return CreateSnapshot();
            }
        }

        public DetectionResult TryRecordDetection(int playerId, int round)
        {
            lock (_syncRoot)
            {
                DetectionStatus status;

                if (_gameFinished)
                {
                    status = DetectionStatus.GameAlreadyFinished;
                }
                else if (!_scores.ContainsKey(playerId))
                {
                    status = DetectionStatus.UnknownPlayer;
                }
                else if (round != _currentRound)
                {
                    status = DetectionStatus.InvalidRound;
                }
                else if (_roundFinished)
                {
                    status = DetectionStatus.RoundAlreadyFinished;
                }
                else
                {
                    _roundFinished = true;
                    _scores[playerId]++;

                    if (_scores[playerId] >= _winScore)
                    {
                        _gameFinished = true;
                        _winnerPlayerId = playerId;
                    }

                    status = DetectionStatus.Accepted;
                }

                return new DetectionResult(status, playerId, CreateSnapshot());
            }
        }

        public bool TryStartNextRound(int completedRound, out GameStateSnapshot snapshot)
        {
            lock (_syncRoot)
            {
                if (_gameFinished || !_roundFinished || completedRound != _currentRound)
                {
                    snapshot = CreateSnapshot();
                    return false;
                }

                _currentRound++;
                _roundFinished = false;
                _answerIndex = SelectAnswerIndex();
                snapshot = CreateSnapshot();
                return true;
            }
        }

        private int SelectAnswerIndex()
        {
            return _random.Next(0, _itemCount);
        }

        private GameStateSnapshot CreateSnapshot()
        {
            return new GameStateSnapshot(
                _currentRound,
                new Dictionary<int, int>(_scores),
                new Dictionary<int, string>(_nicknames),
                _gameFinished,
                _winnerPlayerId,
                _answerIndex);
        }

        private string SanitizeNickname(string value, int playerId)
        {
            const int maxLength = 16;
            string trimmed = string.IsNullOrWhiteSpace(value)
                ? $"Player {playerId}"
                : value.Trim();
            var characters = new List<char>(maxLength);

            foreach (char character in trimmed)
            {
                if (!char.IsControl(character))
                {
                    characters.Add(character);
                }

                if (characters.Count >= maxLength)
                {
                    break;
                }
            }

            string nickname = new string(characters.ToArray()).Trim();
            return string.IsNullOrEmpty(nickname) ? $"Player {playerId}" : nickname;
        }
    }
}
