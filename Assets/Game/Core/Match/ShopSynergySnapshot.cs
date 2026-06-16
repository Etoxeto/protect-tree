namespace ProtectTree.Core.Match
{
    public sealed class ShopSynergySnapshot
    {
        public ShopSynergySnapshot(string synergyId, string displayName)
        {
            SynergyId = synergyId;
            DisplayName = displayName;
        }

        public string SynergyId { get; }

        public string DisplayName { get; }
    }
}
