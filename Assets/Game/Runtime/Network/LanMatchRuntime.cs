using System;
using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Core.Network;
using ProtectTree.Runtime.Lua;
using UnityEngine;

namespace ProtectTree.Runtime.Network
{
    [DisallowMultipleComponent]
    public sealed class LanMatchRuntime : MonoBehaviour
    {
        private const int MaxClientConnectAttempts = 30;
        private const float ClientConnectRetrySeconds = 0.5f;
        private const float HostSnapshotBroadcastSeconds = 0.1f;
        private const float HostSnapshotBroadcastLogSeconds = 1f;
        private const float ClientSnapshotLogSeconds = 1f;

        private static LanMatchRuntime _instance;

        private readonly Dictionary<int, int> _playerIdByConnectionId =
            new Dictionary<int, int>();
        private readonly Dictionary<int, int> _connectionIdByPlayerId =
            new Dictionary<int, int>();
        private readonly Dictionary<int, string> _matchJoinTokenByPlayerId =
            new Dictionary<int, string>();
        private readonly Dictionary<int, string> _avatarResourcePathByPlayerId =
            new Dictionary<int, string>();
        private readonly HashSet<int> _disconnectedPlayerIds =
            new HashSet<int>();

        private IMatchProtocolCodec _codec;
        private IMatchHostTransport _hostTransport;
        private IMatchClientTransport _clientTransport;
        private LuaLoopbackMatchHost _hostPipeline;
        private MatchCommandEnvelopeFactory _commandFactory;
        private MatchSnapshotReceiver _snapshotReceiver;
        private LuaRuntime _boundRuntime;
        private bool _clientIsConnecting;
        private int _clientConnectAttempts;
        private float _nextClientConnectTime;
        private float _nextHostSnapshotBroadcastTime;
        private float _nextHostSnapshotBroadcastLogTime;
        private float _nextClientSnapshotLogTime;
        private string _localMatchJoinToken;
        private bool _hasAcceptedAuthoritativeSnapshot;
        private bool _clientWaitingForReconnectSnapshot;

        public static LanMatchRuntime Instance => _instance;

        public static bool HasActiveSession => _instance != null
            && _instance.IsActive;

        public bool IsActive { get; private set; }

        public LanMatchRole Role { get; private set; } = LanMatchRole.None;

        public bool IsHost => Role == LanMatchRole.Host;

        public bool IsClient => Role == LanMatchRole.Client;

        public string RoomId { get; private set; }

        public string MatchId { get; private set; }

        public int PlayerCount { get; private set; }

        public int LocalPlayerId { get; private set; }

        public string HostAddress { get; private set; }

        public ushort MatchPort { get; private set; }

        public bool HasLiveMatchTransport
        {
            get
            {
                if (!IsActive)
                {
                    return false;
                }

                if (IsHost)
                {
                    return _hostTransport != null && _hostTransport.IsRunning;
                }

                return _clientTransport != null && _clientTransport.IsConnected;
            }
        }

        public bool HasRemoteSnapshot => _snapshotReceiver != null
            && _snapshotReceiver.HasSnapshot;

        public MatchStateSnapshot LatestRemoteSnapshot =>
            _snapshotReceiver?.LatestSnapshot;

        public bool IsClientConnecting => IsClient && _clientIsConnecting;

        public bool HasAcceptedAuthoritativeSnapshot =>
            _hasAcceptedAuthoritativeSnapshot;

        public bool IsClientWaitingForReconnectSnapshot =>
            IsClient && _clientWaitingForReconnectSnapshot;

        public bool IsClientRetryExhausted => IsClient
            && !_clientIsConnecting
            && !HasLiveMatchTransport
            && _clientConnectAttempts >= MaxClientConnectAttempts;

        public int ClientConnectAttempts => _clientConnectAttempts;

        public int MaxClientConnectAttemptsAllowed => MaxClientConnectAttempts;

        public int ReconnectablePlayerCount => _disconnectedPlayerIds.Count;

        public static LanMatchRuntime BeginHostSession(
            string roomId,
            string matchId,
            int playerCount,
            int localPlayerId,
            string hostAddress,
            ushort matchPort,
            IReadOnlyDictionary<int, string> matchJoinTokens,
            IReadOnlyDictionary<int, string> playerAvatarResourcePaths = null)
        {
            return BeginSession(
                LanMatchRole.Host,
                roomId,
                matchId,
                playerCount,
                localPlayerId,
                hostAddress,
                matchPort,
                GetRequiredJoinToken(matchJoinTokens, localPlayerId),
                matchJoinTokens,
                playerAvatarResourcePaths);
        }

