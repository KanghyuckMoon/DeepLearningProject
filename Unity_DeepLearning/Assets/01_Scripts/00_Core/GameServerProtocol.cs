using System;
using System.Collections.Generic;

namespace DeepLearning.GameServer.Core
{
    public static class ClientMessageType
    {
        public const string Detected = "detected";
        public const string Ready = "ready";
        public const string Ping = "ping";
    }

    public static class ServerMessageType
    {
        public const string Connected = "connected";
        public const string PlayerJoined = "player_joined";
        public const string PlayerLeft = "player_left";
        public const string GameState = "game_state";
        public const string RoundStart = "round_start";
        public const string RoundResult = "round_result";
        public const string GameOver = "game_over";
        public const string Pong = "pong";
    }

    [Serializable]
    public sealed class ClientMessage
    {
        public string type;
        public int round;
    }

    public sealed class GameStateSnapshot
    {
        public GameStateSnapshot(
            int round,
            IReadOnlyDictionary<int, int> scores,
            bool gameFinished,
            int winnerPlayerId,
            int answerIndex)
        {
            Round = round;
            Scores = scores;
            GameFinished = gameFinished;
            WinnerPlayerId = winnerPlayerId;
            AnswerIndex = answerIndex;
        }

        public int Round { get; }
        public IReadOnlyDictionary<int, int> Scores { get; }
        public bool GameFinished { get; }
        public int WinnerPlayerId { get; }
        public int AnswerIndex { get; }
    }
}
