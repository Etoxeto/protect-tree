using System.Collections;
using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Board;
using UnityEngine;

namespace ProtectTree.Runtime
{
    public class BoardProjectilePool : MonoBehaviour
    {
        [SerializeField] private Transform projectileRoot;

        private readonly Dictionary<GameObject, Queue<BoardProjectileVisual>> _pooled =
            new Dictionary<GameObject, Queue<BoardProjectileVisual>>();
        private readonly Dictionary<BoardProjectileVisual, GameObject> _prefabsByInstance =
            new Dictionary<BoardProjectileVisual, GameObject>();
        private readonly List<BoardProjectileVisual> _instances =
            new List<BoardProjectileVisual>();

        public void Initialize(Transform root)
        {
            projectileRoot = root != null ? root : transform;
        }

        public BoardProjectileVisual Spawn(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            if (!_pooled.TryGetValue(prefab, out Queue<BoardProjectileVisual> queue))
            {
                queue = new Queue<BoardProjectileVisual>();
                _pooled.Add(prefab, queue);
            }

            BoardProjectileVisual visual =
                queue.Count > 0 ? queue.Dequeue() : CreateInstance(prefab);
            if (visual == null)
            {
                return null;
            }

            Transform root = projectileRoot != null ? projectileRoot : transform;
            visual.transform.SetParent(root, true);
            visual.gameObject.SetActive(true);
            return visual;
        }

        public void Recycle(BoardProjectileVisual visual)
        {
            if (visual == null)
            {
                return;
            }

            visual.gameObject.SetActive(false);

            if (!_prefabsByInstance.TryGetValue(visual, out GameObject prefab)
                || prefab == null)
            {
                Destroy(visual.gameObject);
                return;
            }

            if (!_pooled.TryGetValue(prefab, out Queue<BoardProjectileVisual> queue))
            {
                queue = new Queue<BoardProjectileVisual>();
                _pooled.Add(prefab, queue);
            }

            queue.Enqueue(visual);
        }

        public void Launch(
            BoardProjectileEntry entry,
            Vector3 startPosition,
            Vector3 targetPosition,
            int sortingOrder,
            float scaleMultiplier = 1f)
        {
            if (entry == null || entry.ProjectilePrefab == null)
            {
                return;
            }

            if (entry.FireDelaySeconds > 0f)
            {
                StartCoroutine(LaunchDelayed(
                    entry,
                    startPosition,
                    targetPosition,
                    sortingOrder,
                    scaleMultiplier));
                return;
            }

            LaunchNow(entry, startPosition, targetPosition, sortingOrder, scaleMultiplier);
        }

        public void Clear()
        {
            StopAllCoroutines();

            foreach (BoardProjectileVisual visual in _instances)
            {
                if (visual != null)
                {
                    DestroyRuntimeObject(visual.gameObject);
                }
            }

            _pooled.Clear();
            _prefabsByInstance.Clear();
            _instances.Clear();
        }

        private BoardProjectileVisual CreateInstance(GameObject prefab)
        {
            Transform root = projectileRoot != null ? projectileRoot : transform;
            GameObject instance = Instantiate(prefab, root, false);
            instance.name = prefab.name;

            BoardProjectileVisual visual =
                instance.GetComponent<BoardProjectileVisual>();
            if (visual == null)
            {
                visual = instance.AddComponent<BoardProjectileVisual>();
            }

            _prefabsByInstance[visual] = prefab;
            _instances.Add(visual);
            return visual;
        }

        private IEnumerator LaunchDelayed(
            BoardProjectileEntry entry,
            Vector3 startPosition,
            Vector3 targetPosition,
            int sortingOrder,
            float scaleMultiplier)
        {
            yield return new WaitForSeconds(entry.FireDelaySeconds);
            LaunchNow(entry, startPosition, targetPosition, sortingOrder, scaleMultiplier);
        }

        private void LaunchNow(
            BoardProjectileEntry entry,
            Vector3 startPosition,
            Vector3 targetPosition,
            int sortingOrder,
            float scaleMultiplier)
        {
            BoardProjectileVisual projectile = Spawn(entry.ProjectilePrefab);
            if (projectile == null)
            {
                return;
            }

            projectile.Launch(
                startPosition,
                targetPosition,
                entry.Speed,
                entry.ImpactHoldSeconds,
                entry.RotateToVelocity,
                sortingOrder,
                scaleMultiplier,
                this);
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
    }

