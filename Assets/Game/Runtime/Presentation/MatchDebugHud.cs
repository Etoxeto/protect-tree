using ProtectTree.Core.Match;
using UnityEngine;

namespace ProtectTree.Runtime.Presentation
{
    /// <summary>
    /// 临时诊断 HUD，仅显示权威快照信息，不负责正式 UI 或棋盘画面。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchDebugHud : MatchSceneFeature
    {
        [SerializeField]
        private bool showHud;

        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private MatchFlowSnapshot _flowSnapshot;
        private PlayerRosterSnapshot _playerRosterSnapshot;
        private PieceRosterSnapshot _pieceRosterSnapshot;
        private ShopSnapshot _shopSnapshot;
        private int _selectedPieceInstanceId;

        public override void Refresh(MatchSceneContext context)
        {
            _flowSnapshot = context.Flow;
            _shopSnapshot = context.Shop;
            _playerRosterSnapshot = context.Players;
            _pieceRosterSnapshot = context.Pieces;
            _selectedPieceInstanceId = context.SelectedPieceInstanceId;
        }

        public override void OnRuntimeUnavailable()
        {
            _flowSnapshot = null;
            _shopSnapshot = null;
            _playerRosterSnapshot = null;
            _pieceRosterSnapshot = null;
            _selectedPieceInstanceId = 0;
        }

        private void OnGUI()
        {
            if (!showHud)
            {
                return;
            }

            EnsureGuiStyles();
            if (_flowSnapshot == null)
            {
                return;
            }

            GUI.Label(
                new Rect(20f, 16f, 700f, 42f),
                $"Protect Tree Demo    Wave {_flowSnapshot.Wave}    {_flowSnapshot.Phase}    "
                + $"{_flowSnapshot.RemainingSeconds:F1}s",
                _titleStyle);
            GUI.Label(
                new Rect(22f, 58f, 1100f, 30f),
                "Prep: Q/W/E buy, R refresh, U upgrade, L lock, Tab select, B bench, X sell",
                _labelStyle);
            GUI.Label(
                new Rect(22f, 82f, 1100f, 30f),
                "Diagnostic: 1/2/3 deploy, arrow keys face, Space ready; mouse supports formal placement",
                _labelStyle);

            if (_playerRosterSnapshot != null
                && _playerRosterSnapshot.Players.Count > 0)
            {
                PlayerSnapshot player = _playerRosterSnapshot.Players[0];
                GUI.Label(
                    new Rect(22f, 106f, 800f, 30f),
                    $"Player {player.PlayerId}    HP {player.Health}/{player.MaxHealth}    "
                    + $"Gold {player.Gold}    {player.Status}    Ready {player.IsReady}",
                    _labelStyle);
            }

            PieceCapacitySnapshot capacity = FindCapacity(
                _pieceRosterSnapshot,
                _shopSnapshot?.PlayerId ?? 0);
            if (capacity != null)
            {
                GUI.Label(
                    new Rect(700f, 106f, 500f, 30f),
                    $"Board {capacity.BoardCount}/{capacity.DeploymentLimit}    "
                    + $"Bench {capacity.BenchCount}/{capacity.BenchCapacity}    "
                    + $"Temporary {capacity.TemporaryBenchCount}/"
                    + $"{capacity.TemporaryBenchCapacity}",
                    _labelStyle);
            }

            PieceSnapshot selectedPiece = FindPiece(
                _pieceRosterSnapshot,
                _selectedPieceInstanceId);
            if (selectedPiece != null)
            {
                string cell = selectedPiece.CellId.HasValue
                    ? selectedPiece.CellId.Value.ToString()
                    : "-";
                GUI.Label(
                    new Rect(22f, 136f, 1100f, 30f),
                    $"Selected #{selectedPiece.InstanceId} {selectedPiece.PieceId} Lv.{selectedPiece.Level}    "
                    + $"{selectedPiece.Location} Cell {cell}    Facing {selectedPiece.Facing}    "
                    + $"Damage {selectedPiece.Damage}    Sell {selectedPiece.SellValue}g",
                    _labelStyle);
            }

            if (_shopSnapshot != null)
            {
                GUI.Label(
                    new Rect(22f, 166f, 1000f, 30f),
                    BuildShopLabel(_shopSnapshot),
                    _labelStyle);
            }

            GUI.Label(
                new Rect(22f, 196f, 1100f, 30f),
                BuildSynergyLabel(
                    _pieceRosterSnapshot,
                    _shopSnapshot?.PlayerId ?? 0),
                _labelStyle);

            if (_flowSnapshot.IsFinished)
            {
                GUI.Label(
                    new Rect(340f, 250f, 600f, 80f),
                    $"MATCH {_flowSnapshot.Result}",
                    _titleStyle);
            }
        }

        private static string BuildShopLabel(ShopSnapshot shop)
        {
            string upgrade = shop.CanUpgrade
                ? $"[U upgrade {shop.UpgradeCost}]"
                : "[MAX]";
            string label =
                $"Shop Lv.{shop.Level}/{shop.MaxLevel} {upgrade} [R refresh {shop.RefreshCost}] "
                + $"[L {(shop.IsLocked ? "unlock" : "lock")}]";

            foreach (ShopOfferSnapshot offer in shop.Offers)
            {
                string key = offer.SlotIndex == 1
                    ? "Q"
                    : offer.SlotIndex == 2 ? "W" : "E";
                string state = offer.IsSold ? "Sold" : $"{offer.PieceId} {offer.Cost}g";
                label += $"    [{key}] {state}";
            }

            return label;
        }

        private static string BuildSynergyLabel(
            PieceRosterSnapshot pieces,
            int playerId)
        {
            string label = "Synergies";
            if (pieces == null)
            {
                return label + "    None";
            }

            bool hasActiveSynergy = false;
            foreach (ActiveSynergySnapshot synergy in pieces.ActiveSynergies)
            {
                if (synergy.PlayerId != playerId)
                {
                    continue;
                }

                hasActiveSynergy = true;
                label += $"    {synergy.SynergyId} Lv.{synergy.Level} "
                    + $"{synergy.UniquePieceCount}/{synergy.RequiredUniquePieces} "
                    + synergy.EffectDescription;
            }

            return hasActiveSynergy ? label : label + "    None";
        }

        private static PieceSnapshot FindPiece(
            PieceRosterSnapshot snapshot,
            int instanceId)
        {
            if (snapshot == null)
            {
                return null;
            }

            foreach (PieceSnapshot piece in snapshot.Pieces)
            {
                if (piece.InstanceId == instanceId)
                {
                    return piece;
                }
            }

            return null;
        }

        private static PieceCapacitySnapshot FindCapacity(
            PieceRosterSnapshot snapshot,
            int playerId)
        {
            if (snapshot == null)
            {
                return null;
            }

            foreach (PieceCapacitySnapshot capacity in snapshot.Players)
            {
                if (capacity.PlayerId == playerId)
                {
                    return capacity;
                }
            }

            return null;
        }

        private void EnsureGuiStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = new Color(0.85f, 0.9f, 1f) },
            };
        }
    }
}
