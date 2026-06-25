using System;

namespace ProtectTree.Core.Network
{
    public sealed class MatchStartEnvelope
    {
        public MatchStartEnvelope(
            int protocolVersion,
            string roomId,
            string matchId,
            int playerCount,
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

            if (playerCount < 2 || playerCount > GameLimits.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount));
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
            PlayerCount = playerCount;
            JoinToken = joinToken;
        }

        public int ProtocolVersion { get; }

        public string RoomId { get; }

        public string MatchId { get; }

        public int PlayerCount { get; }

        public string JoinToken { get; }
    }
}
