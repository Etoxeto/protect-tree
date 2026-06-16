using System;
using System.Collections.Generic;
using ProtectTree.Core.Match;
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

        private int _pressedPieceInstanceId;
        private Vector2 _pressScreenPosition;
        private int _draggingPieceInstanceId;
        private int _pendingPieceInstanceId;
        private BoardVisualCell _pendingCell;
        private string _pendingFacing;
        private bool _isChoosingFacing;
        private readonly List<RaycastResult> _uiRaycastResults =
            new List<RaycastResult>();
        private readonly List<BoardVisualCell> _legalPlacementCells =
            new List<BoardVisualCell>();

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
            _pendingCell = null;
            _pendingFacing = null;
            _isChoosingFacing = false;
            highlightView?.SetVisible(false);
            highlightView?.ClearPlacementCells();
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
            Reset(highlightView);

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (IsPointerOverUi())
            {
                context.SelectPiece(0);
                return;
            }

            if (TryPickPiece(pieceView, out int clickedPieceInstanceId))
            {
                context.SelectPiece(clickedPieceInstanceId);
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
                    context.SelectPiece(0);
                    highlightView.SetVisible(false);
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
                    return;
                }

                Vector2 dragDelta = (Vector2)Input.mousePosition - _pressScreenPosition;
                if (dragDelta.magnitude < DragStartThresholdPixels)
                {
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
            }
            else
            {
                highlightView.SetVisible(false);
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
            _pendingCell = targetCell;
            _pendingFacing = draggingPiece.Facing;
            highlightView.ClearPlacementCells();
            highlightView.Show(targetCell);
        }

        private void UpdateFacingConfirmation(
            MatchSceneContext context,
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
            highlightView.Show(_pendingCell);

            if (Input.GetMouseButtonDown(0))
            {
                _isChoosingFacing =
                    TryPickPiece(pieceView, out int clickedPieceInstanceId)
                    && clickedPieceInstanceId == _pendingPieceInstanceId;

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
            if (piece.CellId == targetCell.CellId
                || FindPieceAtCell(
                    context.Pieces,
                    context.LocalPlayerId,
                    targetCell.CellId) != null)
            {
                return false;
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
