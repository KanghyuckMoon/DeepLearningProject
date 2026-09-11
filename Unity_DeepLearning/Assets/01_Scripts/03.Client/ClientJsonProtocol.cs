using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DeepLearning.GameClient
{
    internal static class ClientJsonProtocol
    {
        private static readonly Regex StringField = new Regex(
            "\\\"(?<key>[^\\\"]+)\\\"\\s*:\\s*\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\"",
            RegexOptions.Compiled);

        private static readonly Regex IntegerField = new Regex(
            "\\\"(?<key>[^\\\"]+)\\\"\\s*:\\s*(?<value>-?\\d+)",
            RegexOptions.Compiled);

        private static readonly Regex BooleanField = new Regex(
            "\\\"(?<key>[^\\\"]+)\\\"\\s*:\\s*(?<value>true|false)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ScoresField = new Regex(
            "\\\"scores\\\"\\s*:\\s*\\{(?<value>[^}]*)\\}",
            RegexOptions.Compiled);

        private static readonly Regex ScoreEntry = new Regex(
            "\\\"(?<player>\\d+)\\\"\\s*:\\s*(?<score>-?\\d+)",
            RegexOptions.Compiled);

        public static string Ready()
        {
            return "{\"type\":\"ready\"}";
        }

        public static string Ping()
        {
            return "{\"type\":\"ping\"}";
        }

        public static string Detected(int round)
        {
            return "{\"type\":\"detected\",\"round\":" +
                   round.ToString(CultureInfo.InvariantCulture) + "}";
        }

        public static bool TryParseServerMessage(string json, out ServerMessage message)
        {
            message = new ServerMessage();

            foreach (Match match in StringField.Matches(json))
            {
                if (match.Groups["key"].Value == "type")
                {
                    message.Type = match.Groups["value"].Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(message.Type))
            {
                message = null;
                return false;
            }

            foreach (Match match in IntegerField.Matches(json))
            {
                if (!int.TryParse(
                        match.Groups["value"].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int value))
                {
                    continue;
                }

                switch (match.Groups["key"].Value)
                {
                    case "player_id":
                        message.PlayerId = value;
                        break;
                    case "round":
                        message.Round = value;
                        break;
                    case "winner":
                        message.Winner = value;
                        break;
                    case "answer_index":
                        message.AnswerIndex = value;
                        break;
                }
            }

            foreach (Match match in BooleanField.Matches(json))
            {
                if (match.Groups["key"].Value == "game_finished")
                {
                    message.GameFinished = match.Groups["value"].Value.ToLowerInvariant() == "true";
                }
            }

            Match scoresMatch = ScoresField.Match(json);
            if (scoresMatch.Success)
            {
                message.Scores = new Dictionary<int, int>();

                foreach (Match scoreMatch in ScoreEntry.Matches(scoresMatch.Groups["value"].Value))
                {
                    if (int.TryParse(scoreMatch.Groups["player"].Value, out int playerId) &&
                        int.TryParse(scoreMatch.Groups["score"].Value, out int score))
                    {
                        message.Scores[playerId] = score;
                    }
                }
            }

            return true;
        }
    }
}
