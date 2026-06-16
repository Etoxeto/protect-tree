using ProtectTree.Core;

namespace ProtectTree.Runtime
{
    public static class ProjectRuntime
    {
        public static int MaxSupportedPlayers => GameLimits.MaxPlayers;
    }

    public static class MatchStartupOptions
    {
        private const int SinglePlayerCount = 1;
        private const int DefaultLocalPlayerId = 1;

        public static int PlayerCount { get; private set; } = SinglePlayerCount;

        public static int LocalPlayerId { get; private set; } = DefaultLocalPlayerId;

        public static int InitialObservedPlayerId { get; private set; } =
            DefaultLocalPlayerId;

        public static bool IsLocalMultiplayer => PlayerCount > SinglePlayerCount;

        public static void UseSinglePlayer()
        {
            PlayerCount = SinglePlayerCount;
            LocalPlayerId = DefaultLocalPlayerId;
            InitialObservedPlayerId = DefaultLocalPlayerId;
        }

        public static void UseLocalMultiplayer(
            int playerCount,
            int localPlayerId = DefaultLocalPlayerId)
        {
            if (playerCount < 2 || playerCount > ProjectRuntime.MaxSupportedPlayers)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(playerCount),
                    "Local multiplayer player count is out of range.");
            }

            if (localPlayerId < 1 || localPlayerId > playerCount)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(localPlayerId),
                    "Local player id must exist in the local multiplayer session.");
            }

            PlayerCount = playerCount;
            LocalPlayerId = localPlayerId;
            InitialObservedPlayerId = localPlayerId;
        }
    }
}
