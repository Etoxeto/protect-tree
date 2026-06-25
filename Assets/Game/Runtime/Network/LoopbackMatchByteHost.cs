using System;
using System.IO;
using ProtectTree.Core.Network;
using ProtectTree.Runtime.Lua;

namespace ProtectTree.Runtime.Network
{
    /// <summary>
    /// Local byte-payload host adapter used to validate the codec path before a real transport exists.
    /// </summary>
    public sealed class LoopbackMatchByteHost
    {
        private readonly LuaLoopbackMatchHost _host;
        private readonly IMatchProtocolCodec _codec;

        public LoopbackMatchByteHost(
            LuaLoopbackMatchHost host,
            IMatchProtocolCodec codec)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        public long NextSnapshotSequence => _host.NextSnapshotSequence;

        public Exception LastCommandGameplayException =>
            _host.LastCommandGameplayException;

        public bool TrySubmitCommandPayload(
            int assignedPlayerId,
            byte[] payload,
            out CommandRejectionReason rejectionReason)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            // 真实传输层只负责搬运字节；这里模拟服务端收到字节后再进入协议解码和权威校验。
            NetworkMessageType messageType = _codec.GetMessageType(payload);
            if (messageType != NetworkMessageType.PlayerCommand)
            {
                throw new InvalidDataException(
                    $"Expected {NetworkMessageType.PlayerCommand} payload but found {messageType}.");
            }

            PlayerCommandEnvelope envelope = _codec.DecodePlayerCommand(payload);
            return _host.TrySubmitCommand(
                assignedPlayerId,
                envelope,
                out rejectionReason);
        }

        public byte[] CreateSnapshotPayload(int recipientPlayerId)
        {
            ServerSnapshotEnvelope envelope =
                _host.CreateSnapshotEnvelope(recipientPlayerId);
            return _codec.EncodeServerSnapshot(envelope);
        }

        public void Tick(float deltaTime)
        {
            _host.Tick(deltaTime);
        }
    }
}
