using System;
using ProtectTree.Core.Network;

namespace ProtectTree.Runtime.Network
{
    public sealed class LobbyClientService : IDisposable
    {
        private readonly IMatchClientTransport _transport;
        private readonly IMatchProtocolCodec _codec;
        private readonly string _displayName;
        private readonly string _avatarResourcePath;

        private long _nextCommandSequence = 1;
        private string _roomId;
        private int _assignedPlayerId;
        private int _maxPlayers;
        private bool _isStarted;

        public LobbyClientService(
            IMatchClientTransport transport,
            IMatchProtocolCodec codec,
            string displayName,
            string avatarResourcePath = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Display name is required.",
                    nameof(displayName));
            }

            _displayName = displayName;
            _avatarResourcePath = avatarResourcePath ?? string.Empty;
        }

        public event Action Connected;

        public event Action Disconnected;

        public event Action<string> ConnectionFailed;

        public event Action<LobbyAssignmentEnvelope> Assigned;

        public event Action<LobbySnapshot> LobbyChanged;

        public event Action<MatchStartEnvelope> MatchStarted;

        public event Action<string> ProtocolError;

        public bool IsConnected => _transport.IsConnected;

        public bool IsAssigned => _assignedPlayerId > 0;

        public int AssignedPlayerId => _assignedPlayerId;

        public int MaxPlayers => _maxPlayers;

        public string RoomId => _roomId;

        public LobbySnapshot LatestLobbySnapshot { get; private set; }

        public MatchStartEnvelope LastMatchStart { get; private set; }

        public void Connect(string address, ushort port)
        {
            if (_isStarted)
            {
                throw new InvalidOperationException("Lobby client is already started.");
            }

            _transport.Connected += OnConnected;
            _transport.Disconnected += OnDisconnected;
            _transport.ConnectionFailed += OnConnectionFailed;
            _transport.MessageReceived += OnMessageReceived;
            _transport.Connect(address, port);
            _isStarted = true;
        }

        public void Pump()
        {
            _transport.Pump();
        }

        public bool TrySetReady(bool isReady)
        {
            return TrySendCommand(LobbyCommand.SetReady(isReady));
        }

        public bool TrySetDisplayName(string displayName)
        {
            return TrySendCommand(LobbyCommand.SetDisplayName(displayName));
        }

        public bool TrySetAvatarResourcePath(string avatarResourcePath)
        {
            return TrySendCommand(LobbyCommand.SetAvatar(avatarResourcePath));
        }

        public void Disconnect()
        {
            if (!_isStarted)
            {
                return;
            }

            UnsubscribeTransport();
            _transport.Disconnect();
            ResetAssignment();
            _isStarted = false;
        }

        public void Dispose()
        {
            if (_isStarted)
            {
                UnsubscribeTransport();
                _isStarted = false;
            }

            _transport.Dispose();
        }

        private void UnsubscribeTransport()
        {
            _transport.Connected -= OnConnected;
            _transport.Disconnected -= OnDisconnected;
            _transport.ConnectionFailed -= OnConnectionFailed;
            _transport.MessageReceived -= OnMessageReceived;
        }

        private bool TrySendCommand(LobbyCommand command)
        {
            if (!IsAssigned)
            {
                return false;
            }

            // Lobby 上行也带单调递增序号，Host 用它拒绝重复或乱序命令。
            LobbyCommandEnvelope envelope = new LobbyCommandEnvelope(
                NetworkProtocol.CurrentVersion,
                _roomId,
                _assignedPlayerId,
                _nextCommandSequence,
                command);
            _nextCommandSequence++;
            _transport.Send(_codec.EncodeLobbyCommand(envelope));
            return true;
        }

        private void OnConnected()
        {
            Connected?.Invoke();
        }

        private void OnDisconnected()
        {
            UnsubscribeTransport();
            ResetAssignment();
            _isStarted = false;
            Disconnected?.Invoke();
        }

        private void OnConnectionFailed(string message)
        {
            UnsubscribeTransport();
            ResetAssignment();
            _isStarted = false;
            ConnectionFailed?.Invoke(message);
        }

        private void OnMessageReceived(byte[] payload)
        {
            try
            {
                NetworkMessageType messageType = _codec.GetMessageType(payload);
                switch (messageType)
                {
                    case NetworkMessageType.LobbyAssignment:
                        ApplyAssignment(
                            _codec.DecodeLobbyAssignment(payload));
                        break;
                    case NetworkMessageType.LobbySnapshot:
                        ApplyLobbySnapshot(
                            _codec.DecodeLobbySnapshot(payload));
                        break;
                    case NetworkMessageType.MatchStart:
                        ApplyMatchStart(
                            _codec.DecodeMatchStart(payload));
                        break;
                    default:
                        ProtocolError?.Invoke(
                            "Lobby client received unexpected message: "
                            + messageType);
                        break;
                }
            }
            catch (Exception exception)
            {
                ProtocolError?.Invoke(exception.Message);
            }
        }

        private void ApplyAssignment(LobbyAssignmentEnvelope envelope)
        {
            if (!NetworkProtocol.IsSupported(envelope.ProtocolVersion))
            {
                ProtocolError?.Invoke("Unsupported lobby assignment protocol.");
                return;
            }

            _roomId = envelope.RoomId;
            _assignedPlayerId = envelope.AssignedPlayerId;
            _maxPlayers = envelope.MaxPlayers;
            Assigned?.Invoke(envelope);

            TrySetDisplayName(_displayName);
            if (!string.IsNullOrWhiteSpace(_avatarResourcePath))
            {
                TrySetAvatarResourcePath(_avatarResourcePath);
            }
        }

        private void ApplyMatchStart(MatchStartEnvelope envelope)
        {
            if (!NetworkProtocol.IsSupported(envelope.ProtocolVersion))
            {
                ProtocolError?.Invoke("Unsupported match-start protocol.");
                return;
            }

            if (!IsAssigned)
            {
                ProtocolError?.Invoke("Match start arrived before assignment.");
                return;
            }

            if (!string.Equals(envelope.RoomId, _roomId, StringComparison.Ordinal))
            {
                ProtocolError?.Invoke("Match start was sent to the wrong room.");
                return;
            }

            if (_assignedPlayerId > envelope.PlayerCount)
            {
                ProtocolError?.Invoke(
                    "Assigned player is not included in the starting match.");
                return;
            }

            LastMatchStart = envelope;
            MatchStarted?.Invoke(envelope);
        }

        private void ApplyLobbySnapshot(LobbySnapshot snapshot)
        {
            LatestLobbySnapshot = snapshot;
            LobbyChanged?.Invoke(snapshot);
        }

        private void ResetAssignment()
        {
            _roomId = null;
            _assignedPlayerId = 0;
            _maxPlayers = 0;
            LatestLobbySnapshot = null;
            LastMatchStart = null;
            _nextCommandSequence = 1;
        }
    }
}
