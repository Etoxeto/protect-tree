using System;
using UnityEngine;

namespace ProtectTree.Runtime
{
    public static class PlayerProfileOptions
    {
        private const string PlayerNameKey = "ProtectTree.PlayerName";
        private const string AvatarResourcePathKey = "ProtectTree.AvatarResourcePath";
        private const string DefaultPlayerName = "Player";

        public const string DefaultAvatarResourcePath =
            "UI/Infos/Player/Avatars/blade_of_god_rick";
        private const string LegacyDefaultAvatarResourcePath =
            "UI/Infos/Player/blade_of_god_rick";

        public static event Action AvatarChanged;

        public static string PlayerName
        {
            get
            {
                string value = PlayerPrefs.GetString(
                    PlayerNameKey,
                    DefaultPlayerName);
                return NormalizePlayerName(value);
            }
            set
            {
                PlayerPrefs.SetString(
                    PlayerNameKey,
                    NormalizePlayerName(value));
                PlayerPrefs.Save();
            }
        }

        public static string AvatarResourcePath
        {
            get
            {
                string value = PlayerPrefs.GetString(
                    AvatarResourcePathKey,
                    DefaultAvatarResourcePath);
                return NormalizeAvatarResourcePath(value);
            }
            set
            {
                string normalized = NormalizeAvatarResourcePath(value);
                PlayerPrefs.SetString(AvatarResourcePathKey, normalized);
                PlayerPrefs.Save();
                AvatarChanged?.Invoke();
            }
        }

        private static string NormalizePlayerName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultPlayerName;
            }

            value = value.Trim();
            return value.Length > 16 ? value.Substring(0, 16) : value;
        }

        public static string NormalizeAvatarResourcePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultAvatarResourcePath;
            }

            value = value.Trim().Replace('\\', '/');
            return value == LegacyDefaultAvatarResourcePath
                ? DefaultAvatarResourcePath
                : value;
        }
    }
}
