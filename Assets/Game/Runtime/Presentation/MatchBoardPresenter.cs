using ProtectTree.Core.Match;
using ProtectTree.Runtime;
using ProtectTree.Runtime.Board;
using ProtectTree.Runtime.Lua;
using UnityEngine;

namespace ProtectTree.Runtime.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ObservedBoardView))]
    public sealed class MatchBoardPresenter : MatchSceneFeature
    {
        [Header("Board View")]
        [SerializeField] private ObservedBoardView observedBoardView;
        [SerializeField] private Transform highlightRoot;
        [SerializeField] private Transform projectileRoot;
        [SerializeField] private BoardVisualDefinition visualDefinition;
        [SerializeField] private BoardUnitVisualCatalog unitVisualCatalog;
        [SerializeField] private BoardProjectileCatalog projectileCatalog;

        [Header("Projection")]
        [SerializeField] private float tileWidth = 1.6f;
        [SerializeField] private float tileDepth = 0.8f;
        [SerializeField] private float heightStep = 0.35f;
        [SerializeField] private float frontRowScale = 1f;
        [SerializeField] private float backRowScale = 0.82f;
        [Tooltip("Extra visual depth below the y=0 front faces, measured in height steps.")]
        [Min(0f)]
        [SerializeField] private float frontEdgeExtraDepth = 1.5f;

        [Header("Selection")]
        [SerializeField] private bool enablePicking = true;
        [SerializeField] private float selectedOutlineWidth = 0.045f;

        [Header("Camera Framing")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private RectTransform pieceInspectPanelRect;
        [SerializeField] private string pieceInspectPanelObjectName = "UIPieceInspectPanel";
        [SerializeField] private float cameraSmoothTime = 0.25f;
        [SerializeField] private float selectedReserveCameraX = -4f;
        [SerializeField] private float selectedPieceNoPanBoardX = 5f;
        [SerializeField] private float selectedPieceScreenPadding = 48f;
        [SerializeField] private float fallbackInspectPanelScreenWidth = 360f;
        [SerializeField] private float battleCameraY = 3.5f;
        [SerializeField] private float battleOrthographicSize = 4f;

        [Header("Routes")]
        [SerializeField] private bool showRouteDebugLines;
        [SerializeField] private float routeLineWidth = 0.075f;
        [SerializeField] private float sharedRouteSeparation = 0.14f;

        private BoardSnapshot _builtSnapshot;
        private BoardVisualLayout _layout;
        private BoardPerspectiveProjector _projector;
        private BoardCellPicker _picker;
        private BoardStaticView _staticView;
        private BoardCellHighlightView _highlightView;
        private BoardRouteView _routeView;
        private BoardPieceView _pieceView;
        private BoardEnemyView _enemyView;
        private BoardProjectilePresenter _projectilePresenter;
        private readonly MatchBoardInteraction _interaction =
            new MatchBoardInteraction();
        private bool _hasInitialCameraState;
        private Vector3 _initialCameraPosition;
        private float _initialCameraSize;
        private Vector3 _cameraVelocity;
        private float _cameraSizeVelocity;
        private readonly Vector3[] _screenRectCorners = new Vector3[4];

        public override int RefreshOrder => -100;

        private void Awake()
        {
            if (observedBoardView == null)
            {
                observedBoardView = GetComponent<ObservedBoardView>();
            }
        }

        public override void OnRuntimeChanged(LuaRuntime runtime)
        {
            ClearBoard();
        }

        public override void Refresh(MatchSceneContext context)
        {
            if (context?.Board == null)
            {
                return;
            }

            if (context.Flow == null || context.Flow.IsFinished)
            {
                context.SelectPiece(0);
                context.SetPieceInspectBlocked(false);
            }

            if (!ReferenceEquals(_builtSnapshot, context.Board))
            {
                BuildBoard(context.Board);
            }

            if (context.ObservedPlayerId > 0
                && observedBoardView.ObservedPlayerId != context.ObservedPlayerId)
            {
                observedBoardView.SetObservedPlayer(context.ObservedPlayerId);
            }
            else if (observedBoardView.ObservedPlayerId <= 0)
            {
                observedBoardView.SetObservedPlayer(context.LocalPlayerId);
            }

            _pieceView?.Sync(
                context.Pieces,
                observedBoardView.ObservedPlayerId,
                context.SelectedPieceInstanceId);
            _enemyView?.HandlePreSyncEvents(context.Events);
            _enemyView?.Sync(
                context.Enemies,
                observedBoardView.ObservedPlayerId);
            _pieceView?.HandleEvents(context.Events);
            _enemyView?.HandleEvents(context.Events, _pieceView);
            _projectilePresenter?.HandleEvents(
                context.Events,
                context.Pieces,
                _pieceView,
                _enemyView);

            if (enablePicking)
            {
                _interaction.Update(
                    context,
                    observedBoardView.ObservedPlayerId,
                    _picker,
                    _projector,
                    _pieceView,
                    _highlightView);
            }

            UpdateCameraFraming(context);
        }

        public override void OnRuntimeUnavailable()
        {
            ClearBoard();
        }

        private void BuildBoard(BoardSnapshot snapshot)
        {
            Transform staticBoardRoot = observedBoardView?.StaticBoardRoot;
            Transform routeRoot = observedBoardView?.RouteRoot;
            Transform pieceRoot = observedBoardView?.PieceRoot;
            Transform enemyRoot = observedBoardView?.EnemyRoot;
            Transform resolvedProjectileRoot =
                projectileRoot != null ? projectileRoot : observedBoardView?.EffectRoot;
            if (staticBoardRoot == null
                || routeRoot == null
                || pieceRoot == null
                || enemyRoot == null
                || highlightRoot == null)
            {
                Debug.LogError(
                    "MatchBoardPresenter requires ObservedBoardView static, route, piece, "
                    + "and enemy roots plus a highlight root.",
                    this);
                return;
            }

            // 权威快照决定格子身份与地形；此组件只生成当前客户端看到的静态棋盘。
            _layout = BoardVisualLayoutConverter.Create(snapshot);
            _projector = new BoardPerspectiveProjector(
                transform,
                _layout.Width,
                _layout.Height,
                tileWidth,
                tileDepth,
                heightStep,
                frontRowScale,
                backRowScale);
            BoardSortingPolicy sorting = new BoardSortingPolicy(_layout.Height);

            _staticView = GetOrAddComponent<BoardStaticView>();
            _staticView.Build(
                staticBoardRoot,
                visualDefinition,
                _layout,
                _projector,
                sorting,
                frontEdgeExtraDepth);

            _routeView = GetOrAddComponent<BoardRouteView>();
            if (showRouteDebugLines)
            {
                _routeView.Build(
                    routeRoot,
                    _layout,
                    snapshot.Routes,
                    _projector,
                    sorting,
                    routeLineWidth,
                    sharedRouteSeparation);
            }
            else
            {
                // 路线数据仍供权威玩法和敌人表现使用；正式游戏只隐藏调试线条。
                _routeView.Clear();
            }

            _highlightView = GetOrAddComponent<BoardCellHighlightView>();
            _highlightView.Initialize(
                highlightRoot,
                visualDefinition,
                _projector,
                sorting,
                selectedOutlineWidth);

            _pieceView = GetOrAddComponent<BoardPieceView>();
            BoardUnitVisualCatalog resolvedUnitVisualCatalog =
                unitVisualCatalog != null
                    ? unitVisualCatalog
                    : Resources.Load<BoardUnitVisualCatalog>(
                        "Board/DefaultUnitVisualCatalog");
            _pieceView.Initialize(
                pieceRoot,
                _layout,
                _projector,
                sorting,
                resolvedUnitVisualCatalog);

            _enemyView = GetOrAddComponent<BoardEnemyView>();
            _enemyView.Initialize(
                enemyRoot,
                _layout,
                snapshot.Routes,
                _projector,
                sorting,
                resolvedUnitVisualCatalog);

            BoardProjectileCatalog resolvedProjectileCatalog =
                projectileCatalog != null
                    ? projectileCatalog
                    : Resources.Load<BoardProjectileCatalog>(
                        "Board/DefaultProjectileCatalog");
            _projectilePresenter = new BoardProjectilePresenter();
            _projectilePresenter.Initialize(
                resolvedProjectileRoot,
                resolvedProjectileCatalog,
                this);

            _picker = new BoardCellPicker(_layout, _projector, sorting);
            _builtSnapshot = snapshot;
        }

        private void ClearBoard()
        {
            RestoreInitialCameraImmediate();
            _staticView?.Clear();
            _routeView?.Clear();
            _highlightView?.Clear();
            _pieceView?.Clear();
            _enemyView?.Clear();
            _projectilePresenter?.Clear();
            _interaction.Reset(_highlightView);
            _builtSnapshot = null;
            _layout = null;
            _projector = null;
            _picker = null;
            _projectilePresenter = null;
        }

        private void UpdateCameraFraming(MatchSceneContext context)
        {
            Camera camera = ResolveTargetCamera();
            if (camera == null)
            {
                return;
            }

            CaptureInitialCameraState(camera);

            Vector3 targetPosition = _initialCameraPosition;
            float targetSize = _initialCameraSize;

            if (IsBattlePhase(context.Flow))
            {
                // 战斗阶段禁用棋子和商店操作，镜头临时拉近战场，回合结束后自动回到初始状态。
                targetPosition.y = battleCameraY;
                targetSize = battleOrthographicSize;
            }
            else if (!context.IsPieceInspectBlocked
                && TryCalculateInspectCameraTarget(
                         context,
                         camera,
                         targetSize,
                         out Vector3 inspectTargetPosition))
            {
                targetPosition = inspectTargetPosition;
            }

            MoveCamera(camera, targetPosition, targetSize);
        }

        private Camera ResolveTargetCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            targetCamera = Camera.main;
            return targetCamera;
        }

        private void CaptureInitialCameraState(Camera camera)
        {
            if (_hasInitialCameraState)
            {
                return;
            }

            _initialCameraPosition = camera.transform.position;
            _initialCameraSize = camera.orthographicSize;
            _hasInitialCameraState = true;
        }

        private void MoveCamera(
            Camera camera,
            Vector3 targetPosition,
            float targetSize)
        {
            if (cameraSmoothTime <= 0f)
            {
                camera.transform.position = targetPosition;
                if (camera.orthographic)
                {
                    camera.orthographicSize = targetSize;
                }

                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            camera.transform.position = Vector3.SmoothDamp(
                camera.transform.position,
                targetPosition,
                ref _cameraVelocity,
                cameraSmoothTime,
                Mathf.Infinity,
                deltaTime);

            if (camera.orthographic)
            {
                camera.orthographicSize = Mathf.SmoothDamp(
                    camera.orthographicSize,
                    targetSize,
                    ref _cameraSizeVelocity,
                    cameraSmoothTime,
                    Mathf.Infinity,
                    deltaTime);
            }
        }

        private void RestoreInitialCameraImmediate()
        {
            if (!_hasInitialCameraState)
            {
                return;
            }

            Camera camera = ResolveTargetCamera();
            if (camera == null)
            {
                return;
            }

            _cameraVelocity = Vector3.zero;
            _cameraSizeVelocity = 0f;
            camera.transform.position = _initialCameraPosition;
            if (camera.orthographic)
            {
                camera.orthographicSize = _initialCameraSize;
            }
        }

        private bool TryCalculateInspectCameraTarget(
            MatchSceneContext context,
            Camera camera,
            float targetSize,
            out Vector3 targetPosition)
        {
            targetPosition = _initialCameraPosition;

            if (context.SelectedPieceInstanceId <= 0
                || context.Pieces == null
                || _layout == null
                || _projector == null
                || _pieceView == null)
            {
                return false;
            }

            PieceSnapshot selectedPiece = FindPiece(
                context.Pieces,
                context.SelectedPieceInstanceId);
            if (selectedPiece == null
                || selectedPiece.OwnerPlayerId != context.LocalPlayerId
                || !selectedPiece.CellId.HasValue)
            {
                return false;
            }

            BoardVisualCell cell = _layout.GetCell(selectedPiece.CellId.Value);
            if (cell == null)
            {
                return false;
            }

            Bounds pieceBounds = GetSelectedPieceBounds(selectedPiece, cell);
            float targetX = _initialCameraPosition.x;

            if (selectedPieceNoPanBoardX > 0f && cell.X < selectedPieceNoPanBoardX)
            {
                // x=0 的最左侧棋子按用户实测至少移动到 -4；越接近 x=5，镜头偏移越少。
                float t = Mathf.Clamp01(cell.X / selectedPieceNoPanBoardX);
                float calibratedX = Mathf.Lerp(
                    selectedReserveCameraX,
                    _initialCameraPosition.x,
                    t);
                targetX = Mathf.Min(targetX, calibratedX);
            }

            if (camera.orthographic)
            {
                float safeScreenX = GetInspectPanelSafeScreenX()
                    + selectedPieceScreenPadding;
                float requiredX = CalculateCameraXForBoundsLeft(
                    camera,
                    pieceBounds,
                    targetSize,
                    safeScreenX);
                targetX = Mathf.Min(targetX, requiredX);
            }

            targetPosition.x = targetX;
            return true;
        }

        private Bounds GetSelectedPieceBounds(
            PieceSnapshot selectedPiece,
            BoardVisualCell cell)
        {
            if (_pieceView.TryGetPieceWorldBounds(
                    selectedPiece.InstanceId,
                    out Bounds bounds))
            {
                return bounds;
            }

            Vector3 anchor = _projector.GetCellUnitAnchorWorld(cell, 0.5f);
            return new Bounds(anchor, new Vector3(0.9f, 1.2f, 0.1f));
        }

        private float GetInspectPanelSafeScreenX()
        {
            if (TryGetInspectPanelScreenRect(out Rect panelRect))
            {
                return panelRect.xMax;
            }

            return Mathf.Max(0f, fallbackInspectPanelScreenWidth);
        }

        private bool TryGetInspectPanelScreenRect(out Rect screenRect)
        {
            screenRect = default;
            RectTransform rectTransform = ResolvePieceInspectPanelRect();
            if (rectTransform == null)
            {
                return false;
            }

            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null
                && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            rectTransform.GetWorldCorners(_screenRectCorners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                _screenRectCorners[0]);
            Vector2 max = min;
            for (int index = 1; index < _screenRectCorners.Length; index++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                    uiCamera,
                    _screenRectCorners[index]);
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return screenRect.width > 0f && screenRect.height > 0f;
        }

        private RectTransform ResolvePieceInspectPanelRect()
        {
            if (pieceInspectPanelRect != null)
            {
                return pieceInspectPanelRect;
            }

            foreach (RectTransform rectTransform
                in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (rectTransform == null
                    || rectTransform.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                bool hasInspectPanelScript =
                    rectTransform.GetComponent("UIPieceInspectPanel") != null;
                bool nameMatches = !string.IsNullOrEmpty(pieceInspectPanelObjectName)
                    && rectTransform.name == pieceInspectPanelObjectName;
                if (hasInspectPanelScript || nameMatches)
                {
                    pieceInspectPanelRect = rectTransform;
                    return pieceInspectPanelRect;
                }
            }

            return null;
        }

        private static float CalculateCameraXForBoundsLeft(
            Camera camera,
            Bounds bounds,
            float orthographicSize,
            float screenX)
        {
            if (Screen.width <= 0)
            {
                return camera.transform.position.x;
            }

            float halfWidth = orthographicSize * camera.aspect;
            float normalizedScreenX = Mathf.Clamp01(screenX / Screen.width);
            float worldOffsetFromCamera =
                Mathf.Lerp(-halfWidth, halfWidth, normalizedScreenX);
            return bounds.min.x - worldOffsetFromCamera;
        }

        private static bool IsBattlePhase(MatchFlowSnapshot flow)
        {
            return flow != null
                && (flow.Phase == "Battle"
                    || flow.Phase == "JointDefenseIntro"
                    || flow.Phase == "JointDefense"
                    || flow.Phase == "BossBattle");
        }

        private static PieceSnapshot FindPiece(
            PieceRosterSnapshot roster,
            int instanceId)
        {
            foreach (PieceSnapshot piece in roster.Pieces)
            {
                if (piece.InstanceId == instanceId)
                {
                    return piece;
                }
            }

            return null;
        }

        private static BoardCellSnapshot FindCell(BoardSnapshot board, int cellId)
        {
            foreach (BoardCellSnapshot cell in board.Cells)
            {
                if (cell.CellId == cellId)
                {
                    return cell;
                }
            }

            return null;
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}