        public static LanMatchRuntime BeginClientSession(
            string roomId,
            string matchId,
            int playerCount,
            int localPlayerId,
            string hostAddress,
            ushort matchPort,
            string matchJoinToken,
            IReadOnlyDictionary<int, string> playerAvatarResourcePaths = null)
        {
            return BeginSession(
                LanMatchRole.Client,
                roomId,
                matchId,
                playerCount,
                localPlayerId,
                hostAddress,
                matchPort,
                matchJoinToken,
                expectedMatchJoinTokens: null,
                playerAvatarResourcePaths);
        }

        public static void ClearActiveSession()
        {
            if (_instance == null)
            {
                return;
            }

            GameObject owner = _instance.gameObject;
            _instance.ResetState();
            _instance = null;

            if (Application.isPlaying)
            {
                Destroy(owner);
            }
            else
            {
                DestroyImmediate(owner);
            }
        }

        public void BindRuntime(LuaRuntime runtime)
        {
            if (!IsActive || runtime == null || _boundRuntime == runtime)
            {
                return;
            }

            Debug.Log(
                "[ProtectTree][LAN Match] Binding match runtime to Lua authority: "
                + Describe(),
                this);
            DisposeMatchTransport();
            _boundRuntime = runtime;
            _codec = new BinaryMatchProtocolCodec();

            try
            {
                if (IsHost)
                {
                    StartHostTransport(runtime);
                }
                else if (IsClient)
                {
                    _commandFactory = new MatchCommandEnvelopeFactory(
                        LocalPlayerId,
                        MatchId);
                    _snapshotReceiver = new MatchSnapshotReceiver(
                        LocalPlayerId,
                        MatchId);
                    _clientConnectAttempts = 0;
                    _nextClientConnectTime = 0f;
                    TryConnectClient();
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[ProtectTree][LAN Match] Failed to bind match runtime: "
                    + exception,
                    this);
            }
        }

        public bool TrySendCommand(MatchCommand command)
        {
            if (!IsActive || !IsClient || command == null)
            {
                return false;
            }

            if (_clientTransport == null || !_clientTransport.IsConnected)
            {
                Debug.LogWarning(
                    "[ProtectTree][LAN Match] Cannot send command before match transport connects.",
                    this);
                return false;
            }

            try
            {
                PlayerCommandEnvelope envelope =
                    _commandFactory.CreateEnvelope(command);
                _clientTransport.Send(_codec.EncodePlayerCommand(envelope));
                Debug.Log(
                    $"[ProtectTree][LAN Match] Sent {command.Type} command as player {LocalPlayerId}, seq {envelope.Sequence}.",
                    this);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[ProtectTree][LAN Match] Failed to send command: {exception.Message}",
                    this);
                return false;
            }
        }

        public bool DebugDisconnectClientTransportForReconnectTest()
        {
            if (!IsActive || !IsClient)
            {
                Debug.LogWarning(
                    "[ProtectTree][LAN Match] Debug reconnect test is only available on an active LAN Client.",
                    this);
                return false;
            }

            if (_clientTransport == null || !_clientTransport.IsConnected)
            {
                Debug.LogWarning(
                    "[ProtectTree][LAN Match] Debug reconnect test skipped because the match transport is not connected.",
                    this);
                return false;
            }

            // 只断开传输层，不清理房间、玩家、Token 等会话身份，用于验证短线重连。
            Debug.Log(
                "[ProtectTree][LAN Match] Debug disconnecting client match transport for reconnect test; session identity is kept.",
                this);
            _clientTransport.Disconnect();
            return true;
        }

        public string Describe()
        {
            if (!IsActive)
            {
                return "No active LAN match.";
            }

            return $"{Role} Match={MatchId} Room={RoomId} Player={LocalPlayerId}/{PlayerCount} Host={HostAddress}:{MatchPort}";
        }

        public string GetPlayerAvatarResourcePath(int playerId)
        {
            if (playerId <= 0)
            {
                return string.Empty;
            }

            return _avatarResourcePathByPlayerId.TryGetValue(
                playerId,
                out string avatarResourcePath)
                ? avatarResourcePath
                : string.Empty;
        }

        public string GetLifecycleDebugStatus()
        {
            if (!IsActive)
            {
                return "Inactive";
            }

            return "Role=" + Role
                + ", LiveTransport=" + HasLiveMatchTransport
                + ", Connecting=" + _clientIsConnecting
                + ", Attempts=" + _clientConnectAttempts
                + "/" + MaxClientConnectAttempts
                + ", HasRemoteSnapshot=" + HasRemoteSnapshot
                + ", HasAcceptedSnapshot=" + _hasAcceptedAuthoritativeSnapshot
                + ", WaitingReconnectSnapshot=" + _clientWaitingForReconnectSnapshot
                + ", ReconnectablePlayers=" + _disconnectedPlayerIds.Count;
        }

