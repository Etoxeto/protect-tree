using System;
using System.Collections.Generic;
using ProtectTree.Core;
using ProtectTree.Core.Network;

namespace ProtectTree.Runtime.Network
{
    public sealed class LobbyHostService : IDisposable
    {
        public const int HostPlayerId = 1;

        private readonly IMatchHostTransport _transport;
        private readonly IMatchProtocolCodec _codec;
        private readonly string _roomId;
        private readonly int _maxPlayers;
        private readonly Dictionary<int, LobbyPlayerState> _playersById =
            new Dictionary<int, LobbyPlayerState>();
        private readonly Dictionary<int, int> _playerIdByConnectionId =
            new Dictionary<int, int>();
        private readonly Dictionary<int, string> _lastMatchJoinTokens =
            new Dictionary<int, string>();
        private readonly long[] _lastCommandSequenceByPlayer =
            new long[GameLimits.MaxPlayers + 1];

        private long _revision;
        private bool _isStarted;
        private bool _hasStartedMatch;

        public LobbyHostService(
            IMatchHostTransport transport,
            IMatchProtocolCodec codec,
            string roomId,
            string hostDisplayName,
            int maxPlayers = GameLimits.MaxPlayers,
            string hostAvatarResourcePath = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException("Room ID is required.", nameof(roomId));
            }

            if (string.IsNullOrWhiteSpace(hostDisplayName))
            {
                throw new ArgumentException(
                    "Host display name is required.",
                    nameof(hostDisplayName));
            }

