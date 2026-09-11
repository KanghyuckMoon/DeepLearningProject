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

        private static readonly Regex NicknamesField = new Regex(
            "\\\"nicknames\\\"\\s*:\\s*\\{(?<value>[^}]*)\\}",
            RegexOptions.Compiled);

        private static readonly Regex ScoreEntry = new Regex(
            "\\\"(?<player>\\d+)\\\"\\s*:\\s*(?<score>-?\\d+)",
            RegexOptions.Compiled);

        private static readonly Regex NicknameEntry = new Regex(
            "\\\"(?<player>\\d+)\\\"\\s*:\\s*\\\"(?<name>(?:\\\\.|[^\\\"])*)\\\"",
            RegexOptions.Compiled);

        public static string Ready()
        {
            return "{\"type\":\"ready\"}";
        }

        public static string Ping()
        {
            return "{\"type\":\"ping\"}";
        }

        public static string SetNickname(string nickname)
        {
            return "{\"type\":\"set_nickname\",\"nickname\":\"" +
                   Escape(nickname) + "\"}";
        }

        public static string Detected(int round)
        {
            return "{\"type\":\"detected\",\"round\":" +
                   round.ToString(CultureInfo.InvariantCulture) + "}";
        }

        public static string CameraFrame(
            string imageBase64,
            int width,
            int height,
            int rotation,
            bool mirrorHorizontally,
            bool flipVertically)
        {
            return "{\"type\":\"camera_frame\",\"image\":\"" + imageBase64 +
                   "\",\"width\":" + width.ToString(CultureInfo.InvariantCulture) +
                   ",\"height\":" + height.ToString(CultureInfo.InvariantCulture) +
                   ",\"rotation\":" + rotation.ToString(CultureInfo.InvariantCulture) +
                   ",\"mirror_x\":" + (mirrorHorizontally ? "true" : "false") +
                   ",\"flip_y\":" + (flipVertically ? "true" : "false") + "}";
        }

        public static bool TryParseServerMessage(string json, out ServerMessage message)
        {
            message = new ServerMessage();

            foreach (Match match in StringField.Matches(json))
            {
                switch (match.Groups["key"].Value)
                {
                    case "type":
                        message.Type = match.Groups["value"].Value;
                        break;
                    case "image":
                        message.ImageBase64 = match.Groups["value"].Value;
                        break;
                    case "nickname":
                        message.Nickname = Unescape(match.Groups["value"].Value);
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
                    case "max_players":
                        message.MaxPlayers = value;
                        break;
                    case "width":
                        message.ImageWidth = value;
                        break;
                    case "height":
                        message.ImageHeight = value;
                        break;
                    case "rotation":
                        message.Rotation = value;
                        break;
                }
            }

            foreach (Match match in BooleanField.Matches(json))
            {
                if (match.Groups["key"].Value == "game_finished")
                {
                    message.GameFinished = match.Groups["value"].Value.ToLowerInvariant() == "true";
                }
                else if (match.Groups["key"].Value == "mirror_x")
                {
                    message.MirrorHorizontally = match.Groups["value"].Value.ToLowerInvariant() == "true";
                }
                else if (match.Groups["key"].Value == "flip_y")
                {
                    message.FlipVertically = match.Groups["value"].Value.ToLowerInvariant() == "true";
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

            Match nicknamesMatch = NicknamesField.Match(json);
            if (nicknamesMatch.Success)
            {
                message.Nicknames = new Dictionary<int, string>();

                foreach (Match nicknameMatch in NicknameEntry.Matches(nicknamesMatch.Groups["value"].Value))
                {
                    if (int.TryParse(nicknameMatch.Groups["player"].Value, out int playerId))
                    {
                        message.Nicknames[playerId] = Unescape(nicknameMatch.Groups["name"].Value);
                    }
                }
            }

            return true;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }
}
