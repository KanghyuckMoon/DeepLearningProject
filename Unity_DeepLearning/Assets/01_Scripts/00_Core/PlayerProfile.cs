using System.Text;
using UnityEngine;

namespace DeepLearning.GameClient
{
    public static class PlayerProfile
    {
        public const int MaxNicknameLength = 16;
        private const string NicknameKey = "PlayerNickname";

        public static string Nickname
        {
            get
            {
                string nickname = Normalize(PlayerPrefs.GetString(NicknameKey, string.Empty));
                return string.IsNullOrEmpty(nickname) ? "Player" : nickname;
            }
        }

        public static string SavedNickname =>
            Normalize(PlayerPrefs.GetString(NicknameKey, string.Empty));

        public static bool TrySaveNickname(string value, out string nickname)
        {
            nickname = Normalize(value);
            if (string.IsNullOrEmpty(nickname))
            {
                return false;
            }

            PlayerPrefs.SetString(NicknameKey, nickname);
            PlayerPrefs.Save();
            return true;
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(MaxNicknameLength);
            foreach (char character in value.Trim())
            {
                if (!char.IsControl(character))
                {
                    builder.Append(character);
                }

                if (builder.Length >= MaxNicknameLength)
                {
                    break;
                }
            }

            return builder.ToString().Trim();
        }
    }
}