    public sealed class BoardProjectilePresenter
    {
        private readonly HashSet<IReadOnlyList<MatchEvent>> _processedEventLists =
            new HashSet<IReadOnlyList<MatchEvent>>();

        private BoardProjectilePool _pool;
        private BoardProjectileCatalog _catalog;

        public void Initialize(
            Transform projectileRoot,
            BoardProjectileCatalog catalog,
            Component owner)
        {
            _catalog = catalog;

            if (projectileRoot == null)
            {
                _pool = null;
                return;
            }

            _pool = projectileRoot.GetComponent<BoardProjectilePool>();
            if (_pool == null)
            {
                _pool = projectileRoot.gameObject.AddComponent<BoardProjectilePool>();
            }

            _pool.Initialize(projectileRoot);
        }

        public void HandleEvents(
            IReadOnlyList<MatchEvent> events,
            PieceRosterSnapshot pieces,
            BoardPieceView pieceView,
            BoardEnemyView enemyView)
        {
            if (events == null
                || events.Count == 0
                || _pool == null
                || _catalog == null
                || pieceView == null
                || enemyView == null)
            {
                return;
            }

            if (_processedEventLists.Contains(events))
            {
                return;
            }

            _processedEventLists.Add(events);
            if (_processedEventLists.Count > 32)
            {
                _processedEventLists.Clear();
                _processedEventLists.Add(events);
            }

            foreach (MatchEvent matchEvent in events)
            {
                if (matchEvent.Type != "EnemyDamageRequested"
                    || !matchEvent.SourcePieceInstanceId.HasValue
                    || !matchEvent.EnemyInstanceId.HasValue)
                {
                    continue;
                }

                PieceSnapshot sourcePiece = FindPiece(
                    pieces,
                    matchEvent.SourcePieceInstanceId.Value);
                string projectileId = !string.IsNullOrEmpty(matchEvent.ProjectileId)
                    ? matchEvent.ProjectileId
                    : sourcePiece?.PieceId;
                if (sourcePiece == null
                    || !_catalog.TryGetEntry(
                        projectileId,
                        out BoardProjectileEntry entry)
                    || entry.ProjectilePrefab == null)
                {
                    continue;
                }

                if (!pieceView.TryGetPieceFirePoint(
                        matchEvent.SourcePieceInstanceId.Value,
                        out Vector3 startPosition,
                        out int sourceSortingOrder)
                    || !enemyView.TryGetEnemyHitPoint(
                        matchEvent.EnemyInstanceId.Value,
                        out Vector3 targetPosition,
                        out int targetSortingOrder))
                {
                    continue;
                }

                // 羁绊投射物使用羁绊层数决定尺寸；普通攻击没有对应羁绊时保持 catalog 默认尺寸。
                float projectileScale = entry.GetScale(GetProjectileLayerCount(
                    pieces,
                    sourcePiece,
                    projectileId));
                int sortingOrder = Mathf.Max(sourceSortingOrder, targetSortingOrder) + 2;
                _pool.Launch(
                    entry,
                    startPosition,
                    targetPosition,
                    sortingOrder,
                    projectileScale);
            }
        }

        public void Clear()
        {
            _processedEventLists.Clear();
            _pool?.Clear();
        }

        private static PieceSnapshot FindPiece(
            PieceRosterSnapshot pieces,
            int pieceInstanceId)
        {
            if (pieces == null)
            {
                return null;
            }

            foreach (PieceSnapshot piece in pieces.Pieces)
            {
                if (piece.InstanceId == pieceInstanceId)
                {
                    return piece;
                }
            }

            return null;
        }

        private static int GetProjectileLayerCount(
            PieceRosterSnapshot pieces,
            PieceSnapshot sourcePiece,
            string projectileId)
        {
            if (pieces == null
                || sourcePiece == null
                || string.IsNullOrEmpty(projectileId))
            {
                return 0;
            }

            foreach (ActiveSynergySnapshot synergy in pieces.ActiveSynergies)
            {
                if (synergy.PlayerId == sourcePiece.OwnerPlayerId
                    && synergy.SynergyId == projectileId)
                {
                    return synergy.LayerCount;
                }
            }

            return 0;
        }
    }
}
