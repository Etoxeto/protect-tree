using System;
using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Core.Network;

namespace ProtectTree.Runtime.Lua
{
    /// <summary>
    /// Builds recipient-scoped network snapshots from the local Lua authority.
    /// </summary>
    public sealed class LuaMatchSnapshotFactory
    {
        private readonly LuaRuntime _runtime;
        private readonly string _matchId;

        private long _nextSequence = 1;

        public LuaMatchSnapshotFactory(LuaRuntime runtime, string matchId)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match ID is required.", nameof(matchId));
            }

            _matchId = matchId;
        }

        public long NextSequence => _nextSequence;

        public MatchStateSnapshot CreateSnapshot(
            int recipientPlayerId,
            IReadOnlyList<MatchEvent> events = null)
        {
            // 快照公共状态对所有客户端一致；私有商店只取接收者自己的那份。
            return new MatchStateSnapshot(
                recipientPlayerId,
                _runtime.SimulationTick,
                _runtime.GetMatchFlowSnapshot(),
                _runtime.GetEnemyRosterSnapshot(),
                _runtime.GetPieceRosterSnapshot(),
                _runtime.GetPlayerRosterSnapshot(),
                _runtime.GetShopSnapshot(recipientPlayerId),
                events);
        }

        public ServerSnapshotEnvelope CreateEnvelope(
            int recipientPlayerId,
            IReadOnlyList<MatchEvent> events = null)
        {
            MatchStateSnapshot snapshot = CreateSnapshot(recipientPlayerId, events);
            long sequence = _nextSequence;
            _nextSequence++;

            return new ServerSnapshotEnvelope(
                NetworkProtocol.CurrentVersion,
                _matchId,
                sequence,
                snapshot);
        }
    }
}
