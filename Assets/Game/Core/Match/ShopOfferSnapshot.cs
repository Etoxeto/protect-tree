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
            bool isSold)
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
    }
}
