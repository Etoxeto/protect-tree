using System;

namespace ProtectTree.Core.Network
{
    public sealed class ClientSnapshotGate
    {
        private readonly string _matchId;
        private long _lastAcceptedSequence;
        private long _lastAcceptedSimulationTick;

        public ClientSnapshotGate(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match ID is required.", nameof(matchId));
            }

            _matchId = matchId;
        }

        public long LastAcceptedSequence => _lastAcceptedSequence;

        public long LastAcceptedSimulationTick => _lastAcceptedSimulationTick;

        public bool TryAccept(
            ServerSnapshotEnvelope envelope,
            out SnapshotRejectionReason rejectionReason)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            if (!NetworkProtocol.IsSupported(envelope.ProtocolVersion))
            {
                rejectionReason = SnapshotRejectionReason.UnsupportedProtocol;
                return false;
            }

            if (!string.Equals(envelope.MatchId, _matchId, StringComparison.Ordinal))
            {
                rejectionReason = SnapshotRejectionReason.WrongMatch;
                return false;
            }

            if (envelope.Sequence <= _lastAcceptedSequence)
            {
                rejectionReason = SnapshotRejectionReason.StaleSequence;
                return false;
            }

            if (envelope.Snapshot.SimulationTick < _lastAcceptedSimulationTick)
            {
                rejectionReason = SnapshotRejectionReason.OlderSimulationTick;
                return false;
            }

            _lastAcceptedSequence = envelope.Sequence;
            _lastAcceptedSimulationTick = envelope.Snapshot.SimulationTick;
            rejectionReason = SnapshotRejectionReason.None;
            return true;
        }
    }
}
