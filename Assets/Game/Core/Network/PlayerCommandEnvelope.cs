using System;

namespace ProtectTree.Core.Network
{
    public sealed class PlayerCommandEnvelope
    {
        public PlayerCommandEnvelope(
            int protocolVersion,
            string matchId,
            int playerId,
            long sequence,
            MatchCommand command)
        {
            if (protocolVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(protocolVersion));
            }

            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match ID is required.", nameof(matchId));
            }

            if (playerId <= 0 || playerId > GameLimits.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId));
            }

            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            ProtocolVersion = protocolVersion;
            MatchId = matchId;
            PlayerId = playerId;
            Sequence = sequence;
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }

        public int ProtocolVersion { get; }

        public string MatchId { get; }

        public int PlayerId { get; }

        public long Sequence { get; }

        public MatchCommand Command { get; }
    }
}