        private static LanMatchRuntime BeginSession(
            LanMatchRole role,
            string roomId,
            string matchId,
            int playerCount,
            int localPlayerId,
            string hostAddress,
            ushort matchPort,
            string matchJoinToken,
            IReadOnlyDictionary<int, string> expectedMatchJoinTokens,
            IReadOnlyDictionary<int, string> playerAvatarResourcePaths)
        {
            if (role == LanMatchRole.None)
            {
                throw new ArgumentOutOfRangeException(nameof(role));
            }

            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException("Room ID is required.", nameof(roomId));
            }

            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match ID is required.", nameof(matchId));
            }

            if (playerCount < 2 || playerCount > ProjectRuntime.MaxSupportedPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            }

            if (localPlayerId < 1 || localPlayerId > playerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(localPlayerId));
            }

            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                throw new ArgumentException(
                    "Host address is required.",
                    nameof(hostAddress));
            }

            if (matchPort == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(matchPort));
            }

            if (string.IsNullOrWhiteSpace(matchJoinToken))
            {
                throw new ArgumentException(
                    "Match join token is required.",
                    nameof(matchJoinToken));
            }

            LanMatchRuntime runtime = EnsureInstance();
            runtime.DisposeMatchTransport();
            runtime.IsActive = true;
            runtime.Role = role;
            runtime.RoomId = roomId;
            runtime.MatchId = matchId;
            runtime.PlayerCount = playerCount;
            runtime.LocalPlayerId = localPlayerId;
            runtime.HostAddress = hostAddress;
            runtime.MatchPort = matchPort;
            runtime._localMatchJoinToken = matchJoinToken;
            runtime._disconnectedPlayerIds.Clear();
            runtime.ConfigureExpectedMatchJoinTokens(
                role,
                playerCount,
                expectedMatchJoinTokens);
            runtime.ConfigurePlayerAvatarResourcePaths(
                playerCount,
                playerAvatarResourcePaths);
            return runtime;
        }

        private void ConfigurePlayerAvatarResourcePaths(
            int playerCount,
            IReadOnlyDictionary<int, string> playerAvatarResourcePaths)
        {
            _avatarResourcePathByPlayerId.Clear();
            if (playerAvatarResourcePaths == null)
            {
                return;
            }

            for (int playerId = 1; playerId <= playerCount; playerId++)
            {
                if (playerAvatarResourcePaths.TryGetValue(
                    playerId,
                    out string avatarResourcePath)
                    && !string.IsNullOrWhiteSpace(avatarResourcePath))
                {
                    _avatarResourcePathByPlayerId[playerId] =
                        avatarResourcePath.Trim().Replace('\\', '/');
                }
            }
        }

        private static string GetRequiredJoinToken(
            IReadOnlyDictionary<int, string> tokens,
            int playerId)
        {
            if (tokens == null
                || !tokens.TryGetValue(playerId, out string token)
                || string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException(
                    $"Missing match join token for player {playerId}.",
                    nameof(tokens));
            }

            return token;
        }

        private void ConfigureExpectedMatchJoinTokens(
            LanMatchRole role,
            int playerCount,
            IReadOnlyDictionary<int, string> tokens)
        {
            _matchJoinTokenByPlayerId.Clear();
            if (role != LanMatchRole.Host)
            {
                return;
            }

            if (tokens == null)
            {
                throw new ArgumentException(
                    "Host match join tokens are required.",
                    nameof(tokens));
            }

            for (int playerId = 1; playerId <= playerCount; playerId++)
            {
                _matchJoinTokenByPlayerId[playerId] =
                    GetRequiredJoinToken(tokens, playerId);
            }
        }

        private static LanMatchRuntime EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject owner = new GameObject("[ProtectTree] LAN Match Runtime");
            DontDestroyOnLoad(owner);
            _instance = owner.AddComponent<LanMatchRuntime>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            _hostTransport?.Pump();
            _clientTransport?.Pump();

            if (IsActive
                && IsClient
                && _boundRuntime != null
                && !_clientIsConnecting
                && (_clientTransport == null || !_clientTransport.IsConnected)
                && _clientConnectAttempts < MaxClientConnectAttempts
                && Time.unscaledTime >= _nextClientConnectTime)
            {
                TryConnectClient();
            }

            BroadcastHostSnapshotsIfDue();
        }

        private void OnDestroy()
        {
            DisposeMatchTransport();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void StartHostTransport(LuaRuntime runtime)
        {
            Debug.Log(
                $"[ProtectTree][LAN Match] Starting host transport on port {MatchPort}.",
                this);
            _hostPipeline = new LuaLoopbackMatchHost(runtime, MatchId);
            _hostTransport = new TcpMatchHostTransport();
            _hostTransport.ClientConnected += OnHostClientConnected;
            _hostTransport.ClientDisconnected += OnHostClientDisconnected;
            _hostTransport.MessageReceived += OnHostMessageReceived;
            _hostTransport.Start(MatchPort, PlayerCount - 1);

            Debug.Log(
                $"[ProtectTree][LAN Match] Host listening on port {MatchPort}.",
                this);
        }

        private void TryConnectClient()
        {
            if (!IsClient || string.IsNullOrWhiteSpace(HostAddress))
            {
                return;
            }

            try
            {
                DisposeClientTransport();
                _clientConnectAttempts++;
                _clientIsConnecting = true;
                _clientTransport = new TcpMatchClientTransport();
                _clientTransport.Connected += OnClientConnected;
                _clientTransport.Disconnected += OnClientDisconnected;
                _clientTransport.ConnectionFailed += OnClientConnectionFailed;
                _clientTransport.MessageReceived += OnClientMessageReceived;
                _clientTransport.Connect(HostAddress, MatchPort);

                Debug.Log(
                    $"[ProtectTree][LAN Match] Connecting to match host {HostAddress}:{MatchPort}, attempt {_clientConnectAttempts}.",
                    this);
            }
            catch (Exception exception)
            {
                _clientIsConnecting = false;
                DisposeClientTransport();
                ScheduleClientRetry();
                Debug.LogWarning(
                    $"[ProtectTree][LAN Match] Could not start client connection attempt: {exception.Message}",
                    this);
            }
        }

        private void OnHostClientConnected(int connectionId)
        {
            Debug.Log(
                $"[ProtectTree][LAN Match] Match client connected: {connectionId}.",
                this);
        }

        private void OnHostClientDisconnected(int connectionId)
        {
            int disconnectedPlayerId = 0;
            if (_playerIdByConnectionId.TryGetValue(
                connectionId,
                out int playerId))
            {
                disconnectedPlayerId = playerId;
                _playerIdByConnectionId.Remove(connectionId);
                _connectionIdByPlayerId.Remove(playerId);
                _disconnectedPlayerIds.Add(playerId);
            }

            if (disconnectedPlayerId > 0)
            {
                Debug.LogWarning(
                    "[ProtectTree][LAN Match] Match client disconnected: "
                    + $"connection {connectionId}, player {disconnectedPlayerId} marked reconnectable.",
                    this);
                return;
            }

            Debug.LogWarning(
                $"[ProtectTree][LAN Match] Match client disconnected: connection {connectionId}.",
                this);
        }

        private void OnHostMessageReceived(int connectionId, byte[] payload)
        {
            try
            {
                NetworkMessageType messageType = _codec.GetMessageType(payload);
                if (messageType == NetworkMessageType.MatchJoin)
                {
                    HandleHostMatchJoin(connectionId, payload);
                    return;
                }

                if (messageType != NetworkMessageType.PlayerCommand)
                {
                    Debug.LogWarning(
                        $"[ProtectTree][LAN Match] Host received unexpected message: {messageType}.",
                        this);
                    return;
                }

                PlayerCommandEnvelope envelope =
                    _codec.DecodePlayerCommand(payload);
                if (!_playerIdByConnectionId.TryGetValue(
                    connectionId,
                    out int assignedPlayerId))
                {
                    Debug.LogWarning(
                        "[ProtectTree][LAN Match] Host rejected player command before MatchJoin.",
                        this);
                    return;
                }

                bool accepted = _hostPipeline.TrySubmitCommand(
                    assignedPlayerId,
                    envelope,
                    out CommandRejectionReason rejectionReason);
                if (accepted)
                {
                    Debug.Log(
                        $"[ProtectTree][LAN Match] Host accepted {envelope.Command.Type} from player {assignedPlayerId}.",
                        this);
                }
                else
                {
                    Debug.LogWarning(
                        $"[ProtectTree][LAN Match] Host rejected {envelope.Command.Type} from player {assignedPlayerId}: {rejectionReason}.",
                        this);
                }

                SendSnapshot(
                    connectionId,
                    assignedPlayerId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[ProtectTree][LAN Match] Host message failed: {exception.Message}",
                    this);
            }
        }

        private void HandleHostMatchJoin(int connectionId, byte[] payload)
        {
            MatchJoinEnvelope envelope = _codec.DecodeMatchJoin(payload);
            if (!TryBindMatchJoin(
                connectionId,
                envelope,
                out int assignedPlayerId,
                out bool isReconnect))
            {
                _hostTransport?.DisconnectClient(connectionId);
                return;
            }

            if (isReconnect)
            {
                Debug.Log(
                    $"[ProtectTree][LAN Match] Host accepted reconnecting MatchJoin from player {assignedPlayerId}.",
                    this);
            }
            else
            {
                Debug.Log(
                    $"[ProtectTree][LAN Match] Host accepted MatchJoin from player {assignedPlayerId}.",
                    this);
            }

            SendSnapshot(connectionId, assignedPlayerId);
        }

        private bool TryBindMatchJoin(
            int connectionId,
            MatchJoinEnvelope envelope,
            out int assignedPlayerId,
            out bool isReconnect)
        {
            assignedPlayerId = 0;
            isReconnect = false;
            if (envelope == null)
            {
                return false;
            }

            if (_playerIdByConnectionId.TryGetValue(
                connectionId,
                out assignedPlayerId))
            {
                if (assignedPlayerId == envelope.PlayerId)
                {
                    return true;
                }

                Debug.LogWarning(
                    $"[ProtectTree][LAN Match] Connection {connectionId} tried to rejoin as player {envelope.PlayerId}, already bound to player {assignedPlayerId}.",
                    this);
                assignedPlayerId = 0;
                return false;
            }

            assignedPlayerId = envelope.PlayerId;
            if (!NetworkProtocol.IsSupported(envelope.ProtocolVersion)
                || !string.Equals(envelope.RoomId, RoomId, StringComparison.Ordinal)
                || !string.Equals(envelope.MatchId, MatchId, StringComparison.Ordinal)
                || assignedPlayerId <= 1
                || assignedPlayerId > PlayerCount
                || !HasExpectedMatchJoinToken(assignedPlayerId, envelope.JoinToken)
                || _connectionIdByPlayerId.ContainsKey(assignedPlayerId))
            {
                Debug.LogWarning(
                    $"[ProtectTree][LAN Match] Cannot join connection {connectionId} as player {envelope.PlayerId}.",
                    this);
                assignedPlayerId = 0;
                return false;
            }

            // 局内身份绑定与玩家命令分离，后续断线重连可在这里扩展 token 校验。
            _playerIdByConnectionId[connectionId] = assignedPlayerId;
            _connectionIdByPlayerId[assignedPlayerId] = connectionId;
            isReconnect = _disconnectedPlayerIds.Remove(assignedPlayerId);
            Debug.Log(
                $"[ProtectTree][LAN Match] Bound connection {connectionId} to player {assignedPlayerId}.",
                this);
            return true;
        }

        private bool HasExpectedMatchJoinToken(int playerId, string token)
        {
            return _matchJoinTokenByPlayerId.TryGetValue(playerId, out string expected)
                && string.Equals(expected, token, StringComparison.Ordinal);
        }

        private bool TryResolveAssignedPlayer(
            int connectionId,
            PlayerCommandEnvelope envelope,
            out int assignedPlayerId)
        {
            if (_playerIdByConnectionId.TryGetValue(
                connectionId,
                out assignedPlayerId))
            {
                return true;
            }

            assignedPlayerId = envelope.PlayerId;
            if (!NetworkProtocol.IsSupported(envelope.ProtocolVersion)
                || !string.Equals(envelope.MatchId, MatchId, StringComparison.Ordinal)
                || assignedPlayerId <= 1
                || assignedPlayerId > PlayerCount
                || _connectionIdByPlayerId.ContainsKey(assignedPlayerId))
            {
                Debug.LogWarning(
                    $"[ProtectTree][LAN Match] Cannot bind connection {connectionId} to player {envelope.PlayerId}.",
                    this);
                assignedPlayerId = 0;
                return false;
            }

            // 早期 LAN Demo 暂用命令 envelope 中的 playerId 绑定连接；
            // 后续需要独立 MatchJoin/重连握手，避免玩家冒用身份。
            _playerIdByConnectionId[connectionId] = assignedPlayerId;
            _connectionIdByPlayerId[assignedPlayerId] = connectionId;
            Debug.Log(
                $"[ProtectTree][LAN Match] Bound connection {connectionId} to player {assignedPlayerId}.",
                this);
            return true;
        }

        private void BroadcastHostSnapshotsIfDue()
        {
            if (!IsActive
                || !IsHost
                || _hostTransport == null
                || !_hostTransport.IsRunning
                || _hostPipeline == null
                || _connectionIdByPlayerId.Count == 0
                || Time.unscaledTime < _nextHostSnapshotBroadcastTime)
            {
                return;
            }

            _nextHostSnapshotBroadcastTime =
                Time.unscaledTime + HostSnapshotBroadcastSeconds;

            bool shouldLog =
                Time.unscaledTime >= _nextHostSnapshotBroadcastLogTime;
            if (shouldLog)
            {
                _nextHostSnapshotBroadcastLogTime =
                    Time.unscaledTime + HostSnapshotBroadcastLogSeconds;
            }

            int sentCount = 0;
            long lastSequence = 0;
            IReadOnlyList<MatchEvent> events = DrainHostEvents();
            LogSnapshotEvents(events);
            foreach (KeyValuePair<int, int> pair in _connectionIdByPlayerId)
            {
                int recipientPlayerId = pair.Key;
                int connectionId = pair.Value;
                try
                {
                    lastSequence = SendSnapshot(
                        connectionId,
                        recipientPlayerId,
                        events,
                        logIndividualSnapshot: false);
                    sentCount++;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[ProtectTree][LAN Match] Periodic snapshot failed for "
                        + $"player {recipientPlayerId}: {exception.Message}",
                        this);
                }
            }

            if (shouldLog && sentCount > 0)
            {
                Debug.Log(
                    "[ProtectTree][LAN Match] Broadcasted periodic snapshots "
                    + $"to {sentCount} client(s), last seq {lastSequence}.",
                    this);
            }
        }

        private long SendSnapshot(
            int connectionId,
            int recipientPlayerId,
            IReadOnlyList<MatchEvent> events = null,
            bool logIndividualSnapshot = true)
        {
            ServerSnapshotEnvelope envelope =
                _hostPipeline.CreateSnapshotEnvelope(recipientPlayerId, events);
            _hostTransport.Send(
                connectionId,
                _codec.EncodeServerSnapshot(envelope));
            if (logIndividualSnapshot)
            {
                Debug.Log(
                    $"[ProtectTree][LAN Match] Sent snapshot {envelope.Sequence} to player {recipientPlayerId}.",
                    this);
            }

            return envelope.Sequence;
        }

        private IReadOnlyList<MatchEvent> DrainHostEvents()
        {
            if (_hostPipeline == null)
            {
                return Array.Empty<MatchEvent>();
            }

            IReadOnlyList<MatchEvent> events = _hostPipeline.DrainEvents();
            return events ?? Array.Empty<MatchEvent>();
        }

        private void OnClientConnected()
        {
            _clientIsConnecting = false;
            Debug.Log(
                $"[ProtectTree][LAN Match] Connected to match host {HostAddress}:{MatchPort}.",
                this);
            if (TrySendMatchJoin())
            {
                if (_hasAcceptedAuthoritativeSnapshot)
                {
                    Debug.Log(
                        "[ProtectTree][LAN Match] Sent MatchJoin to resume authoritative snapshots.",
                        this);
                }
                else
                {
                    Debug.Log(
                        "[ProtectTree][LAN Match] Sent MatchJoin for initial authoritative snapshot.",
                        this);
                }
            }
        }

        private bool TrySendMatchJoin()
        {
            if (_clientTransport == null || !_clientTransport.IsConnected)
            {
                return false;
            }

            try
            {
                MatchJoinEnvelope envelope = new MatchJoinEnvelope(
                    NetworkProtocol.CurrentVersion,
                    RoomId,
                    MatchId,
                    LocalPlayerId,
                    _localMatchJoinToken);
                _clientTransport.Send(_codec.EncodeMatchJoin(envelope));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[ProtectTree][LAN Match] Failed to send MatchJoin: {exception.Message}",
                    this);
                return false;
            }
        }

        private void OnClientDisconnected()
        {
            if (!IsActive)
            {
                return;
            }

            _clientIsConnecting = false;
            if (_hasAcceptedAuthoritativeSnapshot)
            {
                _clientWaitingForReconnectSnapshot = true;
            }

            DisposeClientTransport();
            ScheduleClientRetry();
            Debug.LogWarning(
                "[ProtectTree][LAN Match] Disconnected from match host.",
                this);
        }

        private void OnClientConnectionFailed(string message)
        {
            _clientIsConnecting = false;
            DisposeClientTransport();
            ScheduleClientRetry();
            Debug.LogWarning(
                $"[ProtectTree][LAN Match] Match host connection failed: {message}",
                this);
        }

        private void OnClientMessageReceived(byte[] payload)
        {
            try
            {
                NetworkMessageType messageType = _codec.GetMessageType(payload);
                if (messageType != NetworkMessageType.ServerSnapshot)
                {
                    Debug.LogWarning(
                        $"[ProtectTree][LAN Match] Client received unexpected message: {messageType}.",
                        this);
                    return;
                }

                ServerSnapshotEnvelope envelope =
                    _codec.DecodeServerSnapshot(payload);
                if (_snapshotReceiver.TryReceive(
                    envelope,
                    out SnapshotRejectionReason rejectionReason))
                {
                    LogSnapshotEvents(envelope.Snapshot.Events);
                    if (!_hasAcceptedAuthoritativeSnapshot)
                    {
                        _hasAcceptedAuthoritativeSnapshot = true;
                        Debug.Log(
                            "[ProtectTree][LAN Match] Received first authoritative snapshot.",
                            this);
                    }
                    else if (_clientWaitingForReconnectSnapshot)
                    {
                        _clientWaitingForReconnectSnapshot = false;
                        Debug.Log(
                            "[ProtectTree][LAN Match] Received authoritative snapshot after reconnect.",
                            this);
                    }

                    _clientConnectAttempts = 0;
                    if (ShouldLogAcceptedSnapshot())
                    {
                        Debug.Log(
                            $"[ProtectTree][LAN Match] Accepted snapshot {envelope.Sequence}, tick {envelope.Snapshot.SimulationTick}.",
                            this);
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"[ProtectTree][LAN Match] Rejected snapshot {envelope.Sequence}: {rejectionReason}.",
                        this);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[ProtectTree][LAN Match] Client message failed: {exception.Message}",
                    this);
            }
        }

        private void LogSnapshotEvents(IReadOnlyList<MatchEvent> events)
        {
            if (events == null || events.Count == 0)
            {
                return;
            }

            foreach (MatchEvent matchEvent in events)
            {
                if (matchEvent == null)
                {
                    continue;
                }

                switch (matchEvent.Type)
                {
                    case "PhaseChanged":
                        LogBossPhaseEvent(matchEvent);
                        break;
                    case "EnemyCreated":
                        if (matchEvent.IsBoss == true)
                        {
                            Debug.Log(
                                "[ProtectTree][LAN Match] Event BossCreated "
                                + $"wave={matchEvent.Wave ?? 0}, "
                                + $"enemy={matchEvent.EnemyInstanceId ?? 0}, "
                                + $"target={matchEvent.TargetPlayerId ?? 0}.",
                                this);
                        }

                        break;
                    case "BossRetargeted":
                        Debug.Log(
                            "[ProtectTree][LAN Match] Event BossRetargeted "
                            + $"wave={matchEvent.Wave ?? 0}, "
                            + $"enemy={matchEvent.EnemyInstanceId ?? 0}, "
                            + $"from={matchEvent.PreviousTargetPlayerId ?? 0}, "
                            + $"to={matchEvent.TargetPlayerId ?? 0}, "
                            + $"health={matchEvent.Health ?? 0}/{matchEvent.MaxHealth ?? 0}.",
                            this);
                        break;
                    case "EnemyDamaged":
                        if (matchEvent.IsBoss == true)
                        {
                            Debug.Log(
                                "[ProtectTree][LAN Match] Event BossDamaged "
                                + $"wave={matchEvent.Wave ?? 0}, "
                                + $"enemy={matchEvent.EnemyInstanceId ?? 0}, "
                                + $"target={matchEvent.TargetPlayerId ?? 0}, "
                                + $"damage={matchEvent.Damage ?? 0}, "
                                + $"health={matchEvent.Health ?? 0}/{matchEvent.MaxHealth ?? 0}.",
                                this);
                        }

                        break;
                    case "EnemyDefeated":
                        if (matchEvent.IsBoss == true)
                        {
                            Debug.Log(
                                "[ProtectTree][LAN Match] Event BossDefeated "
                                + $"wave={matchEvent.Wave ?? 0}, "
                                + $"enemy={matchEvent.EnemyInstanceId ?? 0}, "
                                + $"target={matchEvent.TargetPlayerId ?? 0}.",
                                this);
                        }

                        break;
                    case "EnemyReachedEndpoint":
                        if (matchEvent.IsBoss == true)
                        {
                            Debug.Log(
                                "[ProtectTree][LAN Match] Event BossReachedEndpoint "
                                + $"wave={matchEvent.Wave ?? 0}, "
                                + $"enemy={matchEvent.EnemyInstanceId ?? 0}, "
                                + $"target={matchEvent.TargetPlayerId ?? 0}.",
                                this);
                        }

                        break;
                    case "JointDefenseStarted":
                        Debug.Log(
                            "[ProtectTree][LAN Match] Event JointDefenseStarted "
                            + $"wave={matchEvent.Wave ?? 0}, "
                            + $"defender={matchEvent.DefenderPlayerId ?? 0}, "
                            + $"leakingPlayers={matchEvent.LeakingPlayerCount ?? 0}, "
                            + $"transferred={matchEvent.TransferredEnemyCount ?? 0}.",
                            this);
                        break;
                    case "LeakedEnemyRescued":
                        Debug.Log(
                            "[ProtectTree][LAN Match] Event LeakedEnemyRescued "
                            + $"wave={matchEvent.Wave ?? 0}, "
                            + $"enemy={matchEvent.EnemyInstanceId ?? 0}, "
                            + $"defender={matchEvent.DefenderPlayerId ?? 0}, "
                            + $"leakOwner={matchEvent.LeakOwnerPlayerId ?? 0}.",
                            this);
                        break;
                    case "PlayerLeakResolved":
                        Debug.Log(
                            "[ProtectTree][LAN Match] Event PlayerLeakResolved "
                            + $"wave={matchEvent.Wave ?? 0}, "
                            + $"player={matchEvent.PlayerId ?? 0}, "
                            + $"initial={matchEvent.InitialLeakCount ?? 0}, "
                            + $"rescued={matchEvent.RescuedCount ?? 0}, "
                            + $"final={matchEvent.FinalLeakCount ?? 0}.",
                            this);
                        break;
                    case "PlayerDamaged":
                        Debug.Log(
                            "[ProtectTree][LAN Match] Event PlayerDamaged "
                            + $"wave={matchEvent.Wave ?? 0}, "
                            + $"player={matchEvent.PlayerId ?? 0}, "
                            + $"damage={matchEvent.Damage ?? 0}, "
                            + $"leaks={matchEvent.LeakCount ?? 0}, "
                            + $"health={matchEvent.Health ?? 0}.",
                            this);
                        break;
                }
            }
        }

        private void LogBossPhaseEvent(MatchEvent matchEvent)
        {
            if (matchEvent.Phase == "BossPreparation"
                || matchEvent.Phase == "BossBattle")
            {
                Debug.Log(
                    "[ProtectTree][LAN Match] Event BossPhaseChanged "
                    + $"wave={matchEvent.Wave ?? 0}, "
                    + $"phase={matchEvent.Phase}.",
                    this);
                return;
            }

            if (matchEvent.Phase == "End")
            {
                Debug.Log(
                    "[ProtectTree][LAN Match] Event MatchEnded "
                    + $"result={matchEvent.Result ?? "Unknown"}.",
                    this);
            }
        }

        private void ScheduleClientRetry()
        {
            if (_clientConnectAttempts >= MaxClientConnectAttempts)
            {
                Debug.LogWarning(
                    "[ProtectTree][LAN Match] Match host connection retries exhausted.",
                    this);
                return;
            }

            _nextClientConnectTime = Time.unscaledTime + ClientConnectRetrySeconds;
        }

        private void ResetState()
        {
            DisposeMatchTransport();
            IsActive = false;
            Role = LanMatchRole.None;
            RoomId = null;
            MatchId = null;
            PlayerCount = 0;
            LocalPlayerId = 0;
            HostAddress = null;
            MatchPort = 0;
            _localMatchJoinToken = null;
            _matchJoinTokenByPlayerId.Clear();
            _disconnectedPlayerIds.Clear();
        }

        private void DisposeMatchTransport()
        {
            DisposeHostTransport();
            DisposeClientTransport();
            _codec = null;
            _hostPipeline = null;
            _commandFactory = null;
            _snapshotReceiver = null;
            _boundRuntime = null;
            _clientIsConnecting = false;
            _clientConnectAttempts = 0;
            _nextClientConnectTime = 0f;
            _nextHostSnapshotBroadcastTime = 0f;
            _nextHostSnapshotBroadcastLogTime = 0f;
            _nextClientSnapshotLogTime = 0f;
            _hasAcceptedAuthoritativeSnapshot = false;
            _clientWaitingForReconnectSnapshot = false;
        }

        private bool ShouldLogAcceptedSnapshot()
        {
            if (Time.unscaledTime < _nextClientSnapshotLogTime)
            {
                return false;
            }

            _nextClientSnapshotLogTime =
                Time.unscaledTime + ClientSnapshotLogSeconds;
            return true;
        }

        private void DisposeHostTransport()
        {
            if (_hostTransport == null)
            {
                return;
            }

            _hostTransport.ClientConnected -= OnHostClientConnected;
            _hostTransport.ClientDisconnected -= OnHostClientDisconnected;
            _hostTransport.MessageReceived -= OnHostMessageReceived;
            _hostTransport.Dispose();
            _hostTransport = null;
            _playerIdByConnectionId.Clear();
            _connectionIdByPlayerId.Clear();
            _disconnectedPlayerIds.Clear();
        }

        private void DisposeClientTransport()
        {
            if (_clientTransport == null)
            {
                return;
            }

            _clientTransport.Connected -= OnClientConnected;
            _clientTransport.Disconnected -= OnClientDisconnected;
            _clientTransport.ConnectionFailed -= OnClientConnectionFailed;
            _clientTransport.MessageReceived -= OnClientMessageReceived;
            _clientTransport.Dispose();
            _clientTransport = null;
        }
    }
}
