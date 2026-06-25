using System;
using ProtectTree.Core;
using ProtectTree.Core.Network;

namespace ProtectTree.Runtime.Network
{
    /// <summary>
    /// Client-side cache for accepted authoritative match snapshots.
    /// </summary>
    public sealed class MatchSnapshotReceiver
    {
        private readonly int _localPlayerId;
        private readonly ClientSnapshotGate _snapshotGate;

        public MatchSnapshotReceiver(int localPlayerId, string matchId)
        {
            if (localPlayerId <= 0 || localPlayerId > GameLimits.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(localPlayerId));
            }

            _localPlayerId = localPlayerId;
            _snapshotGate = new ClientSnapshotGate(matchId);
        }

        public int LocalPlayerId => _localPlayerId;

        public bool HasSnapshot => LatestSnapshot != null;

        public MatchStateSnapshot LatestSnapshot { get; private set; }

        public ServerSnapshotEnvelope LatestEnvelope { get; private set; }

        public long LastAcceptedSequence => _snapshotGate.LastAcceptedSequence;

        public long LastAcceptedSimulationTick =>
            _snapshotGate.LastAcceptedSimulationTick;

        public bool TryReceive(
            ServerSnapshotEnvelope envelope,
            out SnapshotRejectionReason rejectionReason)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            // 客户端只接收发给自己的快照，避免误用其他玩家的私有商店数据。
            if (envelope.Snapshot.RecipientPlayerId != _localPlayerId)
            {
                rejectionReason = SnapshotRejectionReason.WrongRecipient;
                return false;
            }

            if (!_snapshotGate.TryAccept(envelope, out rejectionReason))
            {
                return false;
            }

            LatestEnvelope = envelope;
            LatestSnapshot = envelope.Snapshot;
            return true;
        }
    }
}
