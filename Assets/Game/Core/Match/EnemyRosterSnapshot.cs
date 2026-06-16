using System.Collections.Generic;

namespace ProtectTree.Core.Match
{
    public sealed class EnemyRosterSnapshot
    {
        public EnemyRosterSnapshot(
            int aliveCount,
            IReadOnlyList<EnemySnapshot> enemies)
        {
            AliveCount = aliveCount;
            Enemies = enemies;
        }

        public int AliveCount { get; }

        public IReadOnlyList<EnemySnapshot> Enemies { get; }
    }
}
