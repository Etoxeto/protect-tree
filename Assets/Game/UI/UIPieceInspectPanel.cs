using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class UIPieceInspectPanel : MatchSceneFeature
    {
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private Image characterImage;
        [SerializeField] private Image characterTypeIcon;
        [SerializeField] private TextMeshProUGUI characterNameText;
        [SerializeField] private TextMeshProUGUI characterQualityText;
        [SerializeField] private TextMeshProUGUI characterLevelText;
        [SerializeField] private TextMeshProUGUI characterPropertiesText;
        [SerializeField] private UISynergy[] synergies;
        [SerializeField] private TextMeshProUGUI HpText;
        [SerializeField] private Slider HpSlider;
        [SerializeField] private TextMeshProUGUI featureDescription;

        private ShopOfferSnapshot _manualShopOfferPreview;
        private bool _hasManualShopOfferPreview;

        public override int RefreshOrder => 100;

        public void PreviewShopOffer(ShopOfferSnapshot offer)
        {
            if (offer == null || offer.IsSold)
            {
                ClearShopOfferPreview();
                return;
            }

            _manualShopOfferPreview = offer;
            _hasManualShopOfferPreview = true;
            Render(offer);
        }

        public void ClearShopOfferPreview()
        {
            if (!_hasManualShopOfferPreview)
            {
                return;
            }

            _manualShopOfferPreview = null;
            _hasManualShopOfferPreview = false;
            Clear();
        }

        public override void Refresh(MatchSceneContext context)
        {
            if (context == null
                || context.Flow == null
                || context.Flow.IsFinished)
            {
                _manualShopOfferPreview = null;
                _hasManualShopOfferPreview = false;
                Clear();
                return;
            }

            ShopOfferSnapshot selectedOffer =
                (_hasManualShopOfferPreview ? _manualShopOfferPreview : null)
                ?? context.SelectedShopOfferPreview
                ?? FindOffer(context.Shop, context.SelectedShopSlotIndex);
            if (selectedOffer != null && !selectedOffer.IsSold)
            {
                Render(selectedOffer);
                return;
            }

            if (context.IsPieceInspectBlocked)
            {
                Clear();
                return;
            }

            PieceSnapshot selectedPiece =
                FindPiece(context.Pieces, context.SelectedPieceInstanceId);
            if (selectedPiece != null
                && selectedPiece.OwnerPlayerId == context.LocalPlayerId)
            {
                Render(selectedPiece);
                return;
            }

            Clear();
        }

        public override void OnRuntimeUnavailable()
        {
            Clear();
        }

        public RectTransform ObstructionRectTransform
        {
            get
            {
                if (contentRoot != null
                    && contentRoot.transform is RectTransform contentRect)
                {
                    return contentRect;
                }

                return transform as RectTransform;
            }
        }

        private void Render(PieceSnapshot piece)
        {
            SetContentVisible(true);
            SetImageSprite(characterImage, $"UI/Characters/{piece.Portrait}");
            SetImageSprite(
                characterTypeIcon,
                $"UI/Icons/CharacterType/{piece.ClassId}");
            SetText(characterNameText, piece.DisplayName);
            SetText(characterQualityText, ToRomanNumeral(piece.Rarity));
            SetText(characterLevelText, $"Lv.{piece.Level}");
            SetText(characterPropertiesText, BuildPropertiesText(piece));
            SetHealth(piece.Health, piece.MaxHealth);
            SetText(featureDescription, piece.FeatureDescription);
            RenderSynergies(piece.Synergies);
        }

        private void Render(ShopOfferSnapshot offer)
        {
            SetContentVisible(true);
            SetImageSprite(characterImage, $"UI/Characters/{offer.Portrait}");
            SetImageSprite(
                characterTypeIcon,
                $"UI/Icons/CharacterType/{offer.ClassId}");
            SetText(characterNameText, offer.DisplayName);
            SetText(characterQualityText, ToRomanNumeral(offer.Rarity));
            SetText(characterLevelText, "Lv.1");
            SetText(characterPropertiesText, BuildPropertiesText(offer));
            SetHealth(offer.MaxHealth, offer.MaxHealth);
            SetText(featureDescription, offer.FeatureDescription);
            RenderSynergies(offer.Synergies);
        }

        private void Clear()
        {
            SetContentVisible(false);
            SetImageSprite(characterImage, null);
            SetImageSprite(characterTypeIcon, null);
            SetText(characterNameText, string.Empty);
            SetText(characterQualityText, string.Empty);
            SetText(characterLevelText, string.Empty);
            SetText(characterPropertiesText, string.Empty);
            SetHealth(0, 0);
            SetText(featureDescription, string.Empty);

            if (synergies == null)
            {
                return;
            }

            foreach (UISynergy synergy in synergies)
            {
                synergy?.Hide();
            }
        }

        private void RenderSynergies(IReadOnlyList<ShopSynergySnapshot> pieceSynergies)
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

                if (pieceSynergies != null && index < pieceSynergies.Count)
                {
                    synergies[index].Render(pieceSynergies[index]);
                }
                else
                {
                    synergies[index].Hide();
                }
            }
        }

        private void SetContentVisible(bool isVisible)
        {
            if (contentRoot != null)
            {
                contentRoot.SetActive(isVisible);
                return;
            }

            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(isVisible);
            }
        }

        private static string BuildPropertiesText(PieceSnapshot piece)
        {
            string damage = piece.Damage == piece.BaseDamage
                ? piece.Damage.ToString()
                : $"{piece.Damage} ({piece.BaseDamage}+{piece.Damage - piece.BaseDamage})";
            return $"攻击 {damage}\n"
                + $"阻挡 {piece.BlockedEnemyInstanceIds.Count}/{piece.MaxBlockCount}\n"
                + $"攻速 {piece.AttackIntervalSeconds:0.0}s\n"
                + $"售价 {piece.SellValue}";
        }

        private static string BuildPropertiesText(ShopOfferSnapshot offer)
        {
            return $"攻击 {offer.Damage}\n"
                + $"阻挡 {offer.MaxBlockCount}\n"
                + $"攻速 {offer.AttackIntervalSeconds:0.0}s\n"
                + $"费用 {offer.Cost}";
        }

        private static PieceSnapshot FindPiece(
            PieceRosterSnapshot snapshot,
            int pieceInstanceId)
        {
            if (snapshot == null || pieceInstanceId <= 0)
            {
                return null;
            }

            foreach (PieceSnapshot piece in snapshot.Pieces)
            {
                if (piece.InstanceId == pieceInstanceId)
                {
                    return piece;
                }
            }

            return null;
        }

        private static ShopOfferSnapshot FindOffer(
            ShopSnapshot snapshot,
            int slotIndex)
        {
            if (snapshot == null || slotIndex <= 0)
            {
                return null;
            }

            foreach (ShopOfferSnapshot offer in snapshot.Offers)
            {
                if (offer.SlotIndex == slotIndex)
                {
                    return offer;
                }
            }

            return null;
        }

        private void SetHealth(int currentHealth, int maxHealth)
        {
            SetText(HpText, maxHealth > 0
                ? $"生命值：{currentHealth}/{maxHealth}"
                : string.Empty);

            if (HpSlider == null)
            {
                return;
            }

            HpSlider.maxValue = Mathf.Max(1, maxHealth);
            HpSlider.value = Mathf.Clamp(currentHealth, 0, HpSlider.maxValue);
        }

        private static void SetImageSprite(Image image, string resourcePath)
        {
            if (image == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(resourcePath))
            {
                image.sprite = null;
                image.enabled = false;
                return;
            }

            image.sprite = UIResourceLoader.LoadSprite(resourcePath);
            image.enabled = image.sprite != null;
            image.color = Color.white;
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
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
