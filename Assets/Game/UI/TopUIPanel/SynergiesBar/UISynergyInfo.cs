using System.Collections.Generic;
using ProtectTree.Core.Match;
using TMPro;
using UnityEngine;

namespace ProtectTree.Runtime.UI
{
    public sealed class UISynergyInfo : MonoBehaviour
    {
        private const string ActiveCurrentColor = "#6BDAB7";
        private const string MissingCurrentColor = "#ADADAD";
        private const string RequiredColor = "#ADADAD";

        public UISynergiesBarItem item;
        public TextMeshProUGUI pieceNum;
        public TextMeshProUGUI SynergyDescription;
        [SerializeField] private Transform pieces;
        [SerializeField] private UIShopItem pieceItem;

        private readonly List<UIShopItem> _pieceItems = new List<UIShopItem>();

        private void Awake()
        {
            if (pieceItem != null)
            {
                pieceItem.Hide();
            }

            Hide();
        }

        public void Render(
            SynergyProgressSnapshot synergy,
            PieceRosterSnapshot roster,
            int playerId)
        {
            if (synergy == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            RenderHeader(synergy);
            RenderPieces(synergy.SynergyId, roster, playerId);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            for (int index = 0; index < _pieceItems.Count; index++)
            {
                _pieceItems[index]?.Hide();
            }
        }

        private void RenderHeader(SynergyProgressSnapshot synergy)
        {
            if (item != null)
            {
                item.Render(synergy);
            }

            if (pieceNum != null)
            {
                string currentColor = synergy.IsActive
                    ? ActiveCurrentColor
                    : MissingCurrentColor;
                pieceNum.text =
                    $"<color={currentColor}>{synergy.UniquePieceCount}</color>"
                    + $"<color={RequiredColor}>/{synergy.RequiredUniquePieces}</color>";
            }

            if (SynergyDescription != null)
            {
                SynergyDescription.text = BuildDescription(synergy);
            }
        }

        private void RenderPieces(
            string synergyId,
            PieceRosterSnapshot roster,
            int playerId)
        {
            IReadOnlyList<PieceSnapshot> uniquePieces =
                CollectUniqueOwnedPieces(synergyId, roster, playerId);

            for (int index = 0; index < uniquePieces.Count; index++)
            {
                UIShopItem view = EnsurePieceItem(index);
                if (view == null)
                {
                    break;
                }

                PieceSnapshot piece = uniquePieces[index];
                view.RenderReadOnly(piece, piece.Location == "Board");
            }

            for (int index = uniquePieces.Count; index < _pieceItems.Count; index++)
            {
                _pieceItems[index]?.Hide();
            }
        }

        private UIShopItem EnsurePieceItem(int index)
        {
            while (_pieceItems.Count <= index)
            {
                if (pieceItem == null || pieces == null)
                {
                    Debug.LogError(
                        "Synergy info piece item prefab and parent must be assigned.",
                        this);
                    return null;
                }

                UIShopItem view = Instantiate(pieceItem, pieces);
                view.Initialize(null);
                _pieceItems.Add(view);
            }

            return _pieceItems[index];
        }

        private static IReadOnlyList<PieceSnapshot> CollectUniqueOwnedPieces(
            string synergyId,
            PieceRosterSnapshot roster,
            int playerId)
        {
            List<PieceSnapshot> result = new List<PieceSnapshot>();
            Dictionary<string, int> indexByPieceId = new Dictionary<string, int>();

            if (roster == null || roster.Pieces == null)
            {
                return result;
            }

            foreach (PieceSnapshot piece in roster.Pieces)
            {
                if (piece.OwnerPlayerId != playerId || !HasSynergy(piece, synergyId))
                {
                    continue;
                }

                if (indexByPieceId.TryGetValue(piece.PieceId, out int existingIndex))
                {
                    PieceSnapshot existing = result[existingIndex];
                    if (existing.Location != "Board" && piece.Location == "Board")
                    {
                        result[existingIndex] = piece;
                    }

                    continue;
                }

                indexByPieceId[piece.PieceId] = result.Count;
                result.Add(piece);
            }

            return result;
        }

        private static bool HasSynergy(PieceSnapshot piece, string synergyId)
        {
            if (piece?.Synergies == null)
            {
                return false;
            }

            foreach (ShopSynergySnapshot synergy in piece.Synergies)
            {
                if (synergy.SynergyId == synergyId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildDescription(SynergyProgressSnapshot synergy)
        {
            string effectText = string.IsNullOrWhiteSpace(synergy.EffectDescription)
                ? "暂无效果说明"
                : synergy.EffectDescription;

            return effectText;
        }
    }
}
