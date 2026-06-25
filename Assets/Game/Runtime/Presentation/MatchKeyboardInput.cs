using ProtectTree.Core.Match;
using ProtectTree.Core.Network;
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

        [SerializeField]
        private bool debugUseObservedPlayer = true;

        [SerializeField]
        private string debugGrantPieceId = "Sprout";

        [SerializeField]
        private KeyCode debugDisconnectMatchTransportKey = KeyCode.F5;

        [SerializeField]
        private bool debugDisconnectRequiresControl = true;

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

            int controlledPlayerId = GetControlledPlayerId(context);
            ClearInvalidSelection(context.Pieces, controlledPlayerId);
            context.SelectPiece(_selectedPieceInstanceId);
            HandleLanReconnectDebug(context);

            string phase = context.Flow.Phase;
            if (phase != "Preparation" && phase != "BossPreparation")
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!TrySendClientCommand(context, MatchCommand.SetReady(true)))
                {
                    context.Runtime.SetPlayerReady(controlledPlayerId, true);
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                context.Runtime.DebugEnterBossPreparation();
                _selectedPieceInstanceId = 0;
                context.SelectPiece(0);
                return;
            }

            HandleDebugGrant(context, controlledPlayerId);
            HandlePurchase(context, controlledPlayerId);
            HandleRefresh(context, controlledPlayerId);
            HandleShopLock(context, controlledPlayerId);
            HandleShopUpgrade(context, controlledPlayerId);
            HandleSelection(context.Pieces, controlledPlayerId);
            HandleBench(context, controlledPlayerId);
            HandleSell(context, controlledPlayerId);
            HandleDeployment(context, controlledPlayerId);
            HandleFacing(context, controlledPlayerId);
            context.SelectPiece(_selectedPieceInstanceId);
        }

        public override void OnRuntimeUnavailable()
        {
            _selectedPieceInstanceId = 0;
        }

        private int GetControlledPlayerId(MatchSceneContext context)
        {
            if (debugUseObservedPlayer && context.ObservedPlayerId > 0)
            {
                return context.ObservedPlayerId;
            }

            return localPlayerId;
        }

        private void HandleLanReconnectDebug(MatchSceneContext context)
        {
            if (!Input.GetKeyDown(debugDisconnectMatchTransportKey))
            {
                return;
            }

            if (debugDisconnectRequiresControl
                && !Input.GetKey(KeyCode.LeftControl)
                && !Input.GetKey(KeyCode.RightControl))
            {
                return;
            }

            if (context.LanMatch == null)
            {
                Debug.LogWarning(
                    "[ProtectTree][LAN Match] Debug reconnect test skipped because no LAN match runtime is active.",
                    this);
                return;
            }

            context.LanMatch.DebugDisconnectClientTransportForReconnectTest();
        }

        private void ClearInvalidSelection(
            PieceRosterSnapshot pieces,
            int controlledPlayerId)
        {
            if (_selectedPieceInstanceId == 0)
            {
                return;
            }

            foreach (PieceSnapshot piece in pieces.Pieces)
            {
                if (piece.OwnerPlayerId == controlledPlayerId
                    && piece.InstanceId == _selectedPieceInstanceId)
                {
                    return;
                }
            }

            _selectedPieceInstanceId = 0;
        }

        private void HandleDebugGrant(
            MatchSceneContext context,
            int controlledPlayerId)
        {
            if (!Input.GetKeyDown(KeyCode.G)
                || string.IsNullOrWhiteSpace(debugGrantPieceId))
            {
                return;
            }

            _selectedPieceInstanceId =
                context.Runtime.GrantPiece(controlledPlayerId, debugGrantPieceId);
        }

        private void HandlePurchase(
            MatchSceneContext context,
            int controlledPlayerId)
        {
            if (controlledPlayerId != context.LocalPlayerId)
            {
                return;
            }

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
                && CanAfford(context.Players, controlledPlayerId, offer.Cost))
            {
                if (TrySendClientCommand(
                    context,
                    MatchCommand.PurchaseShopOffer(slotIndex)))
                {
                    return;
                }

                _selectedPieceInstanceId =
                    context.Runtime.PurchaseShopOffer(controlledPlayerId, slotIndex);
            }
        }

        private void HandleRefresh(
            MatchSceneContext context,
            int controlledPlayerId)
        {
            if (controlledPlayerId != context.LocalPlayerId)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.R)
                && CanAfford(
                    context.Players,
                    controlledPlayerId,
                    context.Shop.RefreshCost))
            {
                if (!TrySendClientCommand(context, MatchCommand.RefreshShop()))
                {
                    context.Runtime.RefreshShop(controlledPlayerId);
                }
            }
        }

        private void HandleShopUpgrade(
            MatchSceneContext context,
            int controlledPlayerId)
        {
            if (controlledPlayerId != context.LocalPlayerId)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.U)
                && context.Shop.CanUpgrade
                && CanAfford(
                    context.Players,
                    controlledPlayerId,
                    context.Shop.UpgradeCost))
            {
                if (!TrySendClientCommand(context, MatchCommand.UpgradeShop()))
                {
                    context.Runtime.UpgradeShop(controlledPlayerId);
                }
            }
        }

        private void HandleShopLock(
            MatchSceneContext context,
            int controlledPlayerId)
        {
            if (controlledPlayerId != context.LocalPlayerId)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                if (!TrySendClientCommand(context, MatchCommand.ToggleShopLock()))
                {
                    context.Runtime.ToggleShopLock(controlledPlayerId);
                }
            }
        }

        private bool TrySendClientCommand(
            MatchSceneContext context,
            MatchCommand command)
        {
            if (context.LanMatch == null
                || !context.LanMatch.IsActive
                || !context.LanMatch.IsClient)
            {
                return false;
            }

            if (!context.LanMatch.TrySendCommand(command))
            {
                Debug.LogWarning(
                    $"Debug input command {command.Type} was not sent; waiting for LAN match transport.",
                    this);
            }

            // LAN 客户端只发玩家意图，不直接修改本地 Lua 权威状态。
            return true;
        }

        private void HandleSelection(
            PieceRosterSnapshot pieces,
            int controlledPlayerId)
        {
            if (!Input.GetKeyDown(KeyCode.Tab))
            {
                return;
            }

            bool selectNext = _selectedPieceInstanceId == 0;
            foreach (PieceSnapshot piece in pieces.Pieces)
            {
                if (piece.OwnerPlayerId != controlledPlayerId)
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

        private void HandleBench(
            MatchSceneContext context,
            int controlledPlayerId)
        {
            if (!Input.GetKeyDown(KeyCode.B))
            {
                return;
            }

            PieceSnapshot selectedPiece =
                FindSelectedPiece(context.Pieces, controlledPlayerId);
            PieceCapacitySnapshot capacity =
                FindCapacity(context.Pieces, controlledPlayerId);
            if (selectedPiece != null
                && selectedPiece.Location == "Board"
                && capacity != null
                && capacity.BenchCount < capacity.BenchCapacity)
            {
                if (TrySendClientCommand(
                    context,
                    MatchCommand.BenchPiece(_selectedPieceInstanceId)))
                {
                    return;
                }

                context.Runtime.BenchPiece(
                    controlledPlayerId,
                    _selectedPieceInstanceId);
            }
        }

        private void HandleSell(
            MatchSceneContext context,
            int controlledPlayerId)
        {
            if (Input.GetKeyDown(KeyCode.X)
                && FindSelectedPiece(context.Pieces, controlledPlayerId) != null)
            {
                if (TrySendClientCommand(
                    context,
                    MatchCommand.SellPiece(_selectedPieceInstanceId)))
                {
                    _selectedPieceInstanceId = 0;
                    return;
                }

                context.Runtime.SellPiece(
                    controlledPlayerId,
                    _selectedPieceInstanceId);
                _selectedPieceInstanceId = 0;
            }
        }

        private void HandleDeployment(
            MatchSceneContext context,
            int controlledPlayerId)
        {
            int cellId = Input.GetKeyDown(KeyCode.Alpha1)
                ? 101
                : Input.GetKeyDown(KeyCode.Alpha2)
                    ? 102
                    : Input.GetKeyDown(KeyCode.Alpha3) ? 103 : 0;

            if (_selectedPieceInstanceId != 0
                && cellId != 0
                && IsCellAvailable(context.Pieces, controlledPlayerId, cellId))
            {
                if (TrySendClientCommand(
                    context,
                    MatchCommand.DeployPiece(_selectedPieceInstanceId, cellId)))
                {
                    return;
                }

                context.Runtime.DeployPiece(
                    controlledPlayerId,
                    _selectedPieceInstanceId,
                    cellId);
            }
        }

        private void HandleFacing(
            MatchSceneContext context,
            int controlledPlayerId)
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
                if (TrySendClientCommand(
                    context,
                    MatchCommand.SetPieceFacing(_selectedPieceInstanceId, facing)))
                {
                    return;
                }

                context.Runtime.SetPieceFacing(
                    controlledPlayerId,
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

        private PieceSnapshot FindSelectedPiece(
            PieceRosterSnapshot pieces,
            int controlledPlayerId)
        {
            foreach (PieceSnapshot piece in pieces.Pieces)
            {
                if (piece.OwnerPlayerId == controlledPlayerId
                    && piece.InstanceId == _selectedPieceInstanceId)
                {
                    return piece;
                }
            }

            return null;
        }

        private static PieceCapacitySnapshot FindCapacity(
            PieceRosterSnapshot pieces,
            int controlledPlayerId)
        {
            foreach (PieceCapacitySnapshot capacity in pieces.Players)
            {
                if (capacity.PlayerId == controlledPlayerId)
                {
                    return capacity;
                }
            }

            return null;
        }

        private bool IsCellAvailable(
            PieceRosterSnapshot pieces,
            int controlledPlayerId,
            int cellId)
        {
            foreach (PieceSnapshot piece in pieces.Pieces)
            {
                if (piece.OwnerPlayerId == controlledPlayerId
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
