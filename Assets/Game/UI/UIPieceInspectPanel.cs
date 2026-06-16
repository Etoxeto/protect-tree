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
        [SerializeField] private Image characterImage;    // 角色立绘
        [SerializeField] private Image characterTypeIcon;   // 角色类型图标
        [SerializeField] private TextMeshProUGUI characterNameText;    // 角色名称文本
        [SerializeField] private TextMeshProUGUI characterQualityText;  // 角色稀有度文本 (与UIShopItem的quality对应，都写罗马数字)
        [SerializeField] private TextMeshProUGUI characterLevelText;   // 角色等级文本
        [SerializeField] private TextMeshProUGUI characterPropertiesText; // 角色属性文本， 分多行写
        [SerializeField] private UISynergy[] synergies; // 角色羁绊信息

        public override void Refresh(MatchSceneContext context)
        {
            if (context == null
                || context.IsPieceInspectBlocked
                || context.Flow == null
                || context.Flow.IsFinished)
            {
                Clear();
                return;
            }

            PieceSnapshot selectedPiece =
                FindPiece(context.Pieces, context.SelectedPieceInstanceId);
            if (selectedPiece == null
                || selectedPiece.OwnerPlayerId != context.LocalPlayerId)
            {
                Clear();
                return;
            }

            Render(selectedPiece);
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
                $"UI/Icons/CharacterType/role_icon_{piece.ClassId}_transparent");
            SetText(characterNameText, piece.DisplayName);
            SetText(characterQualityText, ToRomanNumeral(piece.Rarity));
            SetText(characterLevelText, $"Lv.{piece.Level}");
            SetText(characterPropertiesText, BuildPropertiesText(piece));
            RenderSynergies(piece);
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

            if (synergies == null)
            {
                return;
            }

            foreach (UISynergy synergy in synergies)
            {
                synergy?.Hide();
            }
        }

        private void RenderSynergies(PieceSnapshot piece)
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

                if (index < piece.Synergies.Count)
                {
                    synergies[index].Render(piece.Synergies[index]);
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

            // 脚本挂在面板根节点上，不能禁用自身 GameObject，否则无法继续接收选中刷新。
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
            return $"生命 {piece.Health}/{piece.MaxHealth}\n"
                + $"攻击 {damage}\n"
                + $"阻挡 {piece.BlockedEnemyInstanceIds.Count}/{piece.MaxBlockCount}\n"
                + $"攻速 {piece.AttackIntervalSeconds:0.0}s\n"
                + $"售价 {piece.SellValue}";
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
