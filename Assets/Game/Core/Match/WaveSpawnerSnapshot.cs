namespace ProtectTree.Core.Match
{
    public sealed class WaveSpawnerSnapshot
    {
        public WaveSpawnerSnapshot(
            bool isActive,
            int wave,
            string enemyId,
            int routeId,
            int remainingCount,
            int nextSpawnIndex,
            double secondsUntilNextSpawn)
        {
            IsActive = isActive;
            Wave = wave;
            EnemyId = enemyId;
            RouteId = routeId;
            RemainingCount = remainingCount;
            NextSpawnIndex = nextSpawnIndex;
            SecondsUntilNextSpawn = secondsUntilNextSpawn;
        }

        public bool IsActive { get; }

        public int Wave { get; }

        public string EnemyId { get; }

        public int RouteId { get; }

        public int RemainingCount { get; }

        public int NextSpawnIndex { get; }

        public double SecondsUntilNextSpawn { get; }
    }
}
