namespace ProtectTree.Core.Match
{
    public sealed class ActiveSynergySnapshot
    {
        public ActiveSynergySnapshot(
            int playerId,
            string synergyId,
            int level,
            int uniquePieceCount,
            int requiredUniquePieces,
            int damageBonus)
        {
            PlayerId = playerId;
            SynergyId = synergyId;
            Level = level;
            UniquePieceCount = uniquePieceCount;
            RequiredUniquePieces = requiredUniquePieces;
            DamageBonus = damageBonus;
        }

        public int PlayerId { get; }

        public string SynergyId { get; }

        public int Level { get; }

        public int UniquePieceCount { get; }

        public int RequiredUniquePieces { get; }

        public int DamageBonus { get; }
    }
}
