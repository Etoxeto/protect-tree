using System;
using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Core.Network;

namespace ProtectTree.Runtime.Lua
{
    /// <summary>
    /// Minimal host pipeline for local transport-free multiplayer plumbing.
    /// </summary>
    public sealed class LuaLoopbackMatchHost
    {
        private readonly LuaRuntime _runtime;
        private readonly LuaMatchCommandRouter _commandRouter;
        private readonly LuaMatchSnapshotFactory _snapshotFactory;

        public LuaLoopbackMatchHost(LuaRuntime runtime, string matchId)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

            HostCommandGate commandGate = new HostCommandGate(matchId);
            _commandRouter = new LuaMatchCommandRouter(_runtime, commandGate);
            _snapshotFactory = new LuaMatchSnapshotFactory(_runtime, matchId);
        }

        public long NextSnapshotSequence => _snapshotFactory.NextSequence;

        public Exception LastCommandGameplayException =>
            _commandRouter.LastGameplayException;

        public bool TrySubmitCommand(
            int assignedPlayerId,
            PlayerCommandEnvelope envelope,
            out CommandRejectionReason rejectionReason)
        {
            // assignedPlayerId 来自未来连接分配结果，而不是客户端自报身份。
            return _commandRouter.TryRoute(
                assignedPlayerId,
                envelope,
                out rejectionReason);
        }

        public void Tick(float deltaTime)
        {
            _runtime.Tick(deltaTime);
        }

        public IReadOnlyList<MatchEvent> DrainEvents()
        {
            return _runtime.DrainMatchEvents();
        }

        public ServerSnapshotEnvelope CreateSnapshotEnvelope(
            int recipientPlayerId,
            IReadOnlyList<MatchEvent> events = null)
        {
            return _snapshotFactory.CreateEnvelope(recipientPlayerId, events);
        }
    }
}
