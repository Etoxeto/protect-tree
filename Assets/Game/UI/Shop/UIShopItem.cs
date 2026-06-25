using System;
using ProtectTree.Core.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree.Runtime.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class UIShopItem : MonoBehaviour
    {
        private static readonly Color32 Gray = new Color32(160, 160, 160, 255);
        private static readonly Color32 Blue = new Color32(52, 143, 139, 255);
        private static readonly Color32 Gold = new Color32(187, 151, 69, 255);
        private static readonly Color32 Orange = new Color32(190, 97, 28, 255);

        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image characterImage;
        [SerializeField] private Image nameArea;
        [SerializeField] private TextMeshProUGUI characterNameText;
        [SerializeField] private Image characterClassIcon;
        [SerializeField] private Image qualityBackground;
        [SerializeField] private TextMeshProUGUI qualityText;
        [SerializeField] private Image costBackground;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private UISynergy[] synergies;
        [SerializeField] private Image confirmFx;

        private Button _purchaseButton;
        private Action<int> _purchaseRequested;
        private int _slotIndex;
        private bool _purchaseButtonListenerRegistered;

        private void Awake()
        {
            EnsurePurchaseButtonListener();

            if (qualityText == null && qualityBackground != null)
            {
                qualityText = qualityBackground.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            SetConfirmFx(false);
        }

        private void OnDestroy()
        {
            if (_purchaseButton != null && _purchaseButtonListenerRegistered)
            {
                _purchaseButton.onClick.RemoveListener(RequestPurchase);
                _purchaseButtonListenerRegistered = false;
            }
        }

        public void Initialize(Action<int> purchaseRequested)
        {
            _purchaseRequested = purchaseRequested;
            EnsurePurchaseButtonListener();
        }

        public void Render(
            ShopOfferSnapshot offer,
            bool canAfford,
            bool canPurchase,
            bool showConfirmFx = false,
            bool canInspect = true)
        {
            gameObject.SetActive(true);
            _slotIndex = offer.SlotIndex;

            Color32 mainColor = GetMainColor(offer.Rarity);
            Color32 qualityColor = GetQualityColor(offer.Rarity);
            Color32 costColor = canAfford ? Gold : Gray;

            SetImageSprite(
                backgroundImage,
                $"UI/QualityBorder/ui_card_border_{GetMainColorName(offer.Rarity)}_fill_1C1C1C");
            SetImageSprite(
                qualityBackground,
                $"UI/QualityBorder/ui_card_border_{GetQualityColorName(offer.Rarity)}_fill_1C1C1C");
            SetImageSprite(
                costBackground,
                $"UI/QualityBorder/ui_card_border_{(canAfford ? "gold" : "gray")}_fill_1C1C1C");
            SetImageSprite(characterImage, $"UI/Characters/{offer.Portrait}");
            SetImageSprite(
                characterClassIcon,
                $"UI/Icons/CharacterType/{offer.ClassId}");

            if (nameArea != null)
            {
                nameArea.color = mainColor;
            }

            if (characterNameText != null)
            {
                characterNameText.text = offer.DisplayName;
                characterNameText.color = Color.white;
            }

            if (qualityText != null)
            {
                qualityText.text = ToRomanNumeral(offer.Rarity);
                qualityText.color = qualityColor;
            }

            if (costText != null)
            {
                costText.text = offer.Cost.ToString();
                costText.color = costColor;
            }

            RenderSynergies(offer);
            Button purchaseButton = EnsurePurchaseButton();
            if (purchaseButton != null)
            {
                purchaseButton.interactable = canInspect;
            }

            SetConfirmFx(showConfirmFx && canPurchase);
        }

        public void RenderReadOnly(PieceSnapshot piece, bool isOnBoard)
        {
            if (piece == null)
            {
                Hide();
                return;
            }

            Render(
                new ShopOfferSnapshot(
                    0,
                    piece.PieceId,
                    piece.DisplayName,
                    piece.Portrait,
                    piece.ClassId,
                    piece.Rarity,
                    piece.SellValue,
                    piece.Synergies,
                    false),
                true,
                false,
                canInspect: false);

            if (!isOnBoard)
            {
                ApplyDisplayTint(Gray);
            }
        }

        public void Hide()
        {
            SetConfirmFx(false);
            gameObject.SetActive(false);
        }

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (!(transform is RectTransform rectTransform))
            {
                return false;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null
                && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            return RectTransformUtility.RectangleContainsScreenPoint(
                rectTransform,
                screenPoint,
                eventCamera);
        }

        private void RequestPurchase()
        {
            _purchaseRequested?.Invoke(_slotIndex);
        }

        private Button EnsurePurchaseButton()
        {
            if (_purchaseButton == null)
            {
                _purchaseButton = GetComponent<Button>();
            }

            return _purchaseButton;
        }

        private void EnsurePurchaseButtonListener()
        {
            Button purchaseButton = EnsurePurchaseButton();
            if (purchaseButton == null || _purchaseButtonListenerRegistered)
            {
                return;
            }

            purchaseButton.onClick.AddListener(RequestPurchase);
            _purchaseButtonListenerRegistered = true;
        }

        private void SetConfirmFx(bool isVisible)
        {
            if (confirmFx != null)
            {
                confirmFx.gameObject.SetActive(isVisible);
            }
        }

        private void RenderSynergies(ShopOfferSnapshot offer)
        {
            if (synergies == null)
            {
                return;
            }

            for (int index = 0; index < synergies.Length; index++)
            {
                if (synergies[index] == null)
                {
                    continue;
                }

                if (index < offer.Synergies.Count)
                {
                    synergies[index].Render(offer.Synergies[index]);
                }
                else
                {
                    synergies[index].Hide();
                }
            }
        }

        private void ApplyDisplayTint(Color color)
        {
            SetImageColor(backgroundImage, color);
            SetImageColor(characterImage, color);
            SetImageColor(nameArea, color);
            SetImageColor(characterClassIcon, color);
            SetImageColor(qualityBackground, color);
            SetImageColor(costBackground, color);
            SetTextColor(characterNameText, color);
            SetTextColor(qualityText, color);
            SetTextColor(costText, color);
        }

        private static void SetImageSprite(Image image, string resourcePath)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = UIResourceLoader.LoadSprite(resourcePath);
            image.color = Color.white;
        }

        private static void SetImageColor(Image image, Color color)
        {
            if (image != null)
            {
                image.color = color;
            }
        }

        private static void SetTextColor(TextMeshProUGUI text, Color color)
        {
            if (text != null)
            {
                text.color = color;
            }
        }

        private static string GetMainColorName(int rarity)
        {
            return rarity >= 6 ? "orange" : rarity == 5 ? "gold" : "blue";
        }

        private static string GetQualityColorName(int rarity)
        {
            return rarity >= 6
                ? "orange"
                : rarity == 5 ? "gold" : rarity >= 3 ? "blue" : "gray";
        }

        private static Color32 GetMainColor(int rarity)
        {
            return rarity >= 6 ? Orange : rarity == 5 ? Gold : Blue;
        }

        private static Color32 GetQualityColor(int rarity)
        {
            return rarity >= 6
                ? Orange
                : rarity == 5 ? Gold : rarity >= 3 ? Blue : Gray;
        }

        private static string ToRomanNumeral(int rarity)
        {
            switch (rarity)
            {
                case 1:
                    return "I";
                case 2:
                    return "II";
                case 3:
                    return "III";
                case 4:
                    return "IV";
                case 5:
                    return "V";
                case 6:
                    return "VI";
                default:
                    return rarity.ToString();
            }
        }
    }
}
