using System;
using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Core.Network;
using ProtectTree.Runtime.Board;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProtectTree.Runtime.Presentation
{
    /// <summary>
    /// 将鼠标操作转换为一次性的权威放置命令；拖动和朝向确认期间只改变本地预览。
    /// </summary>
    public sealed class MatchBoardInteraction
    {
        private const float DragStartThresholdPixels = 8f;
        private const float FacingPickExpansionWorld = 0.45f;

        private int _pressedPieceInstanceId;
        private Vector2 _pressScreenPosition;
        private int _draggingPieceInstanceId;
        private int _pendingPieceInstanceId;
        private int _pendingSwapPieceInstanceId;
        private BoardVisualCell _pendingCell;
        private BoardVisualCell _pendingSwapCell;
        private string _pendingFacing;
        private bool _isChoosingFacing;
        private readonly List<RaycastResult> _uiRaycastResults =
            new List<RaycastResult>();
        private readonly List<BoardVisualCell> _legalPlacementCells =
            new List<BoardVisualCell>();
        private readonly List<BoardVisualCell> _attackRangeCells =
            new List<BoardVisualCell>();
        private readonly HashSet<int> _attackRangeCellIds = new HashSet<int>();

        public void Update(
            MatchSceneContext context,
            int observedPlayerId,
            BoardCellPicker picker,
            BoardPerspectiveProjector projector,
            BoardPieceView pieceView,
            BoardCellHighlightView highlightView)
        {
            if (context == null
                || picker == null
                || projector == null
                || pieceView == null
                || highlightView == null)
            {
                context?.SetPieceInspectBlocked(false);
                return;
            }

            if (context.Flow == null || context.Flow.IsFinished)
            {
                RestoreDraggingPiece(context, pieceView);
                Reset(highlightView);
                context.SelectPiece(0);
                SyncPieceInspectBlocked(context);
                return;
            }

            bool isPreparation = context.Flow.Phase == "Preparation"
                || context.Flow.Phase == "BossPreparation";
            if (observedPlayerId != context.LocalPlayerId)
            {
                Reset(highlightView);
                SyncPieceInspectBlocked(context);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                RestoreDraggingPiece(context, pieceView);
                Reset(highlightView);
                SyncPieceInspectBlocked(context);
                return;
            }

            if (!isPreparation)
            {
                UpdateSelectionOnly(context, picker, pieceView, highlightView);
                SyncPieceInspectBlocked(context);
                return;
            }

            if (_pendingPieceInstanceId != 0)
            {
                UpdateFacingConfirmation(
                    context,
                    picker,
                    projector,
                    pieceView,
                    highlightView);
                SyncPieceInspectBlocked(context);
                return;
            }

            UpdateDragging(context, picker, pieceView, highlightView);
            SyncPieceInspectBlocked(context);
        }

        public void Reset(BoardCellHighlightView highlightView)
        {
            _pressedPieceInstanceId = 0;
            _draggingPieceInstanceId = 0;
            _pendingPieceInstanceId = 0;
            _pendingSwapPieceInstanceId = 0;
            _pendingCell = null;
            _pendingSwapCell = null;
            _pendingFacing = null;
            _isChoosingFacing = false;
            highlightView?.SetVisible(false);
            highlightView?.ClearPlacementCells();
            highlightView?.ClearAttackRangeCells();
        }

        private bool IsPieceInspectBlocked =>
            _pressedPieceInstanceId != 0
            || _draggingPieceInstanceId != 0
            || _pendingPieceInstanceId != 0;

        private void SyncPieceInspectBlocked(MatchSceneContext context)
        {
            context?.SetPieceInspectBlocked(IsPieceInspectBlocked);
        }

        private void UpdateSelectionOnly(
            MatchSceneContext context,
            BoardCellPicker picker,
            BoardPieceView pieceView,
            BoardCellHighlightView highlightView)
        {
            if (IsPieceInspectBlocked)
            {
                Reset(highlightView);
            }
            else
            {
                highlightView.SetVisible(false);
                highlightView.ClearPlacementCells();
            }

            if (!Input.GetMouseButtonDown(0))
            {
                ShowSelectedPieceAttackRange(context, picker, highlightView);
                return;
            }

            highlightView.ClearAttackRangeCells();

            if (IsPointerOverUi())
            {
                context.ClearPieceSelection();
                return;
            }

            if (TryPickPiece(pieceView, out int clickedPieceInstanceId))
            {
                context.SelectPiece(clickedPieceInstanceId);
                ShowSelectedPieceAttackRange(context, picker, highlightView);
                return;
            }

            if (TryPickCell(picker, out _))
            {
                context.SelectPiece(0);
                return;
            }

            context.SelectPiece(0);
        }

        private void UpdateDragging(
            MatchSceneContext context,
            BoardCellPicker picker,
            BoardPieceView pieceView,
            BoardCellHighlightView highlightView)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUi())
                {
                    context.ClearPieceSelection();
                    highlightView.SetVisible(false);
                    highlightView.ClearAttackRangeCells();
                    return;
                }

                PieceSnapshot clickedPiece = null;
                BoardVisualCell clickedCell = null;
                if (TryPickPiece(pieceView, out int clickedPieceInstanceId))
                {
                    clickedPiece = FindPiece(context.Pieces, clickedPieceInstanceId);
                }

                if (clickedPiece == null
                    && !TryPickCell(picker, out clickedCell))
                {
                    context.SelectPiece(0);
                    highlightView.SetVisible(false);
                    highlightView.ClearAttackRangeCells();
                    return;
                }

                if (clickedPiece == null)
                {
                    clickedPiece = FindPieceAtCell(
                        context.Pieces,
                        context.LocalPlayerId,
                        clickedCell.CellId);
                }

                if (clickedPiece == null)
                {
                    context.SelectPiece(0);
                    highlightView.Show(clickedCell);
                    highlightView.ClearAttackRangeCells();
                    Debug.Log(
                        $"Selected authority board cell {clickedCell.CellId}: "
                        + $"visual=({clickedCell.X}, {clickedCell.Y}), "
                        + $"terrain={clickedCell.Terrain}, zone={clickedCell.Zone}.");
                    return;
                }

                _pressedPieceInstanceId = clickedPiece.InstanceId;
                _pressScreenPosition = Input.mousePosition;
            }

            if (_pressedPieceInstanceId != 0 && _draggingPieceInstanceId == 0)
            {
                if (!Input.GetMouseButton(0))
                {
                    context.SelectPiece(_pressedPieceInstanceId);
                    _pressedPieceInstanceId = 0;
                    ShowSelectedPieceAttackRange(context, picker, highlightView);
                    return;
                }

                Vector2 dragDelta = (Vector2)Input.mousePosition - _pressScreenPosition;
                if (dragDelta.magnitude < DragStartThresholdPixels)
                {
                    highlightView.SetVisible(false);
                    highlightView.ClearAttackRangeCells();
                    return;
                }

                PieceSnapshot pressedPiece =
                    FindPiece(context.Pieces, _pressedPieceInstanceId);
                if (pressedPiece == null)
                {
                    Reset(highlightView);
                    return;
                }

                _draggingPieceInstanceId = pressedPiece.InstanceId;
                _pressedPieceInstanceId = 0;
                context.SelectPiece(pressedPiece.InstanceId);
                ShowPlacementHighlights(context, pressedPiece, picker, highlightView);
            }

            if (_draggingPieceInstanceId == 0)
            {
                ShowSelectedPieceAttackRange(context, picker, highlightView);
                return;
            }

            PieceSnapshot draggingPiece =
                FindPiece(context.Pieces, _draggingPieceInstanceId);
            if (draggingPiece == null)
            {
                Reset(highlightView);
                return;
            }

            pieceView.PreviewPieceAtWorld(
                draggingPiece.InstanceId,
                GetMouseWorldPosition(),
                draggingPiece.Facing);

            bool isOverSellDropZone = TryFindSellDropZone(context, draggingPiece);
            bool isPointerOverUi =
                isOverSellDropZone || IsPointerOverUi(ignorePieceInspectPanel: true);
            BoardVisualCell targetCell = null;
            bool hasValidTarget = !isPointerOverUi
                && TryPickCell(picker, out targetCell)
                && CanPlace(context, draggingPiece, targetCell);

            if (hasValidTarget)
            {
                highlightView.Show(targetCell);
                ShowPieceAttackRangeAtCell(
                    draggingPiece,
                    targetCell,
                    draggingPiece.Facing,
                    picker,
                    highlightView);
            }
            else
            {
                highlightView.SetVisible(false);
                highlightView.ClearAttackRangeCells();
            }

            if (!Input.GetMouseButtonUp(0))
            {
                return;
            }

            _draggingPieceInstanceId = 0;
            if (isOverSellDropZone)
            {
                SubmitSell(context, draggingPiece.InstanceId);
                Reset(highlightView);
                return;
            }

            if (!hasValidTarget)
            {
                pieceView.RestorePiecePreview(draggingPiece);
                Reset(highlightView);
                return;
            }

            if (targetCell.AcceptsReservePiece)
            {
                SubmitPlacement(
                    context,
                    draggingPiece.InstanceId,
                    targetCell.CellId,
                    facing: null);
                Reset(highlightView);
                SyncPieceInspectBlocked(context);
                return;
            }

            // 战场落点先保持为本地预览，玩家再次点击预览棋子并拖动后才提交。
            pieceView.PreviewPiece(
                draggingPiece.InstanceId,
                targetCell,
                draggingPiece.Facing);
            _pendingPieceInstanceId = draggingPiece.InstanceId;
            _pendingSwapPieceInstanceId = 0;
            _pendingCell = targetCell;
            _pendingSwapCell = null;
            _pendingFacing = draggingPiece.Facing;
            PieceSnapshot swapPiece = FindPieceAtCell(
                context.Pieces,
                context.LocalPlayerId,
                targetCell.CellId);
            if (swapPiece != null
                && swapPiece.InstanceId != draggingPiece.InstanceId
                && draggingPiece.CellId.HasValue)
            {
                _pendingSwapPieceInstanceId = swapPiece.InstanceId;
                _pendingSwapCell = FindCellById(picker, draggingPiece.CellId.Value);
                if (_pendingSwapCell != null)
                {
                    pieceView.PreviewPiece(
                        swapPiece.InstanceId,
                        _pendingSwapCell,
                        swapPiece.Facing);
                }
            }

            highlightView.ClearPlacementCells();
            highlightView.Show(targetCell);
            ShowPieceAttackRangeAtCell(
                draggingPiece,
                targetCell,
                draggingPiece.Facing,
                picker,
                highlightView);
        }

        private void UpdateFacingConfirmation(
            MatchSceneContext context,
            BoardCellPicker picker,
            BoardPerspectiveProjector projector,
            BoardPieceView pieceView,
            BoardCellHighlightView highlightView)
        {
            PieceSnapshot piece = FindPiece(context.Pieces, _pendingPieceInstanceId);
            if (piece == null)
            {
                Reset(highlightView);
                SyncPieceInspectBlocked(context);
                return;
            }

            pieceView.PreviewPiece(piece.InstanceId, _pendingCell, _pendingFacing);
            if (_pendingSwapPieceInstanceId != 0 && _pendingSwapCell != null)
            {
                PieceSnapshot swapPiece =
                    FindPiece(context.Pieces, _pendingSwapPieceInstanceId);
                if (swapPiece != null)
                {
                    pieceView.PreviewPiece(
                        swapPiece.InstanceId,
                        _pendingSwapCell,
                        swapPiece.Facing);
                }
            }

            highlightView.Show(_pendingCell);
            ShowPieceAttackRangeAtCell(
                piece,
                _pendingCell,
                _pendingFacing,
                picker,
                highlightView);

            if (Input.GetMouseButtonDown(0))
            {
                _isChoosingFacing =
                    TryPickPieceWithExpandedBounds(
                        pieceView,
                        _pendingPieceInstanceId);

                if (!_isChoosingFacing)
                {
                    Reset(highlightView);
                    return;
                }
            }

            if (_isChoosingFacing && Input.GetMouseButton(0))
            {
                _pendingFacing = CalculateFacing(projector, _pendingCell, _pendingFacing);
                pieceView.PreviewPiece(piece.InstanceId, _pendingCell, _pendingFacing);
                ShowPieceAttackRangeAtCell(
                    piece,
                    _pendingCell,
                    _pendingFacing,
                    picker,
                    highlightView);
            }

            if (!_isChoosingFacing || !Input.GetMouseButtonUp(0))
            {
                return;
            }

            SubmitPlacement(
                context,
                piece.InstanceId,
                _pendingCell.CellId,
                _pendingFacing);
            Reset(highlightView);
            SyncPieceInspectBlocked(context);
        }

        private static bool CanPlace(
            MatchSceneContext context,
            PieceSnapshot piece,
            BoardVisualCell targetCell)
        {
            if (piece.CellId == targetCell.CellId)
            {
                return piece.Location == "Board"
                    && targetCell.AllowsBattleDeployment
                    && !targetCell.AcceptsReservePiece
                    && CanDeployOnTerrain(piece, targetCell.Terrain.ToString());
            }

            PieceSnapshot occupyingPiece = FindPieceAtCell(
                context.Pieces,
                context.LocalPlayerId,
                targetCell.CellId);
            if (occupyingPiece != null)
            {
                return CanSwapWithOccupyingPiece(
                    context,
                    piece,
                    occupyingPiece,
                    targetCell);
            }

            if (targetCell.AcceptsReservePiece)
            {
                if (piece.Location == "Bench")
                {
                    return true;
                }

                PieceCapacitySnapshot capacity =
                    FindCapacity(context.Pieces, context.LocalPlayerId);
                return capacity != null && capacity.BenchCount < capacity.BenchCapacity;
            }

            if (!targetCell.AllowsBattleDeployment
                || !CanDeployOnTerrain(piece, targetCell.Terrain.ToString()))
            {
                return false;
            }

            if (piece.Location == "Board")
            {
                return true;
            }

            PieceCapacitySnapshot playerCapacity =
                FindCapacity(context.Pieces, context.LocalPlayerId);
            return playerCapacity != null
                && playerCapacity.BoardCount < playerCapacity.DeploymentLimit;
        }

        private static bool CanSwapWithOccupyingPiece(
            MatchSceneContext context,
            PieceSnapshot draggingPiece,
            PieceSnapshot occupyingPiece,
            BoardVisualCell targetCell)
        {
            if (draggingPiece == null
                || occupyingPiece == null
                || targetCell == null
                || draggingPiece.InstanceId == occupyingPiece.InstanceId
                || !draggingPiece.CellId.HasValue
                || occupyingPiece.OwnerPlayerId != context.LocalPlayerId
                || occupyingPiece.Location != "Board"
                || targetCell.AcceptsReservePiece
                || !targetCell.AllowsBattleDeployment
                || !CanDeployOnTerrain(
                    draggingPiece,
                    targetCell.Terrain.ToString()))
            {
                return false;
            }

            if (draggingPiece.Location == "Board"
                && !CanDeployOnTerrain(occupyingPiece, draggingPiece.Terrain))
            {
                return false;
            }

            return true;
        }

        private void ShowPlacementHighlights(
            MatchSceneContext context,
            PieceSnapshot piece,
            BoardCellPicker picker,
            BoardCellHighlightView highlightView)
        {
            _legalPlacementCells.Clear();

            foreach (BoardVisualCell cell in picker.Cells)
            {
                if (CanPlace(context, piece, cell))
                {
                    _legalPlacementCells.Add(cell);
                }
            }

            highlightView.ShowPlacementCells(_legalPlacementCells);
        }

        private void ShowSelectedPieceAttackRange(
            MatchSceneContext context,
            BoardCellPicker picker,
            BoardCellHighlightView highlightView)
        {
            if (context.SelectedPieceInstanceId <= 0)
            {
                highlightView.ClearAttackRangeCells();
                return;
            }

            PieceSnapshot selectedPiece =
                FindPiece(context.Pieces, context.SelectedPieceInstanceId);
            if (selectedPiece == null
                || selectedPiece.OwnerPlayerId != context.LocalPlayerId
                || selectedPiece.Location != "Board"
                || !selectedPiece.CellId.HasValue)
            {
                highlightView.ClearAttackRangeCells();
                return;
            }

            BoardVisualCell cell = FindCellById(picker, selectedPiece.CellId.Value);
            ShowPieceAttackRangeAtCell(
                selectedPiece,
                cell,
                selectedPiece.Facing,
                picker,
                highlightView);
        }

        private void ShowPieceAttackRangeAtCell(
            PieceSnapshot piece,
            BoardVisualCell originCell,
            string facing,
            BoardCellPicker picker,
            BoardCellHighlightView highlightView)
        {
            if (piece == null
                || originCell == null
                || originCell.AcceptsReservePiece
                || !TryGetFacingBasis(
                    facing,
                    out Vector2Int forward,
                    out Vector2Int right))
            {
                highlightView.ClearAttackRangeCells();
                return;
            }

            _attackRangeCells.Clear();
            _attackRangeCellIds.Clear();

            IReadOnlyList<BoardVisualCell> cells =
                picker != null ? picker.Cells : null;
            foreach (PieceAttackRangeOffsetSnapshot offset in piece.AttackRange)
            {
                int targetX =
                    originCell.X
                    + forward.x * offset.Forward
                    + right.x * offset.Right;
                int targetY =
                    originCell.Y
                    + forward.y * offset.Forward
                    + right.y * offset.Right;
                BoardVisualCell targetCell =
                    FindCellByCoord(cells, targetX, targetY);
                if (targetCell == null
                    || !_attackRangeCellIds.Add(targetCell.CellId))
                {
                    continue;
                }

                _attackRangeCells.Add(targetCell);
            }

            highlightView.ShowAttackRangeCells(_attackRangeCells);
        }

        private static bool TryGetFacingBasis(
            string facing,
            out Vector2Int forward,
            out Vector2Int right)
        {
            switch (facing)
            {
                case "Up":
                    forward = new Vector2Int(0, 1);
                    break;
                case "Right":
                    forward = new Vector2Int(1, 0);
                    break;
                case "Down":
                    forward = new Vector2Int(0, -1);
                    break;
                case "Left":
                    forward = new Vector2Int(-1, 0);
                    break;
                default:
                    forward = Vector2Int.zero;
                    right = Vector2Int.zero;
                    return false;
            }

            // 与 Lua 的 BoardQueries 保持一致：attack_range 使用“前方/右方”相对坐标。
            right = new Vector2Int(forward.y, -forward.x);
            return true;
        }

        private static BoardVisualCell FindCellById(
            BoardCellPicker picker,
            int cellId)
        {
            return FindCellById(picker?.Cells, cellId);
        }

        private static BoardVisualCell FindCellById(
            IReadOnlyList<BoardVisualCell> cells,
            int cellId)
        {
            if (cells == null)
            {
                return null;
            }

            foreach (BoardVisualCell cell in cells)
            {
                if (cell.CellId == cellId)
                {
                    return cell;
                }
            }

            return null;
        }

        private static BoardVisualCell FindCellByCoord(
            IReadOnlyList<BoardVisualCell> cells,
            int x,
            int y)
        {
            if (cells == null)
            {
                return null;
            }

            foreach (BoardVisualCell cell in cells)
            {
                if (cell.X == x && cell.Y == y)
                {
                    return cell;
                }
            }

            return null;
        }

        private static bool CanDeployOnTerrain(PieceSnapshot piece, string terrain)
        {
            foreach (string deployableTerrain in piece.DeployableTerrains)
            {
                if (deployableTerrain == terrain)
                {
                    return true;
                }
            }

            return false;
        }

        private static string CalculateFacing(
            BoardPerspectiveProjector projector,
            BoardVisualCell cell,
            string fallback)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return fallback;
            }

            Vector3 mouseWorld = camera.ScreenToWorldPoint(Input.mousePosition);
            Vector3 center = projector.GetCellUnitAnchorWorld(cell, 0.5f);
            Vector2 delta = mouseWorld - center;
            if (delta.sqrMagnitude < 0.04f)
            {
                return fallback;
            }

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                return delta.x >= 0f ? "Right" : "Left";
            }

            return delta.y >= 0f ? "Up" : "Down";
        }

        private void RestoreDraggingPiece(
            MatchSceneContext context,
            BoardPieceView pieceView)
        {
            if (_draggingPieceInstanceId == 0)
            {
                return;
            }

            PieceSnapshot piece = FindPiece(context.Pieces, _draggingPieceInstanceId);
            pieceView.RestorePiecePreview(piece);
        }

        private static void SubmitPlacement(
            MatchSceneContext context,
            int pieceInstanceId,
            int cellId,
            string facing)
        {
            if (TrySendClientCommand(
                context,
                MatchCommand.PlacePiece(pieceInstanceId, cellId, facing)))
            {
                context.SelectPiece(0);
                return;
            }

            try
            {
                context.Runtime.PlacePiece(
                    context.LocalPlayerId,
                    pieceInstanceId,
                    cellId,
                    facing);
                // 位置与朝向提交成功后不再保持选中，避免信息面板立刻遮挡下一次操作。
                context.SelectPiece(0);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Piece placement was rejected by authority: {exception.Message}");
            }
        }

        private static void SubmitSell(
            MatchSceneContext context,
            int pieceInstanceId)
        {
            if (TrySendClientCommand(
                context,
                MatchCommand.SellPiece(pieceInstanceId)))
            {
                context.SelectPiece(0);
                return;
            }

            try
            {
                context.Runtime.SellPiece(context.LocalPlayerId, pieceInstanceId);
                context.SelectPiece(0);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Piece sale was rejected by authority: {exception.Message}");
            }
        }

        private static bool TrySendClientCommand(
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
                    $"Piece command {command.Type} was not sent; waiting for LAN match transport.");
            }

            // LAN 客户端只提交棋子操作意图，实际位置/出售结果等待 Host 快照确认。
            return true;
        }

        private bool TryFindSellDropZone(
            MatchSceneContext context,
            PieceSnapshot piece)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition,
            };
            _uiRaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, _uiRaycastResults);

            foreach (RaycastResult result in _uiRaycastResults)
            {
                MonoBehaviour[] behaviours =
                    result.gameObject.GetComponentsInParent<MonoBehaviour>(true);
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is IPieceSellDropZone dropZone
                        && dropZone.CanAcceptPieceSellDrop(context, piece))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsPointerOverUi(bool ignorePieceInspectPanel = false)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (!ignorePieceInspectPanel)
            {
                return eventSystem.IsPointerOverGameObject();
            }

            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition,
            };
            _uiRaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, _uiRaycastResults);

            foreach (RaycastResult result in _uiRaycastResults)
            {
                if (!IsInPieceInspectPanel(result.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInPieceInspectPanel(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            MonoBehaviour[] behaviours =
                target.GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour != null
                    && behaviour.GetType().Name == "UIPieceInspectPanel")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryPickCell(
            BoardCellPicker picker,
            out BoardVisualCell cell)
        {
            cell = null;
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            Vector3 world = camera.ScreenToWorldPoint(Input.mousePosition);
            return picker.TryPick(world, out cell);
        }

        private static Vector3 GetMouseWorldPosition()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return Vector3.zero;
            }

            Vector3 world = camera.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;
            return world;
        }

        private static bool TryPickPiece(
            BoardPieceView pieceView,
            out int pieceInstanceId)
        {
            pieceInstanceId = 0;
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            Vector3 world = camera.ScreenToWorldPoint(Input.mousePosition);
            return pieceView.TryPickPiece(world, out pieceInstanceId);
        }

        private static bool TryPickPieceWithExpandedBounds(
            BoardPieceView pieceView,
            int expectedPieceInstanceId)
        {
            if (TryPickPiece(pieceView, out int pieceInstanceId)
                && pieceInstanceId == expectedPieceInstanceId)
            {
                return true;
            }

            Camera camera = Camera.main;
            if (camera == null
                || !pieceView.TryGetPieceWorldBounds(
                    expectedPieceInstanceId,
                    out Bounds bounds))
            {
                return false;
            }

            bounds.Expand(FacingPickExpansionWorld * 2f);
            Vector3 world = camera.ScreenToWorldPoint(Input.mousePosition);
            world.z = bounds.center.z;
            return bounds.Contains(world);
        }

        private static PieceSnapshot FindPiece(
            PieceRosterSnapshot snapshot,
            int pieceInstanceId)
        {
            foreach (PieceSnapshot piece in snapshot.Pieces)
            {
                if (piece.InstanceId == pieceInstanceId)
                {
                    return piece;
                }
            }

            return null;
        }

        private static PieceSnapshot FindPieceAtCell(
            PieceRosterSnapshot snapshot,
            int playerId,
            int cellId)
        {
            foreach (PieceSnapshot piece in snapshot.Pieces)
            {
                if (piece.OwnerPlayerId == playerId && piece.CellId == cellId)
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
            foreach (PieceCapacitySnapshot capacity in snapshot.Players)
            {
                if (capacity.PlayerId == playerId)
                {
                    return capacity;
                }
            }

            return null;
        }
    }
}
