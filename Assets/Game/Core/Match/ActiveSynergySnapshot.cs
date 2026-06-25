namespace ProtectTree.Core.Match
{
    public sealed class ActiveSynergySnapshot
    {
        public ActiveSynergySnapshot(
            int playerId,
            string synergyId,
            int level,
            int layerCount,
            int uniquePieceCount,
            int requiredUniquePieces,
            int damageBonus,
            string effectDescription = null)
        {
            PlayerId = playerId;
            SynergyId = synergyId;
            Level = level;
            LayerCount = layerCount;
            UniquePieceCount = uniquePieceCount;
            RequiredUniquePieces = requiredUniquePieces;
            DamageBonus = damageBonus;
            EffectDescription = effectDescription ?? string.Empty;
        }

        public int PlayerId { get; }

        public string SynergyId { get; }

        public int Level { get; }

        public int LayerCount { get; }

        public int UniquePieceCount { get; }

        public int RequiredUniquePieces { get; }

        public int DamageBonus { get; }

        public string EffectDescription { get; }
    }
}
