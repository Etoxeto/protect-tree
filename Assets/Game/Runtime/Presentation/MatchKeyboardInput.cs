using ProtectTree.Core.Match;
using UnityEngine;

namespace ProtectTree.Runtime.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MatchKeyboardInput : MatchSceneFeature
    {
        [SerializeField]
        private bool enableDebugInput;

        [SerializeField]
        private int localPlayerId = 1;

        private int _selectedPieceInstanceId;

        public override void Refresh(MatchSceneContext context)
        {
            if (!enableDebugInput)
            {
                _selectedPieceInstanceId = context.SelectedPieceInstanceId;
                return;
            }

            if (context.Flow == null || context.Flow.IsFinished)
            {
                _selectedPieceInstanceId = 0;
                context.SelectPiece(0);
                return;
            }

            if (context.SelectedPieceInstanceId != _selectedPieceInstanceId)
            {
                _selectedPieceInstanceId = context.SelectedPieceInstanceId;
            }

            ClearInvalidSelection(context.Pieces);
            context.SelectPiece(_selectedPieceInstanceId);

            string phase = context.Flow.Phase;
            if (phase != "Preparation" && phase != "BossPreparation")
            {
                return;
            }

            // 输入层只提交玩家意图；是否合法以及如何改变状态仍由 Lua 权威逻辑决定。
            if (Input.GetKeyDown(KeyCode.Space))
            {
                context.Runtime.SetPlayerReady(localPlayerId, true);
                return;
            }

            HandlePurchase(context);
            HandleRefresh(context);
            HandleShopLock(context);
            HandleShopUpgrade(context);
            HandleSelection(context.Pieces);
            HandleBench(context);
            HandleSell(context);
            HandleDeployment(context);
            HandleFacing(context);
            context.SelectPiece(_selectedPieceInstanceId);
        }

        public override void OnRuntimeUnavailable()
        {
            _selectedPieceInstanceId = 0;
        }

        private void ClearInvalidSelection(PieceRosterSnapshot pieces)
        {
            if (_selectedPieceInstanceId == 0)
            {
                return;
            }

            foreach (PieceSnapshot piece in pieces.Pieces)
            {
                if (piece.OwnerPlayerId == localPlayerId
                    && piece.InstanceId == _selectedPieceInstanceId)
                {
                    return;
                }
            }

            _selectedPieceInstanceId = 0;
        }

        private void HandlePurchase(MatchSceneContext context)
        {
            int slotIndex = Input.GetKeyDown(KeyCode.Q)
                ? 1
                : Input.GetKeyDown(KeyCode.W)
                    ? 2
                    : Input.GetKeyDown(KeyCode.E) ? 3 : 0;

            if (slotIndex == 0 || slotIndex > context.Shop.Offers.Count)
            {
                return;
            }

            ShopOfferSnapshot offer = context.Shop.Offers[slotIndex - 1];
            if (!offer.IsSold
                && CanAfford(context.Players, localPlayerId, offer.Cost))
            {
                _selectedPieceInstanceId =
                    context.Runtime.PurchaseShopOffer(localPlayerId, slotIndex);
            }
        }

        private void HandleRefresh(MatchSceneContext context)
        {
            if (Input.GetKeyDown(KeyCode.R)
                && CanAfford(
                    context.Players,
                    localPlayerId,
                    context.Shop.RefreshCost))
            {
                context.Runtime.RefreshShop(localPlayerId);
            }
        }

        private void HandleShopUpgrade(MatchSceneContext context)
        {
            if (Input.GetKeyDown(KeyCode.U)
                && context.Shop.CanUpgrade
                && CanAfford(
                    context.Players,
                    localPlayerId,
                    context.Shop.UpgradeCost))
            {
                context.Runtime.UpgradeShop(localPlayerId);
            }
        }

        private void HandleShopLock(MatchSceneContext context)
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                context.Runtime.ToggleShopLock(localPlayerId);
            }
        }

        private void HandleSelection(PieceRosterSnapshot pieces)
        {
            if (!Input.GetKeyDown(KeyCode.Tab))
            {
                return;
            }

            bool selectNext = _selectedPieceInstanceId == 0;
            foreach (PieceSnapshot piece in pieces.Pieces)
            {
                if (piece.OwnerPlayerId != localPlayerId)
                {
                    continue;
                }

                if (selectNext)
                {
                    _selectedPieceInstanceId = piece.InstanceId;
                    return;
                }

                selectNext = piece.InstanceId == _selectedPieceInstanceId;
            }

            _selectedPieceInstanceId = 0;
        }

        private void HandleBench(MatchSceneContext context)
        {
            if (!Input.GetKeyDown(KeyCode.B))
            {
                return;
            }

            PieceSnapshot selectedPiece = FindSelectedPiece(context.Pieces);
            PieceCapacitySnapshot capacity = FindCapacity(context.Pieces);
            if (selectedPiece != null
                && selectedPiece.Location == "Board"
                && capacity != null
                && capacity.BenchCount < capacity.BenchCapacity)
            {
                context.Runtime.BenchPiece(localPlayerId, _selectedPieceInstanceId);
            }
        }

        private void HandleSell(MatchSceneContext context)
        {
            if (Input.GetKeyDown(KeyCode.X)
                && FindSelectedPiece(context.Pieces) != null)
            {
                context.Runtime.SellPiece(localPlayerId, _selectedPieceInstanceId);
                _selectedPieceInstanceId = 0;
            }
        }

        private void HandleDeployment(MatchSceneContext context)
        {
            int cellId = Input.GetKeyDown(KeyCode.Alpha1)
                ? 101
                : Input.GetKeyDown(KeyCode.Alpha2)
                    ? 102
                    : Input.GetKeyDown(KeyCode.Alpha3) ? 103 : 0;

            if (_selectedPieceInstanceId != 0
                && cellId != 0
                && IsCellAvailable(context.Pieces, cellId))
            {
                context.Runtime.DeployPiece(
                    localPlayerId,
                    _selectedPieceInstanceId,
                    cellId);
            }
        }

        private void HandleFacing(MatchSceneContext context)
        {
            string facing = Input.GetKeyDown(KeyCode.UpArrow)
                ? "Up"
                : Input.GetKeyDown(KeyCode.RightArrow)
                    ? "Right"
                    : Input.GetKeyDown(KeyCode.DownArrow)
                        ? "Down"
                        : Input.GetKeyDown(KeyCode.LeftArrow) ? "Left" : null;

            if (_selectedPieceInstanceId != 0 && facing != null)
            {
                context.Runtime.SetPieceFacing(
                    localPlayerId,
                    _selectedPieceInstanceId,
                    facing);
            }
        }

        private static bool CanAfford(
            PlayerRosterSnapshot players,
            int playerId,
            int cost)
        {
            foreach (PlayerSnapshot player in players.Players)
            {
                if (player.PlayerId == playerId)
                {
                    return player.Gold >= cost;
                }
            }

            return false;
        }

        private PieceSnapshot FindSelectedPiece(PieceRosterSnapshot pieces)
        {
            foreach (PieceSnapshot piece in pieces.Pieces)
            {
                if (piece.OwnerPlayerId == localPlayerId
                    && piece.InstanceId == _selectedPieceInstanceId)
                {
                    return piece;
                }
            }

            return null;
        }

        private PieceCapacitySnapshot FindCapacity(PieceRosterSnapshot pieces)
        {
            foreach (PieceCapacitySnapshot capacity in pieces.Players)
            {
                if (capacity.PlayerId == localPlayerId)
                {
                    return capacity;
                }
            }

            return null;
        }

        private bool IsCellAvailable(PieceRosterSnapshot pieces, int cellId)
        {
            foreach (PieceSnapshot piece in pieces.Pieces)
            {
                if (piece.OwnerPlayerId == localPlayerId
                    && piece.InstanceId != _selectedPieceInstanceId
                    && piece.CellId == cellId)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
