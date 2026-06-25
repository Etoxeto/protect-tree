using UnityEngine;

namespace ProtectTree.Runtime
{
    [System.Serializable]
    public sealed class BoardProjectileEntry
    {
        [SerializeField] private string pieceId;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float fireDelaySeconds = 0.15f;
        [SerializeField] private float speed = 6f;
        [SerializeField] private float impactHoldSeconds = 0.05f;
        [SerializeField] private bool rotateToVelocity = true;
        [SerializeField] private float baseScale = 1f;
        [SerializeField] private int layersPerScaleStep = 20;
        [SerializeField] private float scalePerLayerStep;

        public string PieceId => pieceId;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float FireDelaySeconds => Mathf.Max(0f, fireDelaySeconds);
        public float Speed => speed;
        public float ImpactHoldSeconds => Mathf.Max(0f, impactHoldSeconds);
        public bool RotateToVelocity => rotateToVelocity;

        public float GetScale(int layerCount)
        {
            float resolvedBaseScale = baseScale > 0f ? baseScale : 1f;
            if (scalePerLayerStep <= 0f || layerCount <= 0)
            {
                return resolvedBaseScale;
            }

            int stepLayers = Mathf.Max(1, layersPerScaleStep);
            int steps = Mathf.FloorToInt(layerCount / (float)stepLayers);
            return Mathf.Max(0.01f, resolvedBaseScale + steps * scalePerLayerStep);
        }
    }

    [CreateAssetMenu(
        fileName = "BoardProjectileCatalog",
        menuName = "Game/Board/Projectile Catalog"
    )]
    public sealed class BoardProjectileCatalog : ScriptableObject
    {
        [SerializeField] private BoardProjectileEntry[] pieceProjectileEntries;

        public bool TryGetEntry(
            string pieceId,
            out BoardProjectileEntry entry)
        {
            entry = null;

            if (pieceProjectileEntries == null || string.IsNullOrEmpty(pieceId))
            {
                return false;
            }

            foreach (BoardProjectileEntry candidate in pieceProjectileEntries)
            {
                if (candidate != null && candidate.PieceId == pieceId)
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
