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
        private const float PositionSmoothSpeed = 18f;
        private const float PositionSnapDistance = 1.2f;
        private const float BlockedPresentationBackstepCells = 0.5f;
        private const float BossShootingAnimationLockSeconds = 4.05f;
        private const float BossExplosionAnimationLockSeconds = 3.25f;
        private const float BossTransferInAnimationLockSeconds = 0.75f;
        private const float BossTransferOutAnimationLockSeconds = 0.75f;

        private static readonly Color NormalColor = new Color(0.95f, 0.3f, 0.25f);
        private static readonly Color BlockedColor = new Color(0.95f, 0.55f, 0.2f);
        private static readonly Color BossColor = new Color(0.7f, 0.25f, 0.95f);
        private static readonly Color HitFlashColor = new Color(1f, 0.18f, 0.18f);
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
                visual.TransferPresentationSeconds = Mathf.Max(
                    0f,
                    visual.TransferPresentationSeconds - Time.deltaTime);
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
                        GetVisualPathProgress(enemy, route),
                        out Vector3 position,
                        out int sortingOrder))
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
                if (enemy.IsBoss)
                {
                    visual.Facing = GetHorizontalFacing(
                        route,
                        enemy.PathProgress,
                        visual.Facing);
                }
                if (enemy.Health < visual.PreviousHealth)
                {
                    visual.HitFlashSeconds = 0.12f;
                }

                visual.PreviousHealth = enemy.Health;
                visual.HitFlashSeconds = Mathf.Max(
                    0f,
                    visual.HitFlashSeconds - Time.deltaTime);
                visual.SkillAnimationLockSeconds = Mathf.Max(
                    0f,
                    visual.SkillAnimationLockSeconds - Time.deltaTime);

                visual.Root.SetActive(true);
                visual.HealthBackground.gameObject.SetActive(true);
                visual.HealthFill.gameObject.SetActive(true);
                if (visual.SkillAnimationLockSeconds <= 0f)
                {
                    visual.Root.transform.position =
                        SmoothPosition(visual, position);
                }
                visual.Root.transform.localScale =
                    enemy.IsBoss ? Vector3.one * 1.65f : Vector3.one;
                visual.UnitVisual?.SetMoving(
                    visual.SkillAnimationLockSeconds <= 0f
                    && !enemy.BlockedByPieceInstanceId.HasValue
                    && enemy.PathSpeed > 0d);
                if (enemy.IsBoss)
                {
                    visual.UnitVisual?.SetFacing(visual.Facing);
                }
                visual.UnitVisual?.SetEyesVisible(enemy.IsEnraged);
                visual.UnitVisual?.SetAnimationSpeed(enemy.IsEnraged ? 2f : 1f);
                ApplyHitFlash(visual);
                if (visual.Body != null)
                {
                    visual.Body.color = visual.HitFlashSeconds > 0f
                        ? HitFlashColor
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
                SetSortingOrder(visual, sortingOrder);
            }

            foreach (EnemyVisual visual in _visuals.Values)
            {
                if (!visual.SeenThisFrame)
                {
                    visual.UnitVisual?.ClearTint();
                    visual.HitFlashSeconds = 0f;
                    visual.Root.SetActive(visual.TransferPresentationSeconds > 0f);
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
                visual.UnitVisual?.ClearTint();
                visual.HitFlashSeconds = 0f;
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

        public void HandlePreSyncEvents(IReadOnlyList<MatchEvent> events)
        {
            if (events == null)
            {
                return;
            }

            foreach (MatchEvent matchEvent in events)
            {
                if (matchEvent.Type != "BossTransferRequested")
                {
                    continue;
                }

                int? sourceEnemyInstanceId =
                    matchEvent.SourceEnemyInstanceId ?? matchEvent.EnemyInstanceId;
                if (sourceEnemyInstanceId.HasValue
                    && _visuals.TryGetValue(
                        sourceEnemyInstanceId.Value,
                        out EnemyVisual visual)
                    && visual.Root.activeInHierarchy)
                {
                    visual.TransferPresentationSeconds =
                        BossTransferInAnimationLockSeconds;
                    visual.UnitVisual?.SetMoving(false);
                    visual.UnitVisual?.PlayTransferIn();
                }
            }
        }

        public void HandleEvents(
            IReadOnlyList<MatchEvent> events,
            BoardPieceView pieceView = null)
        {
            if (events == null)
            {
                return;
            }

            foreach (MatchEvent matchEvent in events)
            {
                if (matchEvent.Type == "BossSkillCast")
                {
                    int? sourceEnemyInstanceId =
                        matchEvent.SourceEnemyInstanceId ?? matchEvent.EnemyInstanceId;
                    if (sourceEnemyInstanceId.HasValue
                        && _visuals.TryGetValue(
                            sourceEnemyInstanceId.Value,
                            out EnemyVisual bossVisual)
                        && bossVisual.Root.activeInHierarchy)
                    {
                        Debug.Log(
                            "[ProtectTree][BossSkill] "
                            + $"projectile={matchEvent.ProjectileId}, "
                            + $"boss={sourceEnemyInstanceId.Value}, "
                            + $"targetPiece={matchEvent.PieceInstanceId ?? 0}.");
                        QueueBossSkillTarget(bossVisual, matchEvent, pieceView);
                        TriggerBossSkillAnimation(bossVisual, matchEvent);
                    }
                }
                else if (matchEvent.Type == "BossRetargeted")
                {
                    int? sourceEnemyInstanceId =
                        matchEvent.SourceEnemyInstanceId ?? matchEvent.EnemyInstanceId;
                    if (sourceEnemyInstanceId.HasValue
                        && _visuals.TryGetValue(
                            sourceEnemyInstanceId.Value,
                            out EnemyVisual transferVisual)
                        && transferVisual.Root.activeInHierarchy)
                    {
                        transferVisual.TransferPresentationSeconds =
                            BossTransferOutAnimationLockSeconds;
                        transferVisual.UnitVisual?.SetMoving(false);
                        transferVisual.UnitVisual?.PlayTransferOut();
                    }
                }
                else if (matchEvent.Type == "EnemyAttackStarted"
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

        private static void TriggerBossSkillAnimation(
            EnemyVisual visual,
            MatchEvent matchEvent)
        {
            if (visual.UnitVisual == null)
            {
                return;
            }

            switch (matchEvent.ProjectileId)
            {
                case "BossMagicShooting":
                    visual.SkillAnimationLockSeconds =
                        GetCastLockSeconds(
                            matchEvent,
                            BossShootingAnimationLockSeconds);
                    visual.UnitVisual.SetMoving(false);
                    if (!visual.UnitVisual.PlayAnimatorState("magic_02"))
                    {
                        visual.UnitVisual.TriggerAnimator("magic_02");
                    }
                    break;
                case "BossMagicExplosion":
                    visual.SkillAnimationLockSeconds =
                        GetCastLockSeconds(
                            matchEvent,
                            BossExplosionAnimationLockSeconds);
                    visual.UnitVisual.SetMoving(false);
                    if (!visual.UnitVisual.PlayAnimatorState("magic_01"))
                    {
                        visual.UnitVisual.TriggerAnimator("magic_01");
                    }
                    break;
                default:
                    visual.UnitVisual.TriggerAttack();
                    break;
            }
        }

        private static float GetCastLockSeconds(
            MatchEvent matchEvent,
            float fallbackSeconds)
        {
            if (matchEvent.CastLockSeconds.HasValue)
            {
                return Mathf.Max(0f, (float)matchEvent.CastLockSeconds.Value);
            }

            return fallbackSeconds;
        }

        private static void QueueBossSkillTarget(
            EnemyVisual visual,
            MatchEvent matchEvent,
            BoardPieceView pieceView)
        {
            if (visual?.UnitVisual == null
                || pieceView == null
                || matchEvent.ProjectileId != "BossMagicExplosion"
                || !matchEvent.PieceInstanceId.HasValue)
            {
                return;
            }

            if (pieceView.TryGetPieceGroundPosition(
                    matchEvent.PieceInstanceId.Value,
                    out Vector3 targetPosition,
                    out _))
            {
                visual.UnitVisual.QueueExplosionMagicTarget(targetPosition);
            }
        }

        public bool TryGetEnemyHitPoint(
            int enemyInstanceId,
            out Vector3 position,
            out int sortingOrder)
        {
            position = default;
            sortingOrder = 0;

            if (!_visuals.TryGetValue(enemyInstanceId, out EnemyVisual visual)
                || visual.Root == null
                || !visual.Root.activeInHierarchy)
            {
                return false;
            }

            position = visual.UnitVisual != null
                ? visual.UnitVisual.GetHitPointWorldPosition(visual.Root.transform.position)
                : visual.Root.transform.position;
            sortingOrder = visual.SortingOrder;
            return true;
        }

        private static void ApplyHitFlash(EnemyVisual visual)
        {
            if (visual.UnitVisual == null)
            {
                return;
            }

            if (visual.HitFlashSeconds > 0f)
            {
                visual.UnitVisual.SetTint(HitFlashColor);
            }
            else
            {
                visual.UnitVisual.ClearTint();
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
            out int sortingOrder)
        {
            position = default;
            sortingOrder = 0;

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
                sortingOrder = _sorting.GetRouteUnitOrder(previousCell, currentCell);
                return true;
            }

            BoardVisualCell endpointCell =
                _layout.GetCell(route.Samples[route.Samples.Count - 1].CellId);
            if (endpointCell == null)
            {
                return false;
            }

            position = _projector.GetCellUnitAnchorWorld(endpointCell, 0.5f);
            sortingOrder = _sorting.GetUnitOrder(endpointCell);
            return true;
        }

        private static double GetVisualPathProgress(
            EnemySnapshot enemy,
            BoardRouteSnapshot route)
        {
            if (enemy == null
                || route?.Samples == null
                || route.Samples.Count <= 1
                || !enemy.BlockedByPieceInstanceId.HasValue)
            {
                return enemy?.PathProgress ?? 0d;
            }

            if (!IsNearRouteSample(route, enemy.PathProgress))
            {
                return enemy.PathProgress;
            }

            double step = 1d / (route.Samples.Count - 1);
            return System.Math.Max(
                route.StartProgress,
                enemy.PathProgress - step * BlockedPresentationBackstepCells);
        }

        private static bool IsNearRouteSample(
            BoardRouteSnapshot route,
            double pathProgress)
        {
            const double epsilon = 0.000001d;

            foreach (BoardRouteSampleSnapshot sample in route.Samples)
            {
                if (System.Math.Abs(sample.PathProgress - pathProgress) <= epsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetHorizontalFacing(
            BoardRouteSnapshot route,
            double pathProgress,
            string fallback)
        {
            if (route?.Samples == null || route.Samples.Count < 2)
            {
                return string.IsNullOrEmpty(fallback) ? "Right" : fallback;
            }

            int nearestIndex = 0;
            double nearestDistance = double.MaxValue;
            for (int index = 0; index < route.Samples.Count; index++)
            {
                double distance = System.Math.Abs(
                    route.Samples[index].PathProgress - pathProgress);
                if (distance < nearestDistance)
                {
                    nearestIndex = index;
                    nearestDistance = distance;
                }
            }

            for (int index = nearestIndex + 1; index < route.Samples.Count; index++)
            {
                int deltaX = route.Samples[index].GridX
                    - route.Samples[nearestIndex].GridX;
                if (deltaX != 0)
                {
                    return deltaX > 0 ? "Right" : "Left";
                }
            }

            for (int index = nearestIndex - 1; index >= 0; index--)
            {
                int deltaX = route.Samples[nearestIndex].GridX
                    - route.Samples[index].GridX;
                if (deltaX != 0)
                {
                    return deltaX > 0 ? "Right" : "Left";
                }
            }

            return string.IsNullOrEmpty(fallback) ? "Right" : fallback;
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

        private static Vector3 SmoothPosition(
            EnemyVisual visual,
            Vector3 targetPosition)
        {
            if (!visual.HasPresentedPosition)
            {
                visual.HasPresentedPosition = true;
                visual.PresentedPosition = targetPosition;
                return targetPosition;
            }

            float distance = Vector3.Distance(
                visual.PresentedPosition,
                targetPosition);
            if (distance >= PositionSnapDistance)
            {
                // 切换观察目标或远距离校正时直接吸附，避免敌人从旧位置横穿棋盘。
                visual.PresentedPosition = targetPosition;
                return targetPosition;
            }

            float t = 1f - Mathf.Exp(-PositionSmoothSpeed * Time.deltaTime);
            visual.PresentedPosition = Vector3.Lerp(
                visual.PresentedPosition,
                targetPosition,
                t);
            return visual.PresentedPosition;
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
            visual.SortingOrder = unitOrder + 1;
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
            public string Facing { get; set; } = "Right";
            public float HitFlashSeconds { get; set; }
            public float SkillAnimationLockSeconds { get; set; }
            public float TransferPresentationSeconds { get; set; }
            public float DeathSecondsRemaining { get; set; }
            public int SortingOrder { get; set; }
            public bool SeenThisFrame { get; set; }
            public bool HasPresentedPosition { get; set; }
            public Vector3 PresentedPosition { get; set; }
        }
    }
}
