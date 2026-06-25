using System;
using ProtectTree.Core.Network;
using ProtectTree.Runtime.Lua;

namespace ProtectTree.Runtime.Network
{
    /// <summary>
    /// Transport-free client endpoint paired with a local loopback host.
    /// </summary>
    public sealed class LoopbackMatchClient
    {
        private readonly LuaLoopbackMatchHost _host;
        private readonly LoopbackMatchByteHost _byteHost;
        private readonly IMatchProtocolCodec _codec;
        private readonly MatchCommandEnvelopeFactory _commandFactory;
        private readonly MatchSnapshotReceiver _snapshotReceiver;

        public LoopbackMatchClient(
            LuaLoopbackMatchHost host,
            int localPlayerId,
            string matchId)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _byteHost = null;
            _codec = null;
            _commandFactory =
                new MatchCommandEnvelopeFactory(localPlayerId, matchId);
            _snapshotReceiver = new MatchSnapshotReceiver(localPlayerId, matchId);
        }

        public LoopbackMatchClient(
            LoopbackMatchByteHost byteHost,
            IMatchProtocolCodec codec,
            int localPlayerId,
            string matchId)
        {
            _host = null;
            _byteHost = byteHost
                ?? throw new ArgumentNullException(nameof(byteHost));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _commandFactory =
                new MatchCommandEnvelopeFactory(localPlayerId, matchId);
            _snapshotReceiver = new MatchSnapshotReceiver(localPlayerId, matchId);
        }

        public int LocalPlayerId => _commandFactory.LocalPlayerId;

        public long NextCommandSequence => _commandFactory.NextSequence;

        public bool HasSnapshot => _snapshotReceiver.HasSnapshot;

        public MatchStateSnapshot LatestSnapshot => _snapshotReceiver.LatestSnapshot;

        public MatchSnapshotReceiver SnapshotReceiver => _snapshotReceiver;

        public bool UsesEncodedPayloads => _byteHost != null;

        public bool TrySendCommand(
            MatchCommand command,
            out CommandRejectionReason rejectionReason)
        {
            PlayerCommandEnvelope envelope =
                _commandFactory.CreateEnvelope(command);

            if (_byteHost != null)
            {
                byte[] payload = _codec.EncodePlayerCommand(envelope);
                return _byteHost.TrySubmitCommandPayload(
                    LocalPlayerId,
                    payload,
                    out rejectionReason);
            }

            // 旧回环路径仍直接传 envelope，便于对比排查；真实网络形状请使用 byteHost 构造函数。
            return _host.TrySubmitCommand(
                LocalPlayerId,
                envelope,
                out rejectionReason);
        }

        public bool TryReceiveSnapshot(
            out SnapshotRejectionReason rejectionReason)
        {
            if (_byteHost != null)
            {
                byte[] payload = _byteHost.CreateSnapshotPayload(LocalPlayerId);
                ServerSnapshotEnvelope decodedEnvelope =
                    _codec.DecodeServerSnapshot(payload);
                return _snapshotReceiver.TryReceive(
                    decodedEnvelope,
                    out rejectionReason);
            }

            ServerSnapshotEnvelope envelope =
                _host.CreateSnapshotEnvelope(LocalPlayerId);
            return _snapshotReceiver.TryReceive(envelope, out rejectionReason);
        }

        public bool TrySendCommandAndReceiveSnapshot(
            MatchCommand command,
            out CommandRejectionReason commandRejectionReason,
            out SnapshotRejectionReason snapshotRejectionReason)
        {
            if (!TrySendCommand(command, out commandRejectionReason))
            {
                snapshotRejectionReason = SnapshotRejectionReason.None;
                return false;
            }

            return TryReceiveSnapshot(out snapshotRejectionReason);
        }
    }
}
