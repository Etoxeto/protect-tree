using UnityEngine;
using UnityEngine.Rendering;

namespace ProtectTree.Runtime.Board
{
    /// <summary>
    /// 封装一个角色美术 Prefab 实例，统一处理脚底对齐、动画、点击区域与棋盘排序。
    /// </summary>
    public sealed class BoardUnitVisualInstance
    {
        private static readonly int AttackTrigger = Animator.StringToHash("atk");
        private static readonly int DieTrigger = Animator.StringToHash("die");
        private static readonly int RebornTrigger = Animator.StringToHash("reborn");
        private static readonly int MoveParameter = Animator.StringToHash("move");

        private readonly Transform _container;
        private readonly Transform _facingVisualRoot;
        private readonly Transform _healthBarAnchor;
        private readonly Transform _selectionAnchor;
        private readonly Vector3 _facingVisualBaseScale;
        private readonly Animator _animator;
        private readonly Collider2D _hitArea;
        private SortingGroup _sortingGroup;

        private BoardUnitVisualInstance(
            Transform container,
            GameObject root,
            Transform facingVisualRoot,
            Transform healthBarAnchor,
            Transform selectionAnchor,
            Animator animator,
            Collider2D hitArea,
            SortingGroup sortingGroup)
        {
            _container = container;
            Root = root;
            _facingVisualRoot = facingVisualRoot;
            _healthBarAnchor = healthBarAnchor;
            _selectionAnchor = selectionAnchor;
            _facingVisualBaseScale = facingVisualRoot.localScale;
            _animator = animator;
            _hitArea = hitArea;
            _sortingGroup = sortingGroup;
        }

        public GameObject Root { get; }

        public static BoardUnitVisualInstance Create(
            GameObject prefab,
            Transform container)
        {
            if (prefab == null || container == null)
            {
                return null;
            }

            GameObject root = Object.Instantiate(prefab, container, false);
            root.name = "Visual";

            Transform groundAnchor =
                FindDescendant(root.transform, "GroundAnchor")
                ?? FindDescendant(root.transform, "GroudAnchor");
            if (groundAnchor != null)
            {
                // Prefab 可以保留自己的原始坐标与缩放，运行时只把脚底 Anchor 对齐到单位逻辑根节点。
                Vector3 groundInContainer = container.InverseTransformPoint(
                    groundAnchor.position);
                root.transform.localPosition -= groundInContainer;
            }
            else
            {
                Debug.LogWarning(
                    $"Unit visual prefab '{prefab.name}' has no GroundAnchor.",
                    prefab);
            }

            SortingGroup sortingGroup = GetOrAddSortingGroup(root);
            Transform facingVisualRoot =
                FindDescendant(root.transform, "Rig")
                ?? FindDescendant(root.transform, "PSBroot")
                ?? root.transform;
            Collider2D hitArea =
                FindDescendant(root.transform, "HitArea")?.GetComponent<Collider2D>();
            if (hitArea != null)
            {
                // HitArea 仅服务点击拾取，不参与未来可能加入的 2D 物理碰撞。
                hitArea.isTrigger = true;
            }

            return new BoardUnitVisualInstance(
                container,
                root,
                facingVisualRoot,
                FindDescendant(root.transform, "HealthBarAnchor"),
                FindDescendant(root.transform, "SelectionAnchor"),
                root.GetComponentInChildren<Animator>(true),
                hitArea,
                sortingGroup);
        }

        public Vector3 GetHealthBarLocalPosition(Vector3 fallback)
        {
            return GetAnchorLocalPosition(_healthBarAnchor, fallback);
        }

        public Vector3 GetSelectionLocalPosition(Vector3 fallback)
        {
            return GetAnchorLocalPosition(_selectionAnchor, fallback);
        }

        public bool ContainsPoint(Vector2 worldPoint)
        {
            return _hitArea != null && _hitArea.OverlapPoint(worldPoint);
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            foreach (Renderer renderer in Root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds && _hitArea != null)
            {
                bounds = _hitArea.bounds;
                hasBounds = true;
            }

            return hasBounds;
        }

        public void SetSortingOrder(int sortingOrder)
        {
            if (Root == null)
            {
                return;
            }

            // Unity 对象可能保留已失效的托管引用；设置排序前重新确认原生组件仍然存在。
            if (_sortingGroup == null)
            {
                _sortingGroup = GetOrAddSortingGroup(Root);
            }

            if (_sortingGroup != null)
            {
                _sortingGroup.sortingOrder = sortingOrder;
            }
        }

        public void SetFacing(string facing)
        {
            Vector3 scale = _facingVisualBaseScale;
            scale.x = Mathf.Abs(scale.x) * (facing == "Left" ? -1f : 1f);
            _facingVisualRoot.localScale = scale;
        }

        public void SetMoving(bool isMoving)
        {
            SetBoolIfPresent(MoveParameter, isMoving);
        }

        public void TriggerAttack()
        {
            SetTriggerIfPresent(AttackTrigger);
        }

        public void TriggerDie()
        {
            SetTriggerIfPresent(DieTrigger);
        }

        public void TriggerReborn()
        {
            SetTriggerIfPresent(RebornTrigger);
        }

        private Vector3 GetAnchorLocalPosition(Transform anchor, Vector3 fallback)
        {
            return anchor != null
                ? _container.InverseTransformPoint(anchor.position)
                : fallback;
        }

        private void SetBoolIfPresent(int parameterHash, bool value)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Bool))
            {
                _animator.SetBool(parameterHash, value);
            }
        }

        private void SetTriggerIfPresent(int parameterHash)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Trigger))
            {
                _animator.SetTrigger(parameterHash);
            }
        }

        private bool HasParameter(
            int parameterHash,
            AnimatorControllerParameterType parameterType)
        {
            if (_animator == null)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash == parameterHash && parameter.type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                {
                    return child;
                }
            }

            return null;
        }

        private static SortingGroup GetOrAddSortingGroup(GameObject target)
        {
            SortingGroup sortingGroup = target.GetComponent<SortingGroup>();
            if (sortingGroup == null)
            {
                sortingGroup = target.AddComponent<SortingGroup>();
            }

            return sortingGroup;
        }
    }
}
