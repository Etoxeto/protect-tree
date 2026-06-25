using System;
using ProtectTree.Core.Match;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProtectTree.Runtime.UI
{
    public sealed class UISynergiesBarItem : MonoBehaviour, IPointerClickHandler
    {
        private static readonly Color32 InactiveBackground =
            new Color32(0x59, 0x59, 0x59, 0xFF);
        private static readonly Color32 ActiveBackground =
            new Color32(0x51, 0xA6, 0x8B, 0xFF);
        private static readonly Color32 ProgressColor =
            new Color32(0x6B, 0xDA, 0xB7, 0xFF);
        private static readonly Color32 MissingColor =
            new Color32(0xAD, 0xAD, 0xAD, 0xFF);

        [SerializeField] private Image bg;
        [SerializeField] private Image circleL;
        [SerializeField] private Image circleR;
        [SerializeField] private TextMeshProUGUI synergyName;
        [SerializeField] private Image synergyIcon;
        [SerializeField] private TextMeshProUGUI effectStrength;

        private Action<UISynergiesBarItem, SynergyProgressSnapshot> _clicked;

        public SynergyProgressSnapshot CurrentSynergy { get; private set; }

        public void Initialize(
            Action<UISynergiesBarItem, SynergyProgressSnapshot> clicked)
        {
            _clicked = clicked;
        }

        public void Render(SynergyProgressSnapshot synergy)
        {
            if (synergy == null)
            {
                Hide();
                return;
            }

            CurrentSynergy = synergy;
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

            if (effectStrength != null)
            {
                effectStrength.text = synergy.LayerCount > 0
                    ? synergy.LayerCount.ToString()
                    : string.Empty;
            }
        }

        public void Hide()
        {
            CurrentSynergy = null;
            gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (CurrentSynergy != null)
            {
                _clicked?.Invoke(this, CurrentSynergy);
            }
        }
    }
}
