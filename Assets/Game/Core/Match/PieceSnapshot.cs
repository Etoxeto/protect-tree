using System.Collections.Generic;

namespace ProtectTree.Core.Match
{
    public sealed class PieceSnapshot
    {
        public PieceSnapshot(
            int instanceId,
            string pieceId,
            int ownerPlayerId,
            int level,
            string location,
            int? cellId,
            string terrain,
            string facing,
            int health,
            int maxHealth,
            string status,
            int maxBlockCount,
            IReadOnlyList<int> blockedEnemyInstanceIds,
            double recoverySecondsRemaining,
            int baseDamage,
            int damage,
            double attackIntervalSeconds,
            int sellValue,
            IReadOnlyList<string> deployableTerrains,
            string displayName = null,
            string portrait = null,
            string classId = null,
            int rarity = 1,
            IReadOnlyList<ShopSynergySnapshot> synergies = null)
        {
            InstanceId = instanceId;
            PieceId = pieceId;
            DisplayName = displayName ?? pieceId;
            Portrait = portrait ?? string.Empty;
            ClassId = classId ?? string.Empty;
            Rarity = rarity;
            OwnerPlayerId = ownerPlayerId;
            Level = level;
            Location = location;
            CellId = cellId;
            Terrain = terrain;
            Facing = facing;
            Health = health;
            MaxHealth = maxHealth;
            Status = status;
            MaxBlockCount = maxBlockCount;
            BlockedEnemyInstanceIds = blockedEnemyInstanceIds;
            RecoverySecondsRemaining = recoverySecondsRemaining;
            BaseDamage = baseDamage;
            Damage = damage;
            AttackIntervalSeconds = attackIntervalSeconds;
            SellValue = sellValue;
            DeployableTerrains = deployableTerrains;
            Synergies = synergies ?? new List<ShopSynergySnapshot>();
        }

        public int InstanceId { get; }

        public string PieceId { get; }

        public string DisplayName { get; }

        public string Portrait { get; }

        public string ClassId { get; }

        public int Rarity { get; }

        public int OwnerPlayerId { get; }

        public int Level { get; }

        public string Location { get; }

        public int? CellId { get; }

        public string Terrain { get; }

        public string Facing { get; }

        public int Health { get; }

        public int MaxHealth { get; }

        public string Status { get; }

        public int MaxBlockCount { get; }

        public IReadOnlyList<int> BlockedEnemyInstanceIds { get; }

        public double RecoverySecondsRemaining { get; }

        public int BaseDamage { get; }

        public int Damage { get; }

        public double AttackIntervalSeconds { get; }

        public int SellValue { get; }

        public IReadOnlyList<string> DeployableTerrains { get; }

        public IReadOnlyList<ShopSynergySnapshot> Synergies { get; }
    }
}
