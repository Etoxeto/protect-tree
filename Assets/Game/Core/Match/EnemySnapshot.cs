namespace ProtectTree.Core.Match
{
    public sealed class EnemySnapshot
    {
        public EnemySnapshot(
            int instanceId,
            string enemyId,
            int wave,
            int spawnIndex,
            int targetPlayerId,
            int health,
            int maxHealth,
            string status,
            int attackDamage,
            double attackIntervalSeconds,
            string attackType,
            string attackSfxId,
            bool isBoss,
            bool isEnraged,
            int routeId,
            double pathSpeed,
            double pathProgress,
            int? blockedByPieceInstanceId)
        {
            InstanceId = instanceId;
            EnemyId = enemyId;
            Wave = wave;
            SpawnIndex = spawnIndex;
            TargetPlayerId = targetPlayerId;
            Health = health;
            MaxHealth = maxHealth;
            Status = status;
            AttackDamage = attackDamage;
            AttackIntervalSeconds = attackIntervalSeconds;
            AttackType = attackType;
            AttackSfxId = attackSfxId ?? string.Empty;
            IsBoss = isBoss;
            IsEnraged = isEnraged;
            RouteId = routeId;
            PathSpeed = pathSpeed;
            PathProgress = pathProgress;
            BlockedByPieceInstanceId = blockedByPieceInstanceId;
        }

        public int InstanceId { get; }

        public string EnemyId { get; }

        public int Wave { get; }

        public int SpawnIndex { get; }

        public int TargetPlayerId { get; }

        public int Health { get; }

        public int MaxHealth { get; }

        public string Status { get; }

        public int AttackDamage { get; }

        public double AttackIntervalSeconds { get; }

        public string AttackType { get; }

        public string AttackSfxId { get; }

        public bool IsBoss { get; }

        public bool IsEnraged { get; }

        public int RouteId { get; }

        public double PathSpeed { get; }

        public double PathProgress { get; }

        public int? BlockedByPieceInstanceId { get; }
    }
}
