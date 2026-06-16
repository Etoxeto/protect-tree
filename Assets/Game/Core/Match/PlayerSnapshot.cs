namespace ProtectTree.Core.Match
{
    public sealed class PlayerSnapshot
    {
        public PlayerSnapshot(
            int playerId,
            int health,
            int maxHealth,
            int gold,
            string status,
            bool isReady)
        {
            PlayerId = playerId;
            Health = health;
            MaxHealth = maxHealth;
            Gold = gold;
            Status = status;
            IsReady = isReady;
        }

        public int PlayerId { get; }

        public int Health { get; }

        public int MaxHealth { get; }

        public int Gold { get; }

        public string Status { get; }

        public bool IsReady { get; }
    }
}
