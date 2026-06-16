using System;
using System.Collections.Generic;

namespace ProtectTree.Core.Network
{
    public sealed class LobbySnapshot
    {
        public LobbySnapshot(
            long revision,
            bool canStart,
            IReadOnlyList<LobbyPlayerSnapshot> players)
        {
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            if (players.Count > GameLimits.MaxPlayers)
            {
                throw new ArgumentException(
                    $"Lobby cannot exceed {GameLimits.MaxPlayers} players.",
                    nameof(players));
            }

            bool[] seenPlayerIds = new bool[GameLimits.MaxPlayers + 1];
            int hostCount = 0;
            foreach (LobbyPlayerSnapshot player in players)
            {
                if (player == null)
                {
                    throw new ArgumentException(
                        "Lobby players cannot contain null.",
                        nameof(players));
                }

                if (seenPlayerIds[player.PlayerId])
                {
                    throw new ArgumentException(
                        $"Duplicate player ID: {player.PlayerId}.",
                        nameof(players));
                }

                seenPlayerIds[player.PlayerId] = true;
                hostCount += player.IsHost ? 1 : 0;
            }

            if (hostCount > 1)
            {
                throw new ArgumentException(
                    "Lobby cannot contain more than one host.",
                    nameof(players));
            }

            Revision = revision;
            CanStart = canStart;
            Players = players;
        }

        public long Revision { get; }

        public int MaxPlayers => GameLimits.MaxPlayers;

        public bool CanStart { get; }

        public IReadOnlyList<LobbyPlayerSnapshot> Players { get; }
    }
}
