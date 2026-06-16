using ProtectTree.Core.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree.Runtime.UI
{
    public sealed class UISynergiesBarItem : MonoBehaviour
    {
        private static readonly Color32 InactiveBackground =
            new Color32(0x59, 0x59, 0x59, 0xFF);
        private static readonly Color32 ActiveBackground =
            new Color32(0x51, 0xA6, 0x8B, 0xFF);
        private static readonly Color32 ProgressColor =
            new Color32(0x6B, 0xDA, 0xB7, 0xFF);
        private static readonly Color32 MissingColor =
            new Color32(0xAD, 0xAD, 0xAD, 0xFF);

        [SerializeField] private Image bg;       // 羁绊未激活时，颜色为#595959，激活时颜色为#51A68B
        [SerializeField] private Image circleL;  // 战场上有该羁绊角色时，该半边圆环颜色为#6BDAB7，否则为#ADADAD
        [SerializeField] private Image circleR;  // 战场上有该羁绊角色，并且角色数量足够激活羁绊时，该半边圆环颜色为#6BDAB7，否则为#ADADAD
        [SerializeField] private TextMeshProUGUI synergyName;
        [SerializeField] private Image synergyIcon;  // 未激活羁绊时为白色，激活时为黑色

        public void Render(SynergyProgressSnapshot synergy)
        {
            if (synergy == null)
            {
                Hide();
                return;
            }

            bool hasAnyPiece = synergy.UniquePieceCount > 0;
            bool isActive = synergy.IsActive;
            gameObject.SetActive(true);

            if (bg != null)
            {
                bg.color = isActive ? ActiveBackground : InactiveBackground;
            }

            if (circleL != null)
            {
                circleL.color = hasAnyPiece ? ProgressColor : MissingColor;
            }

            if (circleR != null)
            {
                circleR.color = isActive ? ProgressColor : MissingColor;
            }

            if (synergyName != null)
            {
                synergyName.text =
                    $"{synergy.DisplayName} {synergy.UniquePieceCount}/"
                    + $"{synergy.RequiredUniquePieces}";
            }

            if (synergyIcon != null)
            {
                synergyIcon.sprite =
                    UIResourceLoader.LoadSprite(
                        $"UI/Icons/Synergy/{synergy.SynergyId}");
                synergyIcon.color = isActive ? Color.black : Color.white;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
