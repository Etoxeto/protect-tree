using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using ProtectTree.Core.Network;
using ProtectTree.Runtime;
using ProtectTree.Runtime.Network;
using ProtectTree.Runtime.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProtectTree
{
    public class UILanLobby : MonoBehaviour
    {
        private const int HostPlayerId = 1;

        [SerializeField] private UIMenuPanel menu;
        [SerializeField] private UIRoomPanel room;
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private string mainMenuSceneName = "Menu";
        [SerializeField] private string matchSceneName = "SampleScene";
        [SerializeField] private ushort matchPortOffset = 1;
        [SerializeField] private float hostMatchStartDelaySeconds = 0.25f;
        [SerializeField] private UIMessageBox messageBoxPrefab;
        [SerializeField] private string defaultAvatarResourcePath =
            PlayerProfileOptions.DefaultAvatarResourcePath;

        private LobbyHostService _hostService;
        private LobbyClientService _clientService;
        private LobbySnapshot _latestSnapshot;
        private Sprite _defaultAvatar;
        private int _localPlayerId;
        private bool _isHost;
        private bool _isEnteringMatch;
        private Coroutine _enterMatchCoroutine;
        private string _matchHostAddress;
        private ushort _lobbyPort;
        private UIMessageBox _messageBox;

        private void Awake()
        {
            _defaultAvatar = UIResourceLoader.LoadSprite(
                PlayerProfileOptions.NormalizeAvatarResourcePath(
                    defaultAvatarResourcePath));
            if (_defaultAvatar == null)
            {
                _defaultAvatar = UIResourceLoader.LoadSprite(
                    PlayerProfileOptions.DefaultAvatarResourcePath);
            }

            menu.CreateRequested += CreateRoom;
            menu.JoinRequested += JoinRoom;
            menu.BackToMainMenuRequested += BackToMainMenu;
            room.BackToMenuRequested += LeaveRoom;
            room.ReadyRequested += ToggleReady;
            room.StartGameRequested += RequestStartGame;
            ShowMenu();
        }

        private void Update()
        {
            _hostService?.Pump();
            _clientService?.Pump();
        }

        private void OnDestroy()
        {
            if (menu != null)
            {
                menu.CreateRequested -= CreateRoom;
                menu.JoinRequested -= JoinRoom;
                menu.BackToMainMenuRequested -= BackToMainMenu;
            }

            if (room != null)
            {
                room.BackToMenuRequested -= LeaveRoom;
                room.ReadyRequested -= ToggleReady;
                room.StartGameRequested -= RequestStartGame;
            }

            DisposeServices();
            if (_messageBox != null)
            {
                Destroy(_messageBox.gameObject);
                _messageBox = null;
            }
        }

        private void CreateRoom()
        {
            CancelEnterMatch();
            LanMatchRuntime.ClearActiveSession();
            DisposeServices();
            _isHost = true;
            _localPlayerId = HostPlayerId;
            _lobbyPort = defaultPort;
            _matchHostAddress = GetLocalAddress();

            string roomId = Guid.NewGuid().ToString("N");
            _hostService = new LobbyHostService(
                new TcpMatchHostTransport(),
                new BinaryMatchProtocolCodec(),
                roomId,
                PlayerProfileOptions.PlayerName,
                maxPlayers,
                PlayerProfileOptions.AvatarResourcePath);
            _hostService.LobbyChanged += OnLobbyChanged;
            _hostService.PlayerAssigned += OnPlayerAssigned;
            _hostService.ProtocolError += OnProtocolError;
            try
            {
                _hostService.Start(defaultPort);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[ProtectTree][LAN Lobby] Failed to create room: "
                    + exception.Message,
                    this);
                DisposeServices();
                ShowMenu();
                ShowMessage(
                    "创建房间失败",
                    $"端口 {defaultPort} 可能已被占用。请关闭旧房间或稍后重试。");
                return;
            }

            string inviteCode = _matchHostAddress + ":" + defaultPort;
            menu.SetInviteCode(inviteCode);
            Debug.Log(
                $"[ProtectTree][LAN Lobby] Room created. Invite code: {inviteCode}",
                this);
            ShowRoom(_hostService.LatestLobbySnapshot);
        }

        private void JoinRoom(string inviteCode)
        {
            CancelEnterMatch();
            LanMatchRuntime.ClearActiveSession();
            if (!TryParseInviteCode(
                inviteCode,
                out string address,
                out ushort port))
            {
                Debug.LogWarning(
                    "[ProtectTree][LAN Lobby] Invite code should be IP:Port, for example 192.168.1.23:7777.",
                    this);
                ShowMessage(
                    "加入房间失败",
                    "邀请码格式应为 IP:端口，例如 192.168.1.23:7777。");
                return;
            }

            DisposeServices();
            _isHost = false;
            _localPlayerId = 0;
            _matchHostAddress = address;
            _lobbyPort = port;
            menu.SetInteractable(false);

            _clientService = new LobbyClientService(
                new TcpMatchClientTransport(),
                new BinaryMatchProtocolCodec(),
                PlayerProfileOptions.PlayerName,
                PlayerProfileOptions.AvatarResourcePath);
            _clientService.Connected += OnClientConnected;
            _clientService.Disconnected += OnClientDisconnected;
            _clientService.ConnectionFailed += OnConnectionFailed;
            _clientService.Assigned += OnClientAssigned;
            _clientService.LobbyChanged += OnLobbyChanged;
            _clientService.MatchStarted += OnClientMatchStarted;
            _clientService.ProtocolError += OnProtocolError;
            _clientService.Connect(address, port);

            Debug.Log(
                $"[ProtectTree][LAN Lobby] Connecting to {address}:{port}.",
                this);
            ShowRoom(null);
        }

        private void ToggleReady()
        {
            LobbyPlayerSnapshot localPlayer =
                FindPlayer(_latestSnapshot, _localPlayerId);
            bool nextReady = localPlayer == null || !localPlayer.IsReady;

            if (_isHost)
            {
                _hostService?.SetHostReady(nextReady);
                return;
            }

            if (_clientService == null || !_clientService.TrySetReady(nextReady))
            {
                Debug.LogWarning(
                    "[ProtectTree][LAN Lobby] Cannot change ready state before player assignment.",
                    this);
            }
        }

        private void RequestStartGame()
        {
            if (_isEnteringMatch
                || !_isHost
                || _hostService == null
                || _latestSnapshot == null
                || !_latestSnapshot.CanStart)
            {
                return;
            }

            if (!_hostService.TryStartMatch(out MatchStartEnvelope start))
            {
                return;
            }

            Debug.Log(
                $"[ProtectTree][LAN Lobby] Starting match {start.MatchId} with {start.PlayerCount} players.",
                this);
            BeginEnterMatch(
                start,
                HostPlayerId,
                isHost: true,
                hostMatchStartDelaySeconds);
        }

        private void LeaveRoom()
        {
            Debug.Log("[ProtectTree][LAN Lobby] Leaving room and returning to LAN menu.", this);
            CancelEnterMatch();
            LanMatchRuntime.ClearActiveSession();
            DisposeServices();
            ShowMenu();
        }

        private void BackToMainMenu()
        {
            Debug.Log("[ProtectTree][LAN Lobby] Returning to main menu and clearing LAN session.", this);
            CancelEnterMatch();
            LanMatchRuntime.ClearActiveSession();
            DisposeServices();
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }

        private void OnLobbyChanged(LobbySnapshot snapshot)
        {
            ShowRoom(snapshot);
        }

        private void OnPlayerAssigned(int connectionId, int playerId)
        {
            Debug.Log(
                $"[ProtectTree][LAN Lobby] Connection {connectionId} assigned player {playerId}.",
                this);
        }

        private void OnClientConnected()
        {
            Debug.Log("[ProtectTree][LAN Lobby] Connected to room host.", this);
        }

        private void OnClientAssigned(LobbyAssignmentEnvelope assignment)
        {
            _localPlayerId = assignment.AssignedPlayerId;
            Debug.Log(
                $"[ProtectTree][LAN Lobby] Assigned local player {_localPlayerId}.",
                this);
        }

        private void OnClientMatchStarted(MatchStartEnvelope start)
        {
            Debug.Log(
                $"[ProtectTree][LAN Lobby] Match {start.MatchId} started with {start.PlayerCount} players.",
                this);
            BeginEnterMatch(
                start,
                _localPlayerId,
                isHost: false,
                delaySeconds: 0f);
        }

        private void OnClientDisconnected()
        {
            if (_isEnteringMatch)
            {
                return;
            }

            Debug.LogWarning(
                "[ProtectTree][LAN Lobby] Disconnected from room host.",
                this);
            DisposeServices();
            ShowMenu();
            ShowMessage(
                "房间连接中断",
                "已与房主断开连接，返回联机菜单。");
        }

        private void OnConnectionFailed(string message)
        {
            Debug.LogWarning(
                $"[ProtectTree][LAN Lobby] Connection failed: {message}",
                this);
            DisposeServices();
            ShowMenu();
            ShowMessage(
                "加入房间失败",
                "无法连接到房主，请确认邀请码、网络和房主窗口是否仍在运行。");
        }

        private void OnProtocolError(string message)
        {
            Debug.LogWarning(
                $"[ProtectTree][LAN Lobby] Protocol warning: {message}",
                this);
        }

        private void ShowMenu()
        {
            _latestSnapshot = null;
            _localPlayerId = 0;
            _isHost = false;
            _isEnteringMatch = false;
            menu.Show();
            room.Hide();
        }

        private void ShowRoom(LobbySnapshot snapshot)
        {
            _latestSnapshot = snapshot;
            menu.Hide();
            room.Show();
            room.Render(snapshot, _localPlayerId, _isHost, _defaultAvatar);
        }

        private void DisposeServices()
        {
            if (_hostService != null)
            {
                _hostService.LobbyChanged -= OnLobbyChanged;
                _hostService.PlayerAssigned -= OnPlayerAssigned;
                _hostService.ProtocolError -= OnProtocolError;
                _hostService.Dispose();
                _hostService = null;
            }

            if (_clientService != null)
            {
                _clientService.Connected -= OnClientConnected;
                _clientService.Disconnected -= OnClientDisconnected;
                _clientService.ConnectionFailed -= OnConnectionFailed;
                _clientService.Assigned -= OnClientAssigned;
                _clientService.LobbyChanged -= OnLobbyChanged;
                _clientService.MatchStarted -= OnClientMatchStarted;
                _clientService.ProtocolError -= OnProtocolError;
                _clientService.Dispose();
                _clientService = null;
            }
        }

        private void ShowMessage(string title, string message)
        {
            UIMessageBox box = EnsureMessageBox();
            if (box == null)
            {
                return;
            }

            box.Show(
                title,
                message,
                null,
                null,
                null,
                null,
                "关闭",
                box.Hide);
        }

        private UIMessageBox EnsureMessageBox()
        {
            if (_messageBox != null)
            {
                return _messageBox;
            }

            UIMessageBox prefab = messageBoxPrefab != null
                ? messageBoxPrefab
                : Resources.Load<UIMessageBox>("Prefabs/UIMessageBox");
            if (prefab == null)
            {
                Debug.LogWarning(
                    "UIMessageBox prefab was not found under Resources/Prefabs.",
                    this);
                return null;
            }

            _messageBox = Instantiate(prefab);
            _messageBox.Hide();
            return _messageBox;
        }

        private void BeginEnterMatch(
            MatchStartEnvelope start,
            int localPlayerId,
            bool isHost,
            float delaySeconds)
        {
            if (_isEnteringMatch)
            {
                return;
            }

            _isEnteringMatch = true;
            _enterMatchCoroutine = StartCoroutine(EnterMatchAfterDelay(
                start,
                localPlayerId,
                isHost,
                delaySeconds));
        }

        private IEnumerator EnterMatchAfterDelay(
            MatchStartEnvelope start,
            int localPlayerId,
            bool isHost,
            float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            if (isHost)
            {
                LanMatchRuntime.BeginHostSession(
                    start.RoomId,
                    start.MatchId,
                    start.PlayerCount,
                    localPlayerId,
                    _matchHostAddress,
                    GetMatchPort(_lobbyPort),
                    _hostService.LastMatchJoinTokens,
                    BuildAvatarResourcePaths(_latestSnapshot, localPlayerId));
            }
            else
            {
                LanMatchRuntime.BeginClientSession(
                    start.RoomId,
                    start.MatchId,
                    start.PlayerCount,
                    localPlayerId,
                    _matchHostAddress,
                    GetMatchPort(_lobbyPort),
                    start.JoinToken,
                    BuildAvatarResourcePaths(_latestSnapshot, localPlayerId));
            }

            MatchStartupOptions.UseLocalMultiplayer(start.PlayerCount, localPlayerId);
            DisposeServices();
            SceneManager.LoadScene(matchSceneName, LoadSceneMode.Single);
        }

        private void CancelEnterMatch()
        {
            if (_enterMatchCoroutine != null)
            {
                StopCoroutine(_enterMatchCoroutine);
                _enterMatchCoroutine = null;
            }

            _isEnteringMatch = false;
        }

        private ushort GetMatchPort(ushort lobbyPort)
        {
            int rawPort = lobbyPort + matchPortOffset;
            if (rawPort > ushort.MaxValue)
            {
                Debug.LogWarning(
                    "[ProtectTree][LAN Lobby] Match port overflow; reusing lobby port after lobby transport is disposed.",
                    this);
                return lobbyPort;
            }

            return (ushort)rawPort;
        }

        private static IReadOnlyDictionary<int, string> BuildAvatarResourcePaths(
            LobbySnapshot snapshot,
            int localPlayerId)
        {
            Dictionary<int, string> result = new Dictionary<int, string>();
            if (snapshot != null)
            {
                foreach (LobbyPlayerSnapshot player in snapshot.Players)
                {
                    if (!string.IsNullOrWhiteSpace(player.AvatarResourcePath))
                    {
                        result[player.PlayerId] = player.AvatarResourcePath;
                    }
                }
            }

            if (localPlayerId > 0 && !result.ContainsKey(localPlayerId))
            {
                result[localPlayerId] = PlayerProfileOptions.AvatarResourcePath;
            }

            return result;
        }

        private static bool TryParseInviteCode(
            string inviteCode,
            out string address,
            out ushort port)
        {
            address = null;
            port = 0;

            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                address = "127.0.0.1";
                port = 7777;
                return true;
            }

            string[] parts = inviteCode.Trim().Split(':');
            if (parts.Length == 1)
            {
                address = parts[0].Trim();
                port = 7777;
                return !string.IsNullOrWhiteSpace(address);
            }

            if (parts.Length != 2
                || string.IsNullOrWhiteSpace(parts[0])
                || !ushort.TryParse(parts[1], out port))
            {
                return false;
            }

            address = parts[0].Trim();
            return true;
        }

        private static string GetLocalAddress()
        {
            try
            {
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress address in host.AddressList)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(address))
                    {
                        return address.ToString();
                    }
                }
            }
            catch (Exception)
            {
            }

            return "127.0.0.1";
        }

        private static LobbyPlayerSnapshot FindPlayer(
            LobbySnapshot snapshot,
            int playerId)
        {
            if (snapshot == null)
            {
                return null;
            }

            foreach (LobbyPlayerSnapshot player in snapshot.Players)
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
