using System;
using System.Collections.Generic;
using ProtectTree.Core.Match;

namespace ProtectTree.Core.Network
{
    public sealed class MatchStateSnapshot
    {
        public MatchStateSnapshot(
            int recipientPlayerId,
            long simulationTick,
            MatchFlowSnapshot flow,
            EnemyRosterSnapshot enemies,
            PieceRosterSnapshot pieces,
            PlayerRosterSnapshot players,
            ShopSnapshot shop,
            IReadOnlyList<MatchEvent> events = null)
        {
            if (recipientPlayerId <= 0 || recipientPlayerId > GameLimits.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(recipientPlayerId));
            }

            if (simulationTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationTick));
            }

            Flow = flow ?? throw new ArgumentNullException(nameof(flow));
            Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            Pieces = pieces ?? throw new ArgumentNullException(nameof(pieces));
            Players = players ?? throw new ArgumentNullException(nameof(players));
            Shop = shop ?? throw new ArgumentNullException(nameof(shop));
            Events = events ?? Array.Empty<MatchEvent>();

            // 每个客户端快照只携带接收者自己的商店，避免泄露其他玩家的私有商品。
            if (shop.PlayerId != recipientPlayerId)
            {
                throw new ArgumentException(
                    "Private shop must belong to the snapshot recipient.",
                    nameof(shop));
            }

            RecipientPlayerId = recipientPlayerId;
            SimulationTick = simulationTick;
        }

        public int RecipientPlayerId { get; }

        public long SimulationTick { get; }

        public MatchFlowSnapshot Flow { get; }

        public EnemyRosterSnapshot Enemies { get; }

        public PieceRosterSnapshot Pieces { get; }

        public PlayerRosterSnapshot Players { get; }

        public ShopSnapshot Shop { get; }

        public IReadOnlyList<MatchEvent> Events { get; }
    }
}
