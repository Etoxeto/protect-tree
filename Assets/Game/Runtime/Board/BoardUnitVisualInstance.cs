using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ProtectTree.Runtime;
using ProtectTree.Runtime.VFX;

namespace ProtectTree.Runtime.Board
{
    /// <summary>
    /// 封装一个角色美术 Prefab 实例，统一处理脚底对齐、动画、点击区域与棋盘排序。
    /// </summary>
    public sealed class BoardUnitVisualInstance
    {
        private static readonly int AttackTrigger = Animator.StringToHash("atk");
        private static readonly int BossAttackTrigger = Animator.StringToHash("atk_01");
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
        private readonly BoardUnitSocket _socket;
        private readonly BoardPieceLevelVfx _levelVfx;
        private readonly AnimationVfxTrigger _animationVfxTrigger;
        private readonly Dictionary<SpriteRenderer, Color> _baseSpriteColors =
            new Dictionary<SpriteRenderer, Color>();
        private SortingGroup _sortingGroup;
        private string _facing = "Right";
        private float _levelScaleMultiplier = 1f;
        private bool _isTinted;

        private BoardUnitVisualInstance(
            Transform container,
            GameObject root,
            Transform facingVisualRoot,
            Transform healthBarAnchor,
            Transform selectionAnchor,
            Animator animator,
            Collider2D hitArea,
            BoardUnitSocket socket,
            BoardPieceLevelVfx levelVfx,
            AnimationVfxTrigger animationVfxTrigger,
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
            _socket = socket;
            _levelVfx = levelVfx;
            _animationVfxTrigger = animationVfxTrigger;
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
            Transform healthBarAnchor = FindDescendant(root.transform, "HealthBarAnchor");
            Transform selectionAnchor = FindDescendant(root.transform, "SelectionAnchor");
            Collider2D hitArea =
                FindDescendant(root.transform, "HitArea")?.GetComponent<Collider2D>();
            if (hitArea != null)
            {
                // HitArea 仅服务点击拾取，不参与未来可能加入的 2D 物理碰撞。
                hitArea.isTrigger = true;
            }

            BoardPieceLevelVfx levelVfx = GetOrAddLevelVfx(root);
            levelVfx.Initialize(selectionAnchor, GetLevelVfxSizeHint(root, hitArea));

            return new BoardUnitVisualInstance(
                container,
                root,
                facingVisualRoot,
                healthBarAnchor,
                selectionAnchor,
                root.GetComponentInChildren<Animator>(true),
                hitArea,
                root.GetComponentInChildren<BoardUnitSocket>(true),
                levelVfx,
                root.GetComponentInChildren<AnimationVfxTrigger>(true),
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

        public Vector3 GetFirePointWorldPosition(Vector3 fallback)
        {
            return _socket != null
                ? _socket.GetFirePointWorldPosition(fallback)
                : fallback;
        }

        public Vector3 GetHitPointWorldPosition(Vector3 fallback)
        {
            return _socket != null
                ? _socket.GetHitPointWorldPosition(fallback)
                : fallback;
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

            _animationVfxTrigger?.SetSortingOrder(sortingOrder + 8);
        }

        public void SetFacing(string facing)
        {
            _facing = string.IsNullOrEmpty(facing) ? "Right" : facing;
            _animationVfxTrigger?.SetFacing(_facing);
            ApplyFacingAndLevelScale();
        }

        public void SetLevel(int level)
        {
            _levelScaleMultiplier = GetLevelScaleMultiplier(level);
            ApplyFacingAndLevelScale();
            _levelVfx?.SetLevel(level);
        }

        public void PlayMergeBurst(int level)
        {
            _levelVfx?.PlayMergeBurst(level);
        }

        public void SetMoving(bool isMoving)
        {
            SetBoolIfPresent(MoveParameter, isMoving);
        }

        public void SetAnimationSpeed(float speed)
        {
            if (_animator != null)
            {
                _animator.speed = Mathf.Max(0.01f, speed);
            }
        }

        public void SetEyesVisible(bool isVisible)
        {
            if (_socket?.Eyes == null)
            {
                return;
            }

            foreach (SpriteRenderer eye in _socket.Eyes)
            {
                if (eye != null)
                {
                    eye.gameObject.SetActive(isVisible);
                }
            }
        }

        public void SetTint(Color color)
        {
            CaptureTintTargets();

            foreach (KeyValuePair<SpriteRenderer, Color> entry in _baseSpriteColors)
            {
                if (entry.Key == null)
                {
                    continue;
                }

                Color tinted = color;
                tinted.a = entry.Value.a;
                entry.Key.color = tinted;
            }

            _isTinted = true;
        }

        public void ClearTint()
        {
            if (!_isTinted)
            {
                return;
            }

            foreach (KeyValuePair<SpriteRenderer, Color> entry in _baseSpriteColors)
            {
                if (entry.Key != null)
                {
                    entry.Key.color = entry.Value;
                }
            }

            _isTinted = false;
        }

        public void TriggerAttack()
        {
            if (!SetTriggerIfPresent(AttackTrigger))
            {
                SetTriggerIfPresent(BossAttackTrigger);
            }
        }

        public void TriggerDie()
        {
            SetTriggerIfPresent(DieTrigger);
        }

        public void TriggerReborn()
        {
            SetTriggerIfPresent(RebornTrigger);
        }

        public void TriggerAnimator(string triggerName)
        {
            if (!string.IsNullOrWhiteSpace(triggerName))
            {
                SetTriggerIfPresent(Animator.StringToHash(triggerName));
            }
        }

        public bool PlayAnimatorState(string stateName, float transitionSeconds = 0.05f)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            int stateHash = Animator.StringToHash(stateName);
            if (!_animator.HasState(0, stateHash))
            {
                return false;
            }

            if (transitionSeconds > 0f)
            {
                _animator.CrossFadeInFixedTime(stateHash, transitionSeconds, 0, 0f);
            }
            else
            {
                _animator.Play(stateHash, 0, 0f);
            }

            return true;
        }

        public void PlayTransferIn()
        {
            if (!PlayAnimatorState("transfer_in"))
            {
                TriggerAnimator("transfer_in");
            }
        }

        public void PlayTransferOut()
        {
            if (!PlayAnimatorState("transfer_out"))
            {
                TriggerAnimator("transfer_out");
            }
        }

        public void QueueExplosionMagicTarget(Vector3 worldPosition)
        {
            _animationVfxTrigger?.QueueExplosionMagicTarget(worldPosition);
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

        private bool SetTriggerIfPresent(int parameterHash)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Trigger))
            {
                _animator.SetTrigger(parameterHash);
                return true;
            }

