namespace ProtectTree.Core.Match
{
    public sealed class SynergyProgressSnapshot
    {
        public SynergyProgressSnapshot(
            int playerId,
            string synergyId,
            string displayName,
            int level,
            int uniquePieceCount,
            int requiredUniquePieces,
            int damageBonus)
        {
            PlayerId = playerId;
            SynergyId = synergyId;
            DisplayName = displayName;
            Level = level;
            UniquePieceCount = uniquePieceCount;
            RequiredUniquePieces = requiredUniquePieces;
            DamageBonus = damageBonus;
        }

        public int PlayerId { get; }

        public string SynergyId { get; }

        public string DisplayName { get; }

        public int Level { get; }

        public int UniquePieceCount { get; }

        public int RequiredUniquePieces { get; }

        public int DamageBonus { get; }

        public bool IsActive => Level > 0;
    }
}