            if (maxPlayers < 2 || maxPlayers > GameLimits.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPlayers));
            }

            _roomId = roomId;
            _maxPlayers = maxPlayers;
            _playersById.Add(
                HostPlayerId,
                new LobbyPlayerState(
                    HostPlayerId,
                    hostDisplayName,
                    hostAvatarResourcePath,
                    true,
                    true,
                    -1));
            LatestLobbySnapshot = CreateSnapshot();
        }

        public event Action<LobbySnapshot> LobbyChanged;

        public event Action<int, int> PlayerAssigned;

        public event Action<MatchStartEnvelope> MatchStarted;

        public event Action<string> ProtocolError;

        public bool IsRunning => _isStarted && _transport.IsRunning;

        public string RoomId => _roomId;

        public int MaxPlayers => _maxPlayers;

        public LobbySnapshot LatestLobbySnapshot { get; private set; }

        public MatchStartEnvelope LastMatchStart { get; private set; }

        public IReadOnlyDictionary<int, string> LastMatchJoinTokens =>
            _lastMatchJoinTokens;

        public bool CanStart => LatestLobbySnapshot != null
            && LatestLobbySnapshot.CanStart;

        public void Start(ushort port)
        {
            if (_isStarted)
            {
                throw new InvalidOperationException("Lobby host is already started.");
            }

            _transport.ClientConnected += OnClientConnected;
            _transport.ClientDisconnected += OnClientDisconnected;
            _transport.MessageReceived += OnMessageReceived;
            _transport.Start(port, _maxPlayers - 1);
            _isStarted = true;
            PublishLobby();
        }

        public void Pump()
        {
            _transport.Pump();
        }

        public void SetHostReady(bool isReady)
        {
            _playersById[HostPlayerId].IsReady = isReady;
            PublishLobby();
        }

        public void SetHostDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Display name is required.",
                    nameof(displayName));
            }

            _playersById[HostPlayerId].DisplayName = displayName;
            PublishLobby();
        }

        public void SetHostAvatarResourcePath(string avatarResourcePath)
        {
            if (string.IsNullOrWhiteSpace(avatarResourcePath))
            {
                throw new ArgumentException(
                    "Avatar resource path is required.",
                    nameof(avatarResourcePath));
            }

            _playersById[HostPlayerId].AvatarResourcePath =
                avatarResourcePath.Trim().Replace('\\', '/');
            PublishLobby();
        }

        public bool TryStartMatch(out MatchStartEnvelope envelope)
        {
            envelope = null;
            if (_hasStartedMatch || !CanStart)
            {
                return false;
            }

            _hasStartedMatch = true;
            _lastMatchJoinTokens.Clear();
            foreach (LobbyPlayerSnapshot player in LatestLobbySnapshot.Players)
            {
                _lastMatchJoinTokens[player.PlayerId] =
                    Guid.NewGuid().ToString("N");
            }

            string matchId = Guid.NewGuid().ToString("N");
            envelope = new MatchStartEnvelope(
                NetworkProtocol.CurrentVersion,
                _roomId,
                matchId,
                LatestLobbySnapshot.Players.Count,
                _lastMatchJoinTokens[HostPlayerId]);
            LastMatchStart = envelope;

            foreach (LobbyPlayerState player in _playersById.Values)
            {
                if (!player.IsHost && player.ConnectionId > 0)
                {
                    MatchStartEnvelope playerStart = new MatchStartEnvelope(
                        NetworkProtocol.CurrentVersion,
                        _roomId,
                        matchId,
                        LatestLobbySnapshot.Players.Count,
                        _lastMatchJoinTokens[player.PlayerId]);
                    _transport.Send(
                        player.ConnectionId,
                        _codec.EncodeMatchStart(playerStart));
                }
            }

            MatchStarted?.Invoke(envelope);
            return true;
        }

        public void Stop()
        {
            if (!_isStarted)
            {
                return;
            }

            _transport.ClientConnected -= OnClientConnected;
            _transport.ClientDisconnected -= OnClientDisconnected;
            _transport.MessageReceived -= OnMessageReceived;
            _transport.Stop();
            _playerIdByConnectionId.Clear();

            List<int> remotePlayerIds = new List<int>();
            foreach (LobbyPlayerState player in _playersById.Values)
            {
                if (!player.IsHost)
                {
                    remotePlayerIds.Add(player.PlayerId);
                }
            }

            foreach (int playerId in remotePlayerIds)
            {
                _playersById.Remove(playerId);
            }

            _isStarted = false;
            PublishLobby();
            _hasStartedMatch = false;
            LastMatchStart = null;
            _lastMatchJoinTokens.Clear();
        }

        public void Dispose()
        {
            Stop();
            _transport.Dispose();
        }

        private void OnClientConnected(int connectionId)
        {
            int playerId = FindAvailablePlayerId();
            if (playerId == 0)
            {
                _transport.DisconnectClient(connectionId);
                return;
            }

            // 玩家 ID 只能由 Host 根据连接分配，客户端后续上行命令必须使用这个 ID。
            _playersById[playerId] = new LobbyPlayerState(
                playerId,
                "Player " + playerId,
                null,
                isConnected: true,
                isHost: false,
                connectionId: connectionId);
            _playerIdByConnectionId[connectionId] = playerId;
            _lastCommandSequenceByPlayer[playerId] = 0;

            LobbyAssignmentEnvelope assignment = new LobbyAssignmentEnvelope(
                NetworkProtocol.CurrentVersion,
                _roomId,
                playerId,
                _maxPlayers);
            _transport.Send(
                connectionId,
                _codec.EncodeLobbyAssignment(assignment));
            PlayerAssigned?.Invoke(connectionId, playerId);
            PublishLobby();
        }

        private void OnClientDisconnected(int connectionId)
        {
            if (!_playerIdByConnectionId.TryGetValue(
                connectionId,
                out int playerId))
            {
                return;
            }

            _playerIdByConnectionId.Remove(connectionId);
            _playersById.Remove(playerId);
            PublishLobby();
        }

        private void OnMessageReceived(int connectionId, byte[] payload)
        {
            try
            {
                NetworkMessageType messageType = _codec.GetMessageType(payload);
                if (messageType != NetworkMessageType.LobbyCommand)
                {
                    ProtocolError?.Invoke(
                        "Lobby host received unexpected message: " + messageType);
                    return;
                }

                LobbyCommandEnvelope envelope =
                    _codec.DecodeLobbyCommand(payload);
                if (!CanAcceptCommand(connectionId, envelope))
                {
                    return;
                }

                ApplyCommand(envelope);
                PublishLobby();
            }
            catch (Exception exception)
            {
                ProtocolError?.Invoke(exception.Message);
            }
        }

        private bool CanAcceptCommand(
            int connectionId,
            LobbyCommandEnvelope envelope)
        {
            if (!NetworkProtocol.IsSupported(envelope.ProtocolVersion))
            {
                ProtocolError?.Invoke("Unsupported lobby protocol version.");
                return false;
            }

            if (!string.Equals(envelope.RoomId, _roomId, StringComparison.Ordinal))
            {
                ProtocolError?.Invoke("Lobby command was sent to the wrong room.");
                return false;
            }

            if (!_playerIdByConnectionId.TryGetValue(
                connectionId,
                out int assignedPlayerId)
                || assignedPlayerId != envelope.PlayerId)
            {
                ProtocolError?.Invoke(
                    "Lobby command player ID does not match its connection.");
                return false;
            }

            if (envelope.Sequence <= _lastCommandSequenceByPlayer[envelope.PlayerId])
            {
                ProtocolError?.Invoke("Stale lobby command sequence.");
                return false;
            }

            _lastCommandSequenceByPlayer[envelope.PlayerId] = envelope.Sequence;
            return true;
        }

        private void ApplyCommand(LobbyCommandEnvelope envelope)
        {
            LobbyPlayerState player = _playersById[envelope.PlayerId];
            switch (envelope.Command.Type)
            {
                case LobbyCommandType.SetReady:
                    player.IsReady = envelope.Command.IsReady.GetValueOrDefault();
                    break;
                case LobbyCommandType.SetDisplayName:
                    player.DisplayName = envelope.Command.DisplayName;
                    break;
                case LobbyCommandType.SetAvatar:
                    player.AvatarResourcePath =
                        envelope.Command.AvatarResourcePath;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(envelope),
                        envelope.Command.Type,
                        "Unsupported lobby command.");
            }
        }

        private void PublishLobby()
        {
            _revision++;
            LatestLobbySnapshot = CreateSnapshot();
            byte[] payload = _codec.EncodeLobbySnapshot(LatestLobbySnapshot);

            foreach (LobbyPlayerState player in _playersById.Values)
            {
                if (!player.IsHost && player.ConnectionId > 0)
                {
                    _transport.Send(player.ConnectionId, payload);
                }
            }

            LobbyChanged?.Invoke(LatestLobbySnapshot);
        }

        private LobbySnapshot CreateSnapshot()
        {
            List<LobbyPlayerSnapshot> players = new List<LobbyPlayerSnapshot>();
            for (int playerId = 1; playerId <= _maxPlayers; playerId++)
            {
                if (_playersById.TryGetValue(playerId, out LobbyPlayerState player))
                {
                    players.Add(player.ToSnapshot());
                }
            }

            return new LobbySnapshot(
                _revision,
                CanStartFromPlayers(players),
                players);
        }

        private static bool CanStartFromPlayers(
            IReadOnlyList<LobbyPlayerSnapshot> players)
        {
            if (players.Count < 2)
            {
                return false;
            }

            foreach (LobbyPlayerSnapshot player in players)
            {
                if (!player.IsConnected || !player.IsReady)
                {
                    return false;
                }
            }

            return true;
        }

        private int FindAvailablePlayerId()
        {
            for (int playerId = 2; playerId <= _maxPlayers; playerId++)
            {
                if (!_playersById.ContainsKey(playerId))
                {
                    return playerId;
                }
            }

            return 0;
        }

        private sealed class LobbyPlayerState
        {
            public LobbyPlayerState(
                int playerId,
                string displayName,
                string avatarResourcePath,
                bool isConnected,
                bool isHost,
                int connectionId)
            {
                PlayerId = playerId;
                DisplayName = displayName;
                AvatarResourcePath = avatarResourcePath ?? string.Empty;
                IsConnected = isConnected;
                IsHost = isHost;
                ConnectionId = connectionId;
            }

            public int PlayerId { get; }

            public string DisplayName { get; set; }

            public string AvatarResourcePath { get; set; }

            public bool IsConnected { get; }

            public bool IsHost { get; }

            public int ConnectionId { get; }

            public bool IsReady { get; set; }

            public LobbyPlayerSnapshot ToSnapshot()
            {
                return new LobbyPlayerSnapshot(
                    PlayerId,
                    DisplayName,
                    IsConnected,
                    IsReady,
                    IsHost,
                    AvatarResourcePath);
            }
        }
    }
}
