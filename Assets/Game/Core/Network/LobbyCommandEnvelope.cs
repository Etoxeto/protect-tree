using System;

namespace ProtectTree.Core.Network
{
    public sealed class LobbyCommandEnvelope
    {
        public LobbyCommandEnvelope(
            int protocolVersion,
            string roomId,
            int playerId,
            long sequence,
            LobbyCommand command)
        {
            if (protocolVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(protocolVersion));
            }

            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException("Room ID is required.", nameof(roomId));
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
            RoomId = roomId;
            PlayerId = playerId;
            Sequence = sequence;
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }

        public int ProtocolVersion { get; }

        public string RoomId { get; }

        public int PlayerId { get; }

        public long Sequence { get; }

        public LobbyCommand Command { get; }
    }
}
