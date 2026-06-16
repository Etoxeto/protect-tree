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

        private Button _purchaseButton;
        private Action<int> _purchaseRequested;
        private int _slotIndex;

        private void Awake()
        {
            _purchaseButton = GetComponent<Button>();
            _purchaseButton.onClick.AddListener(RequestPurchase);

            if (qualityText == null && qualityBackground != null)
            {
                qualityText = qualityBackground.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private void OnDestroy()
        {
            _purchaseButton?.onClick.RemoveListener(RequestPurchase);
        }

        public void Initialize(Action<int> purchaseRequested)
        {
            _purchaseRequested = purchaseRequested;
        }

        public void Render(ShopOfferSnapshot offer, bool canAfford, bool canPurchase)
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
                $"UI/Icons/CharacterType/role_icon_{offer.ClassId}_transparent");

            if (nameArea != null)
            {
                nameArea.color = mainColor;
            }

            if (characterNameText != null)
            {
                characterNameText.text = offer.DisplayName;
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
            _purchaseButton.interactable = canPurchase;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void RequestPurchase()
        {
            _purchaseRequested?.Invoke(_slotIndex);
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

        private static void SetImageSprite(Image image, string resourcePath)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = UIResourceLoader.LoadSprite(resourcePath);
            image.color = Color.white;
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
