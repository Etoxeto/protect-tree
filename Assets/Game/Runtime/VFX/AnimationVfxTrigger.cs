using UnityEngine;

namespace ProtectTree.Runtime.VFX
{
    [DisallowMultipleComponent]
    public sealed class AnimationVfxTrigger : MonoBehaviour
    {
        [Header("Anchor")]
        [SerializeField] private Transform castPoint;
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private Transform shootingPoint;
        [SerializeField] private Transform explosionPoint;

        [Header("Prefab")]
        [SerializeField] private GoldenGatherBurstVfx goldenGatherBurstPrefab;
        [SerializeField] private MagicShooting magicShootingPrefab;
        [SerializeField] private MagicExplosion magicExplosionPrefab;
        [SerializeField] private Transform effectRoot;
        [SerializeField] private bool autoDestroySpawnedVfx = true;

        [Header("Boss Magic")]
        [SerializeField] private float shootingMagicDurationSeconds = 2f;
        [SerializeField] private float explosionMagicDurationSeconds = 1.2f;
        [SerializeField] private bool invertShootingMagicFacing = true;

        [Header("Transform")]
        [SerializeField] private bool inheritCastPointRotation;

        [Header("Rendering")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 3100;

        private bool _isFacingLeft;
        private bool _hasQueuedExplosionWorldPosition;
        private Vector3 _queuedExplosionWorldPosition;

        private void OnValidate()
        {
            shootingMagicDurationSeconds =
                Mathf.Max(0.05f, shootingMagicDurationSeconds);
            explosionMagicDurationSeconds =
                Mathf.Max(0.05f, explosionMagicDurationSeconds);
        }

        public void SetSortingOrder(int order)
        {
            sortingOrder = order;
        }

        public void SetFacing(string facing)
        {
            _isFacingLeft = facing == "Left";
        }

        public void QueueExplosionMagicTarget(Vector3 worldPosition)
        {
            _queuedExplosionWorldPosition = worldPosition;
            _hasQueuedExplosionWorldPosition = true;
        }

        // Animation Event only calls public methods. This component owns the
        // spawn position, prefab reference, and rendering order.
        public void PlayGoldenGatherBurst()
        {
            if (goldenGatherBurstPrefab == null)
            {
                Debug.LogWarning(
                    "[ProtectTree][VFX] Golden gather burst prefab is not assigned.",
                    this);
                return;
            }

            Transform anchor = castPoint != null ? castPoint : transform;
            Vector3 position = anchor.TransformPoint(localOffset);
            Quaternion rotation = inheritCastPointRotation
                ? anchor.rotation
                : Quaternion.identity;

            GoldenGatherBurstVfx instance = Instantiate(
                goldenGatherBurstPrefab,
                position,
                rotation,
                effectRoot);

            instance.SetAutoDestroy(autoDestroySpawnedVfx);
            instance.SetSorting(sortingLayerName, sortingOrder);
            instance.Play();
        }

        public void PlayShootingMagic()
        {
            if (magicShootingPrefab == null)
            {
                Debug.LogWarning(
                    "[ProtectTree][VFX] Magic shooting prefab is not assigned.",
                    this);
                return;
            }

            Transform anchor = shootingPoint != null
                ? shootingPoint
                : castPoint != null
                    ? castPoint
                    : transform;

            MagicShooting instance = Instantiate(
                magicShootingPrefab,
                anchor.position,
                anchor.rotation,
                effectRoot);
            instance.SetSorting(sortingLayerName, sortingOrder);
            instance.Play(shootingMagicDurationSeconds, autoDestroySpawnedVfx);
            ApplyFacing(instance.transform);
        }

        public void PlayExplosionMagic()
        {
            if (_hasQueuedExplosionWorldPosition)
            {
                _hasQueuedExplosionWorldPosition = false;
                PlayExplosionMagicAt(_queuedExplosionWorldPosition);
                return;
            }

            Transform anchor = explosionPoint != null
                ? explosionPoint
                : castPoint != null
                    ? castPoint
                    : transform;
            PlayExplosionMagicAt(anchor.position);
        }

        public MagicExplosion PlayExplosionMagicAt(Vector3 worldPosition)
        {
            if (magicExplosionPrefab == null)
            {
                Debug.LogWarning(
                    "[ProtectTree][VFX] Magic explosion prefab is not assigned.",
                    this);
                return null;
            }

            MagicExplosion instance = Instantiate(
                magicExplosionPrefab,
                worldPosition,
                Quaternion.identity,
                effectRoot);
            instance.SetSorting(sortingLayerName, sortingOrder);
            instance.Play(explosionMagicDurationSeconds, autoDestroySpawnedVfx);
            return instance;
        }

        private void ApplyFacing(Transform instanceTransform)
        {
            if (instanceTransform == null)
            {
                return;
            }

            Vector3 scale = instanceTransform.localScale;
            float facingSign = _isFacingLeft ? -1f : 1f;
            if (invertShootingMagicFacing)
            {
                facingSign *= -1f;
            }

            scale.x = Mathf.Abs(scale.x) * facingSign;
            instanceTransform.localScale = scale;
        }

        [ContextMenu("Preview Golden Gather Burst")]
        private void PreviewGoldenGatherBurst()
        {
            PlayGoldenGatherBurst();
        }

        [ContextMenu("Preview Shooting Magic")]
        private void PreviewShootingMagic()
        {
            PlayShootingMagic();
        }

        [ContextMenu("Preview Explosion Magic")]
        private void PreviewExplosionMagic()
        {
            PlayExplosionMagic();
        }
    }
}
