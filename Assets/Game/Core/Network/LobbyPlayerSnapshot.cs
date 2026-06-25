using System;

namespace ProtectTree.Core.Network
{
    public sealed class LobbyPlayerSnapshot
    {
        public LobbyPlayerSnapshot(
            int playerId,
            string displayName,
            bool isConnected,
            bool isReady,
            bool isHost,
            string avatarResourcePath = null)
        {
            if (playerId <= 0 || playerId > GameLimits.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Display name is required.",
                    nameof(displayName));
            }

            PlayerId = playerId;
            DisplayName = displayName;
            AvatarResourcePath = avatarResourcePath ?? string.Empty;
            IsConnected = isConnected;
            IsReady = isReady;
            IsHost = isHost;
        }

        public int PlayerId { get; }

        public string DisplayName { get; }

        public string AvatarResourcePath { get; }

        public bool IsConnected { get; }

        public bool IsReady { get; }

        public bool IsHost { get; }
    }
}
