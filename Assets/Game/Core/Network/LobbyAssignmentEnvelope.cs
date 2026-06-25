using System;

namespace ProtectTree.Core.Network
{
    public sealed class LobbyAssignmentEnvelope
    {
        public LobbyAssignmentEnvelope(
            int protocolVersion,
            string roomId,
            int assignedPlayerId,
            int maxPlayers)
        {
            if (protocolVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(protocolVersion));
            }

            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException("Room ID is required.", nameof(roomId));
            }

            if (assignedPlayerId <= 0 || assignedPlayerId > GameLimits.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(assignedPlayerId));
            }

            if (maxPlayers <= 0 || maxPlayers > GameLimits.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPlayers));
            }

            ProtocolVersion = protocolVersion;
            RoomId = roomId;
            AssignedPlayerId = assignedPlayerId;
            MaxPlayers = maxPlayers;
        }

        public int ProtocolVersion { get; }

        public string RoomId { get; }

        public int AssignedPlayerId { get; }

        public int MaxPlayers { get; }
    }
}
