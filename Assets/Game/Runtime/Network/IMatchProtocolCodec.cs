using ProtectTree.Core.Network;

namespace ProtectTree.Runtime.Network
{
    /// <summary>
    /// Converts protocol envelopes to and from transport byte payloads.
    /// </summary>
    public interface IMatchProtocolCodec
    {
        NetworkMessageType GetMessageType(byte[] payload);

        byte[] EncodePlayerCommand(PlayerCommandEnvelope envelope);

        PlayerCommandEnvelope DecodePlayerCommand(byte[] payload);

        byte[] EncodeServerSnapshot(ServerSnapshotEnvelope envelope);

        ServerSnapshotEnvelope DecodeServerSnapshot(byte[] payload);

        byte[] EncodeLobbySnapshot(LobbySnapshot snapshot);

        LobbySnapshot DecodeLobbySnapshot(byte[] payload);

        byte[] EncodeLobbyCommand(LobbyCommandEnvelope envelope);

        LobbyCommandEnvelope DecodeLobbyCommand(byte[] payload);

        byte[] EncodeLobbyAssignment(LobbyAssignmentEnvelope envelope);

        LobbyAssignmentEnvelope DecodeLobbyAssignment(byte[] payload);

        byte[] EncodeMatchStart(MatchStartEnvelope envelope);

        MatchStartEnvelope DecodeMatchStart(byte[] payload);

        byte[] EncodeMatchJoin(MatchJoinEnvelope envelope);

        MatchJoinEnvelope DecodeMatchJoin(byte[] payload);
    }
}
