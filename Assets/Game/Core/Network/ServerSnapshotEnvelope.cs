using System;

namespace ProtectTree.Core.Network
{
    public sealed class ServerSnapshotEnvelope
    {
        public ServerSnapshotEnvelope(
            int protocolVersion,
            string matchId,
            long sequence,
            MatchStateSnapshot snapshot)
        {
            if (protocolVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(protocolVersion));
            }

            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match ID is required.", nameof(matchId));
            }

            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            ProtocolVersion = protocolVersion;
            MatchId = matchId;
            Sequence = sequence;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public int ProtocolVersion { get; }

        public string MatchId { get; }

        public long Sequence { get; }

        public MatchStateSnapshot Snapshot { get; }
    }
}