            return false;
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

        private void ApplyFacingAndLevelScale()
        {
            Vector3 scale = _facingVisualBaseScale;
            scale.x = Mathf.Abs(scale.x)
                * (_facing == "Left" ? -1f : 1f)
                * _levelScaleMultiplier;
            scale.y *= _levelScaleMultiplier;
            _facingVisualRoot.localScale = scale;
        }

        private void CaptureTintTargets()
        {
            if (_baseSpriteColors.Count > 0 || _facingVisualRoot == null)
            {
                return;
            }

            // 只染色角色本体，不影响脚底光环、选中特效和血条等辅助表现。
            foreach (SpriteRenderer renderer in
                     _facingVisualRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer != null && !_baseSpriteColors.ContainsKey(renderer))
                {
                    _baseSpriteColors.Add(renderer, renderer.color);
                }
            }
        }

        private static float GetLevelScaleMultiplier(int level)
        {
            if (level >= 3)
            {
                return 1.16f;
            }

            return level >= 2 ? 1.1f : 1f;
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

        private static BoardPieceLevelVfx GetOrAddLevelVfx(GameObject target)
        {
            BoardPieceLevelVfx levelVfx =
                target.GetComponentInChildren<BoardPieceLevelVfx>(true);
            if (levelVfx == null)
            {
                levelVfx = target.AddComponent<BoardPieceLevelVfx>();
            }

            return levelVfx;
        }

        private static Vector2 GetLevelVfxSizeHint(
            GameObject root,
            Collider2D hitArea)
        {
            if (root == null || hitArea == null)
            {
                return Vector2.zero;
            }

            Bounds bounds = hitArea.bounds;
            Vector3 localMin = root.transform.InverseTransformPoint(bounds.min);
            Vector3 localMax = root.transform.InverseTransformPoint(bounds.max);
            return new Vector2(
                Mathf.Abs(localMax.x - localMin.x),
                Mathf.Abs(localMax.y - localMin.y));
        }
    }
}
