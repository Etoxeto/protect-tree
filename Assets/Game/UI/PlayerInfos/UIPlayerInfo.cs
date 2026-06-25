using System;
using ProtectTree.Core.Match;
using ProtectTree.Core.Network;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProtectTree.Runtime.UI
{
    public sealed class UIPlayerInfo : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image avatar;
        [SerializeField] private TextMeshProUGUI playerHp;
        [SerializeField] private Image personalBackground;
        [SerializeField] private TextMeshProUGUI playerName;
        [SerializeField] private Image playerReadyFx;  // 准备时显示，未准备时隐藏，仅在房间界面显示
        [SerializeField] private Image playerReadyFxNameBar; // 未准备时为黑色，准备时颜色为#FFA800，仅在房间界面显示

        [SerializeField] private GameObject playerConfirmFx; // 两个用途：1.备战回合，玩家准备时显示。2.战斗回合，玩家杀完当前回合所有敌人时显示

        private int _playerId;
        private Action<int> _onClicked;

        public void Render(
            PlayerSnapshot player,
            Sprite avatarSprite,
            bool isObserved,
            bool showConfirmFx,
            Action<int> onClicked)
        {
            if (player == null)
            {
                Hide();
                return;
            }

            _playerId = player.PlayerId;
            _onClicked = onClicked;
            gameObject.SetActive(true);

            if (avatar != null && avatarSprite != null)
            {
                avatar.sprite = avatarSprite;
                avatar.enabled = true;
                avatar.color = isObserved ? Color.white : new Color(1f, 1f, 1f, 0.65f);
            }

            if (personalBackground != null)
            {
                personalBackground.color = isObserved
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.65f);
            }

            if (playerHp != null)
            {
                playerHp.text =
                    $"{player.Health}";
            }

            if (playerName != null)
            {
                playerName.text = $"P{player.PlayerId}";
            }

            SetReadyFx(false);
            SetNameBarReady(false);
            SetConfirmFx(showConfirmFx);
        }

        public void RenderLobby(
            LobbyPlayerSnapshot player,
            Sprite avatarSprite,
            bool isLocalPlayer,
            Action<int> onClicked)
        {
            if (player == null)
            {
                Hide();
                return;
            }

            _playerId = player.PlayerId;
            _onClicked = onClicked;
            gameObject.SetActive(true);

            if (avatar != null && avatarSprite != null)
            {
                avatar.sprite = avatarSprite;
                avatar.enabled = true;
                avatar.color = isLocalPlayer
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.75f);
            }

            if (playerName != null)
            {
                string hostTag = player.IsHost ? " [Host]" : string.Empty;
                string localTag = isLocalPlayer ? " [You]" : string.Empty;
                playerName.text = player.DisplayName + hostTag + localTag;
            }

            if (playerHp != null)
            {
                playerHp.text = player.IsReady ? "Ready" : "Waiting";
            }

            if (personalBackground != null)
            {
                if (avatarSprite != null)
                {
                    personalBackground.sprite = avatarSprite;
                    personalBackground.enabled = true;
                    personalBackground.preserveAspect = true;
                }

                personalBackground.color = isLocalPlayer
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.8f);
            }

            SetReadyFx(player.IsReady);
            SetNameBarReady(player.IsReady);
            SetConfirmFx(false);
        }

        public void Hide()
        {
            _playerId = 0;
            _onClicked = null;
            SetConfirmFx(false);
            gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_playerId <= 0)
            {
                return;
            }

            _onClicked?.Invoke(_playerId);
        }

        private void SetReadyFx(bool isReady)
        {
            if (playerReadyFx != null)
            {
                playerReadyFx.gameObject.SetActive(isReady);
            }
        }

        private void SetNameBarReady(bool isReady)
        {
            if (playerReadyFxNameBar != null)
            {
                playerReadyFxNameBar.color = isReady
                    ? new Color(1f, 0.65882355f, 0f, playerReadyFxNameBar.color.a)
                    : new Color(0f, 0f, 0f, playerReadyFxNameBar.color.a);
            }
        }

        private void SetConfirmFx(bool isConfirmed)
        {
            if (playerConfirmFx != null)
            {
                playerConfirmFx.SetActive(isConfirmed);
            }
        }
    }
}
