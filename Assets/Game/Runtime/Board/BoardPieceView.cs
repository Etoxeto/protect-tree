using System.Collections.Generic;
using ProtectTree.Core.Match;
using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    /// <summary>
    /// 根据权威棋子快照显示当前观察玩家的棋子，不负责部署和占用规则。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardPieceView : MonoBehaviour
    {
        private const int DragPreviewSortingOrder = 32000;

        private static readonly Color ActiveColor = new Color(0.25f, 0.9f, 0.4f);
        private static readonly Color DownedColor = new Color(0.35f, 0.4f, 0.45f);
        private static readonly Color SelectionColor = new Color(0.15f, 0.95f, 1f);
        private static readonly Color FacingColor = new Color(1f, 0.9f, 0.2f);
        private static readonly Color HealthBackgroundColor =
            new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color HealthFillColor =
            new Color(0.2f, 0.95f, 0.25f);

        private readonly Dictionary<int, PieceVisual> _visuals =
            new Dictionary<int, PieceVisual>();

        private Transform _pieceRoot;
        private BoardVisualLayout _layout;
        private BoardPerspectiveProjector _projector;
        private BoardSortingPolicy _sorting;
        private BoardUnitVisualCatalog _unitVisualCatalog;
        private Texture2D _squareTexture;
        private Sprite _squareSprite;

        public void Initialize(
            Transform pieceRoot,
            BoardVisualLayout layout,
            BoardPerspectiveProjector projector,
            BoardSortingPolicy sorting,
            BoardUnitVisualCatalog unitVisualCatalog)
        {
            Clear();
            _pieceRoot = pieceRoot;
            _layout = layout;
            _projector = projector;
            _sorting = sorting;
            _unitVisualCatalog = unitVisualCatalog;
        }

        public void Sync(
            PieceRosterSnapshot snapshot,
            int observedPlayerId,
            int selectedPieceInstanceId)
        {
            foreach (PieceVisual visual in _visuals.Values)
            {
                visual.SeenThisFrame = false;
            }

            if (snapshot == null
                || observedPlayerId <= 0
                || _pieceRoot == null
                || _layout == null
                || _projector == null
                || _sorting == null)
            {
                HideUnseenVisuals();
                return;
            }

            foreach (PieceSnapshot piece in snapshot.Pieces)
            {
                if (piece.OwnerPlayerId != observedPlayerId || !piece.CellId.HasValue)
                {
                    continue;
                }

                BoardVisualCell cell = _layout.GetCell(piece.CellId.Value);
                if (cell == null)
                {
                    continue;
                }

                if (!_visuals.TryGetValue(piece.InstanceId, out PieceVisual visual))
                {
                    visual = CreateVisual(piece);
                    _visuals.Add(piece.InstanceId, visual);
                }

                visual.SeenThisFrame = true;
                if (piece.Health < visual.PreviousHealth)
                {
                    visual.HitFlashSeconds = 0.12f;
                }

                visual.PreviousHealth = piece.Health;
                visual.HitFlashSeconds = Mathf.Max(
                    0f,
                    visual.HitFlashSeconds - Time.deltaTime);

                if (piece.Status == "Downed" && visual.PreviousStatus != "Downed")
                {
                    visual.UnitVisual?.TriggerDie();
                }
                else if (piece.Status == "Active" && visual.PreviousStatus == "Downed")
                {
                    visual.UnitVisual?.TriggerReborn();
                }

                int unitOrder = _sorting.GetUnitOrder(cell);
                visual.Root.SetActive(true);
                visual.Root.transform.position =
                    _projector.GetCellUnitAnchorWorld(cell, 0.5f);
                visual.UnitVisual?.SetFacing(piece.Facing);
                if (visual.Body != null)
                {
                    visual.Body.color = piece.Status == "Downed"
                        ? DownedColor
                        : visual.HitFlashSeconds > 0f ? Color.white : ActiveColor;
                }

                visual.Selection.gameObject.SetActive(
                    piece.InstanceId == selectedPieceInstanceId);
                visual.FacingRoot.localRotation = GetFacingRotation(piece.Facing);
                visual.PreviousStatus = piece.Status;

                float healthRatio = piece.MaxHealth > 0
                    ? (float)piece.Health / piece.MaxHealth
                    : 0f;
                visual.HealthFill.transform.localScale =
                    new Vector3(0.74f * Mathf.Clamp01(healthRatio), 0.07f, 1f);
                SetSortingOrder(visual, unitOrder);
            }

            HideUnseenVisuals();
        }

        private void HideUnseenVisuals()
        {
            foreach (PieceVisual visual in _visuals.Values)
            {
                if (!visual.SeenThisFrame)
                {
                    visual.Root.SetActive(false);
                }
            }
        }

        public void PreviewPiece(
            int pieceInstanceId,
            BoardVisualCell cell,
            string facing)
        {
            if (cell == null
                || !_visuals.TryGetValue(pieceInstanceId, out PieceVisual visual)
                || _projector == null
                || _sorting == null)
            {
                return;
            }

            visual.Root.transform.position =
                _projector.GetCellUnitAnchorWorld(cell, 0.5f);
            visual.UnitVisual?.SetFacing(facing);
            visual.FacingRoot.localRotation = GetFacingRotation(facing);
            SetSortingOrder(visual, _sorting.GetUnitOrder(cell));
        }

        public void PreviewPieceAtWorld(
            int pieceInstanceId,
            Vector3 worldPosition,
            string facing)
        {
            if (!_visuals.TryGetValue(pieceInstanceId, out PieceVisual visual))
            {
                return;
            }

            visual.Root.transform.position = worldPosition;
            visual.UnitVisual?.SetFacing(facing);
            visual.FacingRoot.localRotation = GetFacingRotation(facing);
            SetSortingOrder(visual, DragPreviewSortingOrder);
        }

        public void RestorePiecePreview(PieceSnapshot piece)
        {
            if (piece == null || !piece.CellId.HasValue || _layout == null)
            {
                return;
            }

            BoardVisualCell cell = _layout.GetCell(piece.CellId.Value);
            if (cell != null)
            {
                PreviewPiece(piece.InstanceId, cell, piece.Facing);
            }
        }

        public void HandleEvents(IReadOnlyList<MatchEvent> events)
        {
            if (events == null)
            {
                return;
            }

            foreach (MatchEvent matchEvent in events)
            {
                if (matchEvent.Type == "EnemyDamageRequested"
                    && matchEvent.SourcePieceInstanceId.HasValue
                    && _visuals.TryGetValue(
                        matchEvent.SourcePieceInstanceId.Value,
                        out PieceVisual visual)
                    && visual.Root.activeInHierarchy)
                {
                    visual.UnitVisual?.TriggerAttack();
                }
            }
        }

        public bool TryPickPiece(Vector2 worldPoint, out int pieceInstanceId)
        {
            pieceInstanceId = 0;
            int bestOrder = int.MinValue;

            foreach (KeyValuePair<int, PieceVisual> pair in _visuals)
            {
                PieceVisual visual = pair.Value;
                if (!visual.Root.activeInHierarchy
                    || !visual.ContainsPoint(worldPoint)
                    || visual.SortingOrder <= bestOrder)
                {
                    continue;
                }

                pieceInstanceId = pair.Key;
                bestOrder = visual.SortingOrder;
            }

            return pieceInstanceId != 0;
        }

        public bool TryGetPieceWorldBounds(int pieceInstanceId, out Bounds bounds)
        {
            bounds = default;

            if (!_visuals.TryGetValue(pieceInstanceId, out PieceVisual visual)
                || visual.Root == null
                || !visual.Root.activeInHierarchy)
            {
                return false;
            }

            if (visual.UnitVisual != null
                && visual.UnitVisual.TryGetWorldBounds(out bounds))
            {
                return true;
            }

            if (visual.Body != null)
            {
                bounds = visual.Body.bounds;
                return true;
            }

            return false;
        }

        public void Clear()
        {
            foreach (PieceVisual visual in _visuals.Values)
            {
                DestroyRuntimeObject(visual.Root);
            }

            _visuals.Clear();

            if (_squareSprite != null)
            {
                DestroyRuntimeObject(_squareSprite);
                _squareSprite = null;
            }

            if (_squareTexture != null)
            {
                DestroyRuntimeObject(_squareTexture);
                _squareTexture = null;
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        private PieceVisual CreateVisual(PieceSnapshot piece)
        {
            EnsureSquareSprite();

            GameObject root = new GameObject($"BoardPiece_{piece.InstanceId}");
            root.transform.SetParent(_pieceRoot, true);

            BoardUnitVisualInstance unitVisual = BoardUnitVisualInstance.Create(
                _unitVisualCatalog?.GetPiecePrefab(piece.PieceId),
                root.transform);

            SpriteRenderer selection = CreateSprite(
                "Selection",
                root.transform,
                SelectionColor,
                new Vector3(0.92f, 0.92f, 1f));
            selection.transform.localPosition = unitVisual?.GetSelectionLocalPosition(
                Vector3.zero) ?? Vector3.zero;

            GameObject facingRoot = new GameObject("Facing");
            facingRoot.transform.SetParent(root.transform, false);

            SpriteRenderer body = unitVisual == null
                ? CreateSprite(
                    "Body",
                    facingRoot.transform,
                    ActiveColor,
                    new Vector3(0.68f, 0.68f, 1f))
                : null;

            SpriteRenderer facing = CreateSprite(
                "FacingMarker",
                facingRoot.transform,
                FacingColor,
                new Vector3(0.3f, 0.2f, 1f));
            facing.transform.localPosition = new Vector3(0.48f, 0f, 0f);

            SpriteRenderer healthBackground = CreateSprite(
                "HealthBackground",
                root.transform,
                HealthBackgroundColor,
                new Vector3(0.78f, 0.09f, 1f));

            SpriteRenderer healthFill = CreateSprite(
                "HealthFill",
                root.transform,
                HealthFillColor,
                new Vector3(0.74f, 0.07f, 1f));
            Vector3 healthBarPosition = unitVisual?.GetHealthBarLocalPosition(
                new Vector3(0f, 0.52f, 0f)) ?? new Vector3(0f, 0.52f, 0f);
            healthBackground.transform.localPosition = healthBarPosition;
            healthFill.transform.localPosition = healthBarPosition;

            if (piece.Status == "Downed")
            {
                unitVisual?.TriggerDie();
            }

            selection.gameObject.SetActive(false);
            return new PieceVisual(
                root,
                unitVisual,
                selection,
                facingRoot.transform,
                body,
                facing,
                healthBackground,
                healthFill,
                piece.Health,
                piece.Status);
        }

        private SpriteRenderer CreateSprite(
            string objectName,
            Transform parent,
            Color color,
            Vector3 scale)
        {
            GameObject target = new GameObject(objectName);
            target.transform.SetParent(parent, false);
            target.transform.localScale = scale;

            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = _squareSprite;
            renderer.color = color;
            return renderer;
        }

        private void EnsureSquareSprite()
        {
            if (_squareSprite != null)
            {
                return;
            }

            _squareTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = "BoardPieceFallbackTexture",
            };
            _squareTexture.SetPixel(0, 0, Color.white);
            _squareTexture.Apply();

            _squareSprite = Sprite.Create(
                _squareTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            _squareSprite.name = "BoardPieceFallbackSprite";
        }

        private static void SetSortingOrder(PieceVisual visual, int unitOrder)
        {
            visual.Selection.sortingOrder = unitOrder;
            visual.UnitVisual?.SetSortingOrder(unitOrder + 1);
            if (visual.Body != null)
            {
                visual.Body.sortingOrder = unitOrder + 1;
            }

            visual.Facing.sortingOrder = unitOrder + 2;
            visual.HealthBackground.sortingOrder = unitOrder + 3;
            visual.HealthFill.sortingOrder = unitOrder + 4;
            visual.SortingOrder = unitOrder + 1;
        }

        private static Quaternion GetFacingRotation(string facing)
        {
            switch (facing)
            {
                case "Up":
                    return Quaternion.Euler(0f, 0f, 90f);
                case "Down":
                    return Quaternion.Euler(0f, 0f, -90f);
                case "Left":
                    return Quaternion.Euler(0f, 0f, 180f);
                default:
                    return Quaternion.identity;
            }
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private sealed class PieceVisual
        {
            public PieceVisual(
                GameObject root,
                BoardUnitVisualInstance unitVisual,
                SpriteRenderer selection,
                Transform facingRoot,
                SpriteRenderer body,
                SpriteRenderer facing,
                SpriteRenderer healthBackground,
                SpriteRenderer healthFill,
                int initialHealth,
                string initialStatus)
            {
                Root = root;
                UnitVisual = unitVisual;
                Selection = selection;
                FacingRoot = facingRoot;
                Body = body;
                Facing = facing;
                HealthBackground = healthBackground;
                HealthFill = healthFill;
                PreviousHealth = initialHealth;
                PreviousStatus = initialStatus;
            }

            public GameObject Root { get; }
            public BoardUnitVisualInstance UnitVisual { get; }
            public SpriteRenderer Selection { get; }
            public Transform FacingRoot { get; }
            public SpriteRenderer Body { get; }
            public SpriteRenderer Facing { get; }
            public SpriteRenderer HealthBackground { get; }
            public SpriteRenderer HealthFill { get; }
            public int PreviousHealth { get; set; }
            public string PreviousStatus { get; set; }
            public float HitFlashSeconds { get; set; }
            public int SortingOrder { get; set; }
            public bool SeenThisFrame { get; set; }

            public bool ContainsPoint(Vector2 worldPoint)
            {
                return UnitVisual != null
                    ? UnitVisual.ContainsPoint(worldPoint)
                    : Body != null && Body.bounds.Contains(worldPoint);
            }
        }
    }
}
