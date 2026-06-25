using System;

namespace ProtectTree.Core.Network
{
    public sealed class LobbyCommand
    {
        private LobbyCommand(
            LobbyCommandType type,
            bool? isReady = null,
            string displayName = null,
            string avatarResourcePath = null)
        {
            Type = type;
            IsReady = isReady;
            DisplayName = displayName;
            AvatarResourcePath = avatarResourcePath;
        }

        public LobbyCommandType Type { get; }

        public bool? IsReady { get; }

        public string DisplayName { get; }

        public string AvatarResourcePath { get; }

        public static LobbyCommand SetReady(bool isReady)
        {
            return new LobbyCommand(
                LobbyCommandType.SetReady,
                isReady: isReady);
        }

        public static LobbyCommand SetDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Display name is required.",
                    nameof(displayName));
            }

            return new LobbyCommand(
                LobbyCommandType.SetDisplayName,
                displayName: displayName);
        }

        public static LobbyCommand SetAvatar(string avatarResourcePath)
        {
            if (string.IsNullOrWhiteSpace(avatarResourcePath))
            {
                throw new ArgumentException(
                    "Avatar resource path is required.",
                    nameof(avatarResourcePath));
            }

            return new LobbyCommand(
                LobbyCommandType.SetAvatar,
                avatarResourcePath: avatarResourcePath.Trim().Replace('\\', '/'));
        }
    }
}
