using System.Collections.Generic;
using ProtectTree.Core.Match;
using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    /// <summary>
    /// 根据权威敌人快照，在当前观察玩家的正式棋盘路线中显示敌人。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardEnemyView : MonoBehaviour
    {
        private const float DeathPresentationSeconds = 0.8f;

        private static readonly Color NormalColor = new Color(0.95f, 0.3f, 0.25f);
        private static readonly Color BlockedColor = new Color(0.95f, 0.55f, 0.2f);
        private static readonly Color BossColor = new Color(0.7f, 0.25f, 0.95f);
        private static readonly Color HealthBackgroundColor =
            new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color HealthFillColor =
            new Color(0.2f, 0.95f, 0.25f);

        private readonly Dictionary<int, EnemyVisual> _visuals =
            new Dictionary<int, EnemyVisual>();
        private readonly Dictionary<int, BoardRouteSnapshot> _routes =
            new Dictionary<int, BoardRouteSnapshot>();

        private Transform _enemyRoot;
        private BoardVisualLayout _layout;
        private BoardPerspectiveProjector _projector;
        private BoardSortingPolicy _sorting;
        private BoardUnitVisualCatalog _unitVisualCatalog;
        private Texture2D _squareTexture;
        private Sprite _squareSprite;

        public void Initialize(
            Transform enemyRoot,
            BoardVisualLayout layout,
            IReadOnlyList<BoardRouteSnapshot> routes,
            BoardPerspectiveProjector projector,
            BoardSortingPolicy sorting,
            BoardUnitVisualCatalog unitVisualCatalog)
        {
            Clear();
            _enemyRoot = enemyRoot;
            _layout = layout;
            _projector = projector;
            _sorting = sorting;
            _unitVisualCatalog = unitVisualCatalog;

            _routes.Clear();
            if (routes == null)
            {
                return;
            }

            foreach (BoardRouteSnapshot route in routes)
            {
                _routes[route.RouteId] = route;
            }
        }

        public void Sync(EnemyRosterSnapshot snapshot, int observedPlayerId)
        {
            foreach (EnemyVisual visual in _visuals.Values)
            {
                visual.SeenThisFrame = false;
            }

            if (snapshot == null
                || observedPlayerId <= 0
                || _enemyRoot == null
                || _layout == null
                || _projector == null
                || _sorting == null)
            {
                return;
            }

            foreach (EnemySnapshot enemy in snapshot.Enemies)
            {
                if (enemy.TargetPlayerId != observedPlayerId)
                {
                    continue;
                }

                if (enemy.Status != "Alive")
                {
                    UpdateDefeatedVisual(enemy);
                    continue;
                }

                if (!_routes.TryGetValue(enemy.RouteId, out BoardRouteSnapshot route)
                    || !TrySampleRoute(
                        route,
                        enemy.PathProgress,
                        out Vector3 position,
                        out BoardVisualCell sortingCell))
                {
                    continue;
                }

                if (!_visuals.TryGetValue(enemy.InstanceId, out EnemyVisual visual))
                {
                    visual = CreateVisual(enemy);
                    _visuals.Add(enemy.InstanceId, visual);
                }

                visual.SeenThisFrame = true;
                visual.PreviousStatus = enemy.Status;
                visual.DeathSecondsRemaining = 0f;
                if (enemy.Health < visual.PreviousHealth)
                {
                    visual.HitFlashSeconds = 0.12f;
                }

                visual.PreviousHealth = enemy.Health;
                visual.HitFlashSeconds = Mathf.Max(
                    0f,
                    visual.HitFlashSeconds - Time.deltaTime);

                visual.Root.SetActive(true);
                visual.HealthBackground.gameObject.SetActive(true);
                visual.HealthFill.gameObject.SetActive(true);
                visual.Root.transform.position = position;
                visual.Root.transform.localScale =
                    enemy.IsBoss ? Vector3.one * 1.65f : Vector3.one;
                visual.UnitVisual?.SetMoving(
                    !enemy.BlockedByPieceInstanceId.HasValue && enemy.PathSpeed > 0d);
                if (visual.Body != null)
                {
                    visual.Body.color = visual.HitFlashSeconds > 0f
                        ? new Color(1f, 0.9f, 0.25f)
                        : enemy.IsBoss
                            ? BossColor
                            : enemy.BlockedByPieceInstanceId.HasValue
                                ? BlockedColor
                                : NormalColor;
                }

                float healthRatio = enemy.MaxHealth > 0
                    ? (float)enemy.Health / enemy.MaxHealth
                    : 0f;
                visual.HealthFill.transform.localScale =
                    new Vector3(0.74f * Mathf.Clamp01(healthRatio), 0.07f, 1f);
                SetSortingOrder(visual, _sorting.GetUnitOrder(sortingCell));
            }

            foreach (EnemyVisual visual in _visuals.Values)
            {
                if (!visual.SeenThisFrame)
                {
                    visual.Root.SetActive(false);
                }
            }
        }

        private void UpdateDefeatedVisual(EnemySnapshot enemy)
        {
            if (!_visuals.TryGetValue(enemy.InstanceId, out EnemyVisual visual))
            {
                return;
            }

            visual.SeenThisFrame = true;
            if (visual.PreviousStatus == "Alive")
            {
                visual.UnitVisual?.SetMoving(false);
                visual.UnitVisual?.TriggerDie();
                visual.DeathSecondsRemaining = DeathPresentationSeconds;
                visual.HealthBackground.gameObject.SetActive(false);
                visual.HealthFill.gameObject.SetActive(false);
            }

            visual.PreviousStatus = enemy.Status;
            visual.DeathSecondsRemaining = Mathf.Max(
                0f,
                visual.DeathSecondsRemaining - Time.deltaTime);
            visual.Root.SetActive(visual.DeathSecondsRemaining > 0f);
        }

        public void HandleEvents(IReadOnlyList<MatchEvent> events)
        {
            if (events == null)
            {
                return;
            }

            foreach (MatchEvent matchEvent in events)
            {
                if (matchEvent.Type == "PieceDamageRequested"
                    && matchEvent.SourceEnemyInstanceId.HasValue
                    && _visuals.TryGetValue(
                        matchEvent.SourceEnemyInstanceId.Value,
                        out EnemyVisual visual)
                    && visual.Root.activeInHierarchy)
                {
                    visual.UnitVisual?.TriggerAttack();
                }
            }
        }

        public void Clear()
        {
            foreach (EnemyVisual visual in _visuals.Values)
            {
                DestroyRuntimeObject(visual.Root);
            }

            _visuals.Clear();
            _routes.Clear();

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

        private bool TrySampleRoute(
            BoardRouteSnapshot route,
            double pathProgress,
            out Vector3 position,
            out BoardVisualCell sortingCell)
        {
            position = default;
            sortingCell = null;

            if (route?.Samples == null || route.Samples.Count == 0)
            {
                return false;
            }

            double clampedProgress = System.Math.Max(
                route.StartProgress,
                System.Math.Min(route.EndpointProgress, pathProgress));
            BoardRouteSampleSnapshot previous = route.Samples[0];

            for (int index = 1; index < route.Samples.Count; index++)
            {
                BoardRouteSampleSnapshot current = route.Samples[index];
                if (clampedProgress > current.PathProgress)
                {
                    previous = current;
                    continue;
                }

                BoardVisualCell previousCell = _layout.GetCell(previous.CellId);
                BoardVisualCell currentCell = _layout.GetCell(current.CellId);
                if (previousCell == null || currentCell == null)
                {
                    return false;
                }

                double segmentLength = current.PathProgress - previous.PathProgress;
                float segmentProgress = segmentLength > 0d
                    ? (float)((clampedProgress - previous.PathProgress) / segmentLength)
                    : 0f;
                position = Vector3.Lerp(
                    _projector.GetCellUnitAnchorWorld(previousCell, 0.5f),
                    _projector.GetCellUnitAnchorWorld(currentCell, 0.5f),
                    Mathf.Clamp01(segmentProgress));
                sortingCell = segmentProgress < 0.5f ? previousCell : currentCell;
                return true;
            }

            BoardVisualCell endpointCell =
                _layout.GetCell(route.Samples[route.Samples.Count - 1].CellId);
            if (endpointCell == null)
            {
                return false;
            }

            position = _projector.GetCellUnitAnchorWorld(endpointCell, 0.5f);
            sortingCell = endpointCell;
            return true;
        }

        private EnemyVisual CreateVisual(EnemySnapshot enemy)
        {
            EnsureSquareSprite();

            GameObject root = new GameObject($"BoardEnemy_{enemy.InstanceId}");
            root.transform.SetParent(_enemyRoot, true);

            BoardUnitVisualInstance unitVisual = BoardUnitVisualInstance.Create(
                _unitVisualCatalog?.GetEnemyPrefab(enemy.EnemyId),
                root.transform);

            SpriteRenderer body = unitVisual == null
                ? CreateSprite(
                    "Body",
                    root.transform,
                    NormalColor,
                    new Vector3(0.62f, 0.62f, 1f))
                : null;
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
                new Vector3(0f, 0.5f, 0f)) ?? new Vector3(0f, 0.5f, 0f);
            healthBackground.transform.localPosition = healthBarPosition;
            healthFill.transform.localPosition = healthBarPosition;

            return new EnemyVisual(
                root,
                unitVisual,
                body,
                healthBackground,
                healthFill,
                enemy.Health,
                enemy.Status);
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
                name = "BoardEnemyFallbackTexture",
            };
            _squareTexture.SetPixel(0, 0, Color.white);
            _squareTexture.Apply();

            _squareSprite = Sprite.Create(
                _squareTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            _squareSprite.name = "BoardEnemyFallbackSprite";
        }

        private static void SetSortingOrder(EnemyVisual visual, int unitOrder)
        {
            visual.UnitVisual?.SetSortingOrder(unitOrder + 1);
            if (visual.Body != null)
            {
                visual.Body.sortingOrder = unitOrder + 1;
            }

            visual.HealthBackground.sortingOrder = unitOrder + 3;
            visual.HealthFill.sortingOrder = unitOrder + 4;
        }

        private void OnDestroy()
        {
            Clear();
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

        private sealed class EnemyVisual
        {
            public EnemyVisual(
                GameObject root,
                BoardUnitVisualInstance unitVisual,
                SpriteRenderer body,
                SpriteRenderer healthBackground,
                SpriteRenderer healthFill,
                int initialHealth,
                string initialStatus)
            {
                Root = root;
                UnitVisual = unitVisual;
                Body = body;
                HealthBackground = healthBackground;
                HealthFill = healthFill;
                PreviousHealth = initialHealth;
                PreviousStatus = initialStatus;
            }

            public GameObject Root { get; }
            public BoardUnitVisualInstance UnitVisual { get; }
            public SpriteRenderer Body { get; }
            public SpriteRenderer HealthBackground { get; }
            public SpriteRenderer HealthFill { get; }
            public int PreviousHealth { get; set; }
            public string PreviousStatus { get; set; }
            public float HitFlashSeconds { get; set; }
            public float DeathSecondsRemaining { get; set; }
            public bool SeenThisFrame { get; set; }
        }
    }
}
