namespace ProtectTree.Core.Match
{
    public sealed class MatchEvent
    {
        public MatchEvent(
            string type,
            int? wave = null,
            string phase = null,
            string enemyId = null,
            int? spawnIndex = null,
            int? enemyInstanceId = null,
            string result = null,
            int? pieceInstanceId = null,
            int? sourcePieceInstanceId = null,
            int? sourceEnemyInstanceId = null,
            int? playerId = null,
            int? targetPlayerId = null,
            int? defenderPlayerId = null,
            int? leakOwnerPlayerId = null,
            int? initialLeakCount = null,
            int? rescuedCount = null,
            int? finalLeakCount = null,
            int? damage = null,
            int? leakCount = null,
            int? health = null,
            int? leakingPlayerCount = null,
            int? transferredEnemyCount = null,
            int? previousTargetPlayerId = null,
            int? maxHealth = null,
            bool? isBoss = null,
            string projectileId = null,
            double? castLockSeconds = null)
        {
            Type = type;
            Wave = wave;
            Phase = phase;
            EnemyId = enemyId;
            SpawnIndex = spawnIndex;
            EnemyInstanceId = enemyInstanceId;
            Result = result;
            PieceInstanceId = pieceInstanceId;
            SourcePieceInstanceId = sourcePieceInstanceId;
            SourceEnemyInstanceId = sourceEnemyInstanceId;
            PlayerId = playerId;
            TargetPlayerId = targetPlayerId;
            DefenderPlayerId = defenderPlayerId;
            LeakOwnerPlayerId = leakOwnerPlayerId;
            InitialLeakCount = initialLeakCount;
            RescuedCount = rescuedCount;
            FinalLeakCount = finalLeakCount;
            Damage = damage;
            LeakCount = leakCount;
            Health = health;
            LeakingPlayerCount = leakingPlayerCount;
            TransferredEnemyCount = transferredEnemyCount;
            PreviousTargetPlayerId = previousTargetPlayerId;
            MaxHealth = maxHealth;
            IsBoss = isBoss;
            ProjectileId = projectileId;
            CastLockSeconds = castLockSeconds;
        }

        public string Type { get; }

        public int? Wave { get; }

        public string Phase { get; }

        public string EnemyId { get; }

        public int? SpawnIndex { get; }

        public int? EnemyInstanceId { get; }

        public string Result { get; }

        public int? PieceInstanceId { get; }

        public int? SourcePieceInstanceId { get; }

        public int? SourceEnemyInstanceId { get; }

        public int? PlayerId { get; }

        public int? TargetPlayerId { get; }

        public int? DefenderPlayerId { get; }

        public int? LeakOwnerPlayerId { get; }

        public int? InitialLeakCount { get; }

        public int? RescuedCount { get; }

        public int? FinalLeakCount { get; }

        public int? Damage { get; }

        public int? LeakCount { get; }

        public int? Health { get; }

        public int? LeakingPlayerCount { get; }

        public int? TransferredEnemyCount { get; }

        public int? PreviousTargetPlayerId { get; }

        public int? MaxHealth { get; }

        public bool? IsBoss { get; }

        public string ProjectileId { get; }

        public double? CastLockSeconds { get; }
    }
}
