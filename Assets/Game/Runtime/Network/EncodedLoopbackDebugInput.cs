using System;
using ProtectTree.Core.Match;
using ProtectTree.Core.Network;
using ProtectTree.Runtime.Lua;
using UnityEngine;

namespace ProtectTree.Runtime.Network
{
    [AddComponentMenu("Protect Tree/Debug/Encoded Loopback Debug Input")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LuaBootstrap))]
    public sealed class EncodedLoopbackDebugInput : MonoBehaviour
    {
        [SerializeField]
        private bool enableDebugInput;

        [SerializeField]
        private string matchId = "local-encoded-loopback";

        [SerializeField]
        private int playerOneId = 1;

        [SerializeField]
        private int playerTwoId = 2;

        [SerializeField]
        private KeyCode resetKey = KeyCode.F7;

        [SerializeField]
        private KeyCode playerOneReadyKey = KeyCode.F8;

        [SerializeField]
        private KeyCode playerTwoReadyKey = KeyCode.F9;

        [SerializeField]
        private KeyCode snapshotKey = KeyCode.F10;

        [SerializeField]
        private KeyCode bothReadyKey = KeyCode.F11;

        private LuaBootstrap _bootstrap;
        private LuaRuntime _runtime;
        private LoopbackMatchByteHost _byteHost;
        private BinaryMatchProtocolCodec _codec;
        private LoopbackMatchClient _playerOneClient;
        private LoopbackMatchClient _playerTwoClient;

        private void Awake()
        {
            _bootstrap = GetComponent<LuaBootstrap>();
        }

        private void Update()
        {
            if (!enableDebugInput)
            {
                return;
            }

            LuaRuntime runtime = _bootstrap.Runtime;
            if (runtime == null || !runtime.IsStarted)
            {
                ResetClients();
                return;
            }

            if (runtime != _runtime || _byteHost == null)
            {
                RebuildClients(runtime);
            }

            if (Input.GetKeyDown(resetKey))
            {
                RebuildClients(runtime);
                Debug.Log(
                    "[ProtectTree][Network Loopback] Rebuilt encoded loopback clients.",
                    this);
                return;
            }

            if (Input.GetKeyDown(playerOneReadyKey))
            {
                SendReadyAndReceive(_playerOneClient, playerOneId);
                return;
            }

            if (Input.GetKeyDown(playerTwoReadyKey))
            {
                SendReadyAndReceive(_playerTwoClient, playerTwoId);
                return;
            }

            if (Input.GetKeyDown(bothReadyKey))
            {
                SendReadyAndReceive(_playerOneClient, playerOneId);
                SendReadyAndReceive(_playerTwoClient, playerTwoId);
                return;
            }

            if (Input.GetKeyDown(snapshotKey))
            {
                ReceiveSnapshot(_playerOneClient, playerOneId);
                ReceiveSnapshot(_playerTwoClient, playerTwoId);
            }
        }

        private void RebuildClients(LuaRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _codec = new BinaryMatchProtocolCodec();
            LuaLoopbackMatchHost host =
                new LuaLoopbackMatchHost(_runtime, matchId);
            _byteHost = new LoopbackMatchByteHost(host, _codec);
            _playerOneClient = CreateClient(playerOneId);
            _playerTwoClient = CreateClient(playerTwoId);
        }

        private LoopbackMatchClient CreateClient(int playerId)
        {
            return new LoopbackMatchClient(
                _byteHost,
                _codec,
                playerId,
                matchId);
        }

        private void ResetClients()
        {
            _runtime = null;
            _byteHost = null;
            _codec = null;
            _playerOneClient = null;
            _playerTwoClient = null;
        }

        private void SendReadyAndReceive(
            LoopbackMatchClient client,
            int playerId)
        {
            if (client == null)
            {
                Debug.LogWarning(
                    "[ProtectTree][Network Loopback] Encoded client is not ready.",
                    this);
                return;
            }

            bool accepted = client.TrySendCommandAndReceiveSnapshot(
                MatchCommand.SetReady(true),
                out CommandRejectionReason commandRejectionReason,
                out SnapshotRejectionReason snapshotRejectionReason);

            if (!accepted)
            {
                Debug.LogWarning(
                    $"[ProtectTree][Network Loopback] Player {playerId} ready command rejected. Command={commandRejectionReason}, Snapshot={snapshotRejectionReason}, Encoded={client.UsesEncodedPayloads}.",
                    this);
                LogGameplayExceptionIfAny();
                return;
            }

            LogSnapshot(client, playerId, "ready command accepted");
        }

        private void ReceiveSnapshot(LoopbackMatchClient client, int playerId)
        {
            if (client == null)
            {
                Debug.LogWarning(
                    "[ProtectTree][Network Loopback] Encoded client is not ready.",
                    this);
                return;
            }

            if (!client.TryReceiveSnapshot(out SnapshotRejectionReason reason))
            {
                Debug.LogWarning(
                    $"[ProtectTree][Network Loopback] Player {playerId} snapshot rejected. Reason={reason}, Encoded={client.UsesEncodedPayloads}.",
                    this);
                return;
            }

            LogSnapshot(client, playerId, "snapshot received");
        }

        private void LogGameplayExceptionIfAny()
        {
            Exception exception = _byteHost?.LastCommandGameplayException;
            if (exception == null)
            {
                return;
            }

            Debug.LogWarning(
                $"[ProtectTree][Network Loopback] Lua authority rejected command: {exception.Message}",
                this);
        }

        private void LogSnapshot(
            LoopbackMatchClient client,
            int playerId,
            string action)
        {
            MatchStateSnapshot snapshot = client.LatestSnapshot;
            if (snapshot == null)
            {
                Debug.LogWarning(
                    $"[ProtectTree][Network Loopback] Player {playerId} {action}, but no snapshot was cached.",
                    this);
                return;
            }

            PlayerSnapshot player = FindPlayer(snapshot.Players, playerId);
            string readyText = player == null
                ? "unknown"
                : player.IsReady ? "ready" : "not ready";

            Debug.Log(
                $"[ProtectTree][Network Loopback] Player {playerId} {action}. Encoded={client.UsesEncodedPayloads}, SnapshotSeq={client.SnapshotReceiver.LatestEnvelope.Sequence}, Tick={snapshot.SimulationTick}, Phase={snapshot.Flow.Phase}, PlayerReady={readyText}.",
                this);
        }

        private static PlayerSnapshot FindPlayer(
            PlayerRosterSnapshot roster,
            int playerId)
        {
            if (roster == null)
            {
                return null;
            }

            foreach (PlayerSnapshot player in roster.Players)
            {
                if (player.PlayerId == playerId)
                {
                    return player;
                }
            }

            return null;
        }
    }
}
