using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DeepLearning.GameServer.Core;
using UnityEngine;

namespace DeepLearning.GameServer.Network
{
    public static class JsonLineProtocol
    {
        public static bool TryParseClientMessage(string json, out ClientMessage message)
        {
            message = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                message = JsonUtility.FromJson<ClientMessage>(json);
                return message != null && !string.IsNullOrWhiteSpace(message.type);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static string Connected(int playerId, GameStateSnapshot state)
        {
            return Build(
                ServerMessageType.Connected,
                ("player_id", playerId),
                ("round", state.Round),
                ("scores", state.Scores),
                ("answer_index", state.AnswerIndex));
        }

        public static string PlayerJoined(int playerId, GameStateSnapshot state)
        {
            return Build(
                ServerMessageType.PlayerJoined,
                ("player_id", playerId),
                ("scores", state.Scores));
        }

        public static string PlayerLeft(int playerId, GameStateSnapshot state)
        {
            return Build(
                ServerMessageType.PlayerLeft,
                ("player_id", playerId),
                ("scores", state.Scores));
        }

        public static string GameState(GameStateSnapshot state)
        {
            return Build(
                ServerMessageType.GameState,
                ("round", state.Round),
                ("scores", state.Scores),
                ("game_finished", state.GameFinished),
                ("winner", state.WinnerPlayerId),
                ("answer_index", state.AnswerIndex));
        }

        public static string RoundStart(GameStateSnapshot state)
        {
            return Build(
                ServerMessageType.RoundStart,
                ("round", state.Round),
                ("scores", state.Scores),
                ("answer_index", state.AnswerIndex));
        }

        public static string RoundResult(int winnerPlayerId, GameStateSnapshot state)
        {
            return Build(
                ServerMessageType.RoundResult,
                ("round", state.Round),
                ("winner", winnerPlayerId),
                ("scores", state.Scores),
                ("answer_index", state.AnswerIndex));
        }

        public static string GameOver(int winnerPlayerId, GameStateSnapshot state)
        {
            return Build(
                ServerMessageType.GameOver,
                ("winner", winnerPlayerId),
                ("round", state.Round),
                ("scores", state.Scores));
        }

        public static string Pong()
        {
            return Build(ServerMessageType.Pong);
        }

        private static string Build(string type, params (string Key, object Value)[] fields)
        {
            var builder = new StringBuilder(128);
            builder.Append("{\"type\":\"").Append(Escape(type)).Append('"');

            foreach ((string key, object value) in fields)
            {
                builder.Append(",\"").Append(Escape(key)).Append("\":");
                AppendValue(builder, value);
            }

            return builder.Append('}').ToString();
        }

        private static void AppendValue(StringBuilder builder, object value)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    break;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    break;
                case int number:
                    builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    break;
                case IReadOnlyDictionary<int, int> scores:
                    AppendScores(builder, scores);
                    break;
                default:
                    builder.Append('"').Append(Escape(value.ToString())).Append('"');
                    break;
            }
        }

        private static void AppendScores(StringBuilder builder, IReadOnlyDictionary<int, int> scores)
        {
            builder.Append('{');
            bool first = true;

            foreach (KeyValuePair<int, int> score in scores)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append('"')
                    .Append(score.Key.ToString(CultureInfo.InvariantCulture))
                    .Append("\":")
                    .Append(score.Value.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append('}');
        }

        private static string Escape(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
