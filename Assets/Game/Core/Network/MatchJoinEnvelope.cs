using System;

namespace ProtectTree.Core.Network
{
    public sealed class MatchJoinEnvelope
    {
        public MatchJoinEnvelope(
            int protocolVersion,
            string roomId,
            string matchId,
            int playerId,
            string joinToken)
        {
            if (protocolVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(protocolVersion));
            }

            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException("Room ID is required.", nameof(roomId));
            }

            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match ID is required.", nameof(matchId));
            }

            if (playerId <= 0 || playerId > GameLimits.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId));
            }

            if (string.IsNullOrWhiteSpace(joinToken))
            {
                throw new ArgumentException(
                    "Join token is required.",
                    nameof(joinToken));
            }

            ProtocolVersion = protocolVersion;
            RoomId = roomId;
            MatchId = matchId;
            PlayerId = playerId;
            JoinToken = joinToken;
        }

        public int ProtocolVersion { get; }

        public string RoomId { get; }

        public string MatchId { get; }

        public int PlayerId { get; }

        public string JoinToken { get; }
    }
}
