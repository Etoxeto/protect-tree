using System.Collections.Generic;

namespace ProtectTree.Core.Match
{
    public sealed class PlayerRosterSnapshot
    {
        public PlayerRosterSnapshot(
            int aliveCount,
            IReadOnlyList<PlayerSnapshot> players)
        {
            AliveCount = aliveCount;
            Players = players;
        }

        public int AliveCount { get; }

        public IReadOnlyList<PlayerSnapshot> Players { get; }
    }
}
