using System;
using System.Collections.Generic;

namespace DeepLearning.GameClient
{
    public enum GameClientEventType
    {
        Connected,
        Disconnected,
        RoundStarted,
        RoundResult,
        GameOver,
        PlayerJoined,
        PlayerLeft,
        StateChanged,
        Error
    }

    public sealed class GameClientSnapshot
    {
        public GameClientSnapshot(
            int playerId,
            int round,
            IReadOnlyDictionary<int, int> scores,
            int answerIndex,
            int roundWinner,
            int gameWinner,
            bool connected,
            bool detectionSent)
        {
            PlayerId = playerId;
            Round = round;
            Scores = scores;
            AnswerIndex = answerIndex;
            RoundWinner = roundWinner;
            GameWinner = gameWinner;
            Connected = connected;
            DetectionSent = detectionSent;
        }

        public int PlayerId { get; }
        public int Round { get; }
        public IReadOnlyDictionary<int, int> Scores { get; }
        public int AnswerIndex { get; }
        public int RoundWinner { get; }
        public int GameWinner { get; }
        public bool Connected { get; }
        public bool DetectionSent { get; }
    }

    public sealed class GameClientEvent
    {
        public GameClientEvent(
            GameClientEventType type,
            GameClientSnapshot snapshot,
            int subjectPlayerId = -1,
            string message = null)
        {
            Type = type;
            Snapshot = snapshot;
            SubjectPlayerId = subjectPlayerId;
            Message = message;
        }

        public GameClientEventType Type { get; }
        public GameClientSnapshot Snapshot { get; }
        public int SubjectPlayerId { get; }
        public string Message { get; }
    }

    internal sealed class ServerMessage
    {
        public string Type;
        public int PlayerId = -1;
        public int Round = -1;
        public int Winner = -1;
        public int AnswerIndex = -1;
        public bool GameFinished;
        public Dictionary<int, int> Scores;
    }
}
