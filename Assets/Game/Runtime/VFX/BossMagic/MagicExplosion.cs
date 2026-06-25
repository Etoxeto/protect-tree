using UnityEngine;

namespace ProtectTree.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MagicExplosion : MonoBehaviour
    {
        [SerializeField] private float defaultLifetimeSeconds = 1.2f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool autoDestroy = true;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 3200;

        private Animator _animator;
        private float _remainingSeconds;
        private bool _isPlaying;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            ApplySorting();
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                Play(defaultLifetimeSeconds, autoDestroy);
            }
        }

        private void OnValidate()
        {
            defaultLifetimeSeconds = Mathf.Max(0.05f, defaultLifetimeSeconds);
        }

        public void Play(float lifetimeSeconds, bool shouldAutoDestroy)
        {
            _remainingSeconds = Mathf.Max(0.05f, lifetimeSeconds);
            autoDestroy = shouldAutoDestroy;
            _isPlaying = true;

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_animator != null)
            {
                _animator.Rebind();
                _animator.Update(0f);
            }

            ApplySorting();
        }

        public void SetSorting(string layerName, int order)
        {
            if (!string.IsNullOrWhiteSpace(layerName))
            {
                sortingLayerName = layerName;
            }

            sortingOrder = order;
            ApplySorting();
        }

        private void ApplySorting()
        {
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sortingLayerName = sortingLayerName;
                renderer.sortingOrder = sortingOrder;
            }
        }

        private void Update()
        {
            if (!_isPlaying)
            {
                return;
            }

            _remainingSeconds -= Time.deltaTime;
            if (_remainingSeconds > 0f)
            {
                return;
            }

            _isPlaying = false;
            if (autoDestroy)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
