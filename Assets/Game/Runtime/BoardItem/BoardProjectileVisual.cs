using UnityEngine;
using UnityEngine.Rendering;

namespace ProtectTree.Runtime
{
    public class BoardProjectileVisual : MonoBehaviour
    {
        public float speed;
        public float impactHoldSeconds;
        public bool rotateToVelocity;

        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private float fallbackSpeed = 6f;

        private BoardProjectilePool _pool;
        private Vector3 _targetPosition;
        private float _activeSpeed;
        private float _holdSecondsRemaining;
        private bool _isFlying;
        private bool _isHoldingImpact;
        private bool _hasInitialLocalScale;
        private Vector3 _initialLocalScale;

        public void Launch(
            Vector3 startPosition,
            Vector3 targetPosition,
            float configuredSpeed,
            float configuredImpactHoldSeconds,
            bool? configuredRotateToVelocity,
            int sortingOrder,
            float scaleMultiplier,
            BoardProjectilePool pool)
        {
            if (!_hasInitialLocalScale)
            {
                _initialLocalScale = transform.localScale;
                _hasInitialLocalScale = true;
            }

            _pool = pool;
            _targetPosition = targetPosition;
            _activeSpeed = configuredSpeed > 0f
                ? configuredSpeed
                : speed > 0f ? speed : fallbackSpeed;
            _holdSecondsRemaining = Mathf.Max(
                0f,
                configuredImpactHoldSeconds > 0f
                    ? configuredImpactHoldSeconds
                    : impactHoldSeconds);
            _isFlying = true;
            _isHoldingImpact = false;

            gameObject.SetActive(true);
            transform.position = startPosition;
            transform.localScale = _initialLocalScale * Mathf.Max(0.01f, scaleMultiplier);
            SetSortingOrder(sortingOrder);
            UpdateRotation(targetPosition - startPosition, configuredRotateToVelocity);
        }

        private void Update()
        {
            if (_isFlying)
            {
                TickFlying();
                return;
            }

            if (_isHoldingImpact)
            {
                _holdSecondsRemaining -= Time.deltaTime;
                if (_holdSecondsRemaining <= 0f)
                {
                    Recycle();
                }
            }
        }

        private void TickFlying()
        {
            Vector3 current = transform.position;
            Vector3 delta = _targetPosition - current;
            float distance = delta.magnitude;
            float step = Mathf.Max(0.001f, _activeSpeed * Time.deltaTime);

            if (distance <= step)
            {
                transform.position = _targetPosition;
                Arrive();
                return;
            }

            transform.position = Vector3.MoveTowards(current, _targetPosition, step);
            UpdateRotation(delta, null);
        }

        private void Arrive()
        {
            _isFlying = false;

            if (impactEffectPrefab != null)
            {
                GameObject effect = Instantiate(
                    impactEffectPrefab,
                    transform.position,
                    Quaternion.identity,
                    transform.parent);
                Destroy(effect, 1.5f);
            }

            if (_holdSecondsRemaining <= 0f)
            {
                Recycle();
                return;
            }

            _isHoldingImpact = true;
        }

        private void Recycle()
        {
            _isFlying = false;
            _isHoldingImpact = false;
            _pool?.Recycle(this);
        }

        private void UpdateRotation(Vector3 direction, bool? configuredRotate)
        {
            bool shouldRotate = configuredRotate ?? rotateToVelocity;
            if (!shouldRotate || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void SetSortingOrder(int sortingOrder)
        {
            SortingGroup sortingGroup = GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder = sortingOrder;
            }

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingOrder = sortingOrder;
            }
        }
    }
}
