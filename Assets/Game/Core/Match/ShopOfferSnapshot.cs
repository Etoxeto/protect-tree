using System.Collections.Generic;

namespace ProtectTree.Core.Match
{
    public sealed class ShopOfferSnapshot
    {
        public ShopOfferSnapshot(
            int slotIndex,
            string pieceId,
            string displayName,
            string portrait,
            string classId,
            int rarity,
            int cost,
            IReadOnlyList<ShopSynergySnapshot> synergies,
            bool isSold,
            int maxHealth = 0,
            int maxBlockCount = 0,
            int damage = 0,
            double attackIntervalSeconds = 0,
            string featureDescription = null)
        {
            SlotIndex = slotIndex;
            PieceId = pieceId;
            DisplayName = displayName;
            Portrait = portrait;
            ClassId = classId;
            Rarity = rarity;
            Cost = cost;
            Synergies = synergies;
            IsSold = isSold;
            MaxHealth = maxHealth;
            MaxBlockCount = maxBlockCount;
            Damage = damage;
            AttackIntervalSeconds = attackIntervalSeconds;
            FeatureDescription = featureDescription ?? string.Empty;
        }

        public int SlotIndex { get; }

        public string PieceId { get; }

        public string DisplayName { get; }

        public string Portrait { get; }

        public string ClassId { get; }

        public int Rarity { get; }

        public int Cost { get; }

        public IReadOnlyList<ShopSynergySnapshot> Synergies { get; }

        public bool IsSold { get; }

        public int MaxHealth { get; }

        public int MaxBlockCount { get; }

        public int Damage { get; }

        public double AttackIntervalSeconds { get; }

        public string FeatureDescription { get; }
    }
}
