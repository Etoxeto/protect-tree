namespace ProtectTree.Core.Match
{
    public sealed class ShopRarityWeightSnapshot
    {
        public ShopRarityWeightSnapshot(int rarity, int weight)
        {
            Rarity = rarity;
            Weight = weight;
        }

        public int Rarity { get; }

        public int Weight { get; }
    }
}
