using System.Collections.Generic;

namespace ProtectTree.Core.Match
{
    public sealed class ShopSnapshot
    {
        public ShopSnapshot(
            int playerId,
            int level,
            int maxLevel,
            bool canUpgrade,
            int upgradeCost,
            int refreshCost,
            bool isLocked,
            IReadOnlyList<ShopRarityWeightSnapshot> rarityWeights,
            IReadOnlyList<ShopOfferSnapshot> offers)
        {
            PlayerId = playerId;
            Level = level;
            MaxLevel = maxLevel;
            CanUpgrade = canUpgrade;
            UpgradeCost = upgradeCost;
            RefreshCost = refreshCost;
            IsLocked = isLocked;
            RarityWeights = rarityWeights;
            Offers = offers;
        }

        public int PlayerId { get; }

        public int Level { get; }

        public int MaxLevel { get; }

        public bool CanUpgrade { get; }

        public int UpgradeCost { get; }

        public int RefreshCost { get; }

        public bool IsLocked { get; }

        public IReadOnlyList<ShopRarityWeightSnapshot> RarityWeights { get; }

        public IReadOnlyList<ShopOfferSnapshot> Offers { get; }
    }
}
