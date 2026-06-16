using System;
using ProtectTree.Core.Match;
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

        private int _playerId;
        private Action<int> _onClicked;

        public void Render(
            PlayerSnapshot player,
            Sprite avatarSprite,
            bool isObserved,
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

            if (playerHp != null)
            {
                string prefix = isObserved ? "[P" : "P";
                string suffix = isObserved ? "]" : string.Empty;
                playerHp.text =
                    $"{prefix}{player.PlayerId}{suffix} {player.Health}/{player.MaxHealth}";
            }
        }

        public void Hide()
        {
            _playerId = 0;
            _onClicked = null;
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
    }
}
