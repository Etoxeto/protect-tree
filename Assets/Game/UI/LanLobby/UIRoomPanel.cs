using System;
using ProtectTree.Core.Network;
using ProtectTree.Runtime.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree
{
    public class UIRoomPanel : MonoBehaviour
    {
        [SerializeField] private UIPlayerInfo[] players = new UIPlayerInfo[4];
        [SerializeField] private Button buttonBackToMenu;
        [SerializeField] private Button buttonStartGame;
        [SerializeField] private Button buttonReady;

        private TextMeshProUGUI _readyText;
        private TextMeshProUGUI _startText;

        public event Action BackToMenuRequested;

        public event Action ReadyRequested;

        public event Action StartGameRequested;

        private void Awake()
        {
            _readyText = buttonReady != null
                ? buttonReady.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;
            _startText = buttonStartGame != null
                ? buttonStartGame.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;
        }

        private void OnEnable()
        {
            buttonBackToMenu?.onClick.AddListener(RequestBackToMenu);
            buttonReady?.onClick.AddListener(RequestReady);
            buttonStartGame?.onClick.AddListener(RequestStartGame);
        }

        private void OnDisable()
        {
            buttonBackToMenu?.onClick.RemoveListener(RequestBackToMenu);
            buttonReady?.onClick.RemoveListener(RequestReady);
            buttonStartGame?.onClick.RemoveListener(RequestStartGame);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            HidePlayers();
            gameObject.SetActive(false);
        }

        public void Render(
            LobbySnapshot snapshot,
            int localPlayerId,
            bool isHost,
            Sprite avatarSprite)
        {
            if (snapshot == null)
            {
                HidePlayers();
                SetReadyButton(false, "连接中");
                SetStartButton(false, "等待");
                return;
            }

            int index = 0;
            foreach (LobbyPlayerSnapshot player in snapshot.Players)
            {
                if (index >= players.Length)
                {
                    break;
                }

                players[index]?.RenderLobby(
                    player,
                    ResolveAvatar(player, avatarSprite),
                    player.PlayerId == localPlayerId,
                    null);
                index++;
            }

            for (; index < players.Length; index++)
            {
                players[index]?.Hide();
            }

            LobbyPlayerSnapshot localPlayer =
                FindPlayer(snapshot, localPlayerId);
            bool isReady = localPlayer != null && localPlayer.IsReady;
            SetReadyButton(localPlayer != null, isReady ? "取消准备" : "准备");
            SetStartButton(isHost && snapshot.CanStart, "开始");
        }

        private void HidePlayers()
        {
            if (players == null)
            {
                return;
            }

            foreach (UIPlayerInfo player in players)
            {
                player?.Hide();
            }
        }

        private void SetReadyButton(bool isInteractable, string label)
        {
            if (buttonReady != null)
            {
                buttonReady.interactable = isInteractable;
            }

            if (_readyText != null)
            {
                _readyText.text = label;
            }
        }

        private void SetStartButton(bool isInteractable, string label)
        {
            if (buttonStartGame != null)
            {
                buttonStartGame.interactable = isInteractable;
            }

            if (_startText != null)
            {
                _startText.text = label;
            }
        }

        private void RequestBackToMenu()
        {
            BackToMenuRequested?.Invoke();
        }

        private void RequestReady()
        {
            ReadyRequested?.Invoke();
        }

        private void RequestStartGame()
        {
            StartGameRequested?.Invoke();
        }

        private static LobbyPlayerSnapshot FindPlayer(
            LobbySnapshot snapshot,
            int playerId)
        {
            foreach (LobbyPlayerSnapshot player in snapshot.Players)
            {
                if (player.PlayerId == playerId)
                {
                    return player;
                }
            }

            return null;
        }

        private static Sprite ResolveAvatar(
            LobbyPlayerSnapshot player,
            Sprite fallbackAvatar)
        {
            if (player == null
                || string.IsNullOrWhiteSpace(player.AvatarResourcePath))
            {
                return fallbackAvatar;
            }

            return UIResourceLoader.LoadSprite(player.AvatarResourcePath)
                ?? fallbackAvatar;
        }
    }
}
