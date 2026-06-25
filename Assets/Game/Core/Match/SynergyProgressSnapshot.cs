namespace ProtectTree.Core.Match
{
    public sealed class SynergyProgressSnapshot
    {
        public SynergyProgressSnapshot(
            int playerId,
            string synergyId,
            string displayName,
            int level,
            int layerCount,
            int uniquePieceCount,
            int requiredUniquePieces,
            int damageBonus,
            string effectDescription = null)
        {
            PlayerId = playerId;
            SynergyId = synergyId;
            DisplayName = displayName;
            Level = level;
            LayerCount = layerCount;
            UniquePieceCount = uniquePieceCount;
            RequiredUniquePieces = requiredUniquePieces;
            DamageBonus = damageBonus;
            EffectDescription = effectDescription ?? string.Empty;
        }

        public int PlayerId { get; }

        public string SynergyId { get; }

        public string DisplayName { get; }

        public int Level { get; }

        public int LayerCount { get; }

        public int UniquePieceCount { get; }

        public int RequiredUniquePieces { get; }

        public int DamageBonus { get; }

        public string EffectDescription { get; }

        public bool IsActive => Level > 0;
    }
}
