using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    /// <summary>
    /// 棋子星级表现组件：负责二星/三星常驻光效和合成瞬间爆发。
    /// 可手动挂在棋子 prefab 上；若没有挂，运行时会自动补一个默认版本。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardPieceLevelVfx : MonoBehaviour
    {
        private const int GlowTextureSize = 64;
        private const string EffectRootName = "LevelVfxRoot";
        private const string AuraName = "LevelAura";
        private const string BurstName = "MergeBurst";

        private static Sprite s_glowSprite;

        [Header("Optional References")]
        [SerializeField] private Transform effectRoot;
        [SerializeField] private SpriteRenderer auraRenderer;
        [SerializeField] private SpriteRenderer burstRenderer;
        [SerializeField] private ParticleSystem levelThreeParticles;

        [Header("Level Aura")]
        [SerializeField] private Color levelTwoAuraColor =
            new Color(1f, 1f, 1f, 0.34f);
        [SerializeField] private Color levelThreeAuraColor =
            new Color(1f, 0.72f, 0.08f, 0.52f);
        [SerializeField] private Vector3 levelTwoAuraScale =
            new Vector3(1.45f, 1.08f, 1f);
        [SerializeField] private Vector3 levelThreeAuraScale =
            new Vector3(1.7f, 1.24f, 1f);
        [SerializeField] private bool useAutoSizeHint = true;
        [SerializeField] private Vector2 minimumAutoAuraSize =
            new Vector2(1.35f, 1.0f);
        [SerializeField] private Vector2 levelTwoAutoSizePadding =
            new Vector2(1.35f, 1.22f);
        [SerializeField] private Vector2 levelThreeAutoSizePadding =
            new Vector2(1.55f, 1.38f);
        [SerializeField] private float auraPulseSpeed = 2.8f;
        [SerializeField] private float auraPulseScale = 0.055f;

        [Header("Merge Burst")]
        [SerializeField] private Color levelTwoBurstColor =
            new Color(1f, 1f, 1f, 0.78f);
        [SerializeField] private Color levelThreeBurstColor =
            new Color(1f, 0.78f, 0.12f, 0.9f);
        [SerializeField] private Vector3 levelTwoBurstScale =
            new Vector3(1.65f, 1.0f, 1f);
        [SerializeField] private Vector3 levelThreeBurstScale =
            new Vector3(2.05f, 1.25f, 1f);
        [SerializeField] private float burstDuration = 0.45f;

        private Transform _effectAnchor;
        private int _level = 1;
        private float _burstSecondsRemaining;
        private Vector3 _currentAuraBaseScale;
        private Vector3 _currentBurstTargetScale;
        private Color _currentBurstColor;
        private Vector2 _autoSizeHint;
        private bool _hasAutoSizeHint;

        public void Initialize(Transform effectAnchor)
        {
            Initialize(effectAnchor, Vector2.zero);
        }

        public void Initialize(Transform effectAnchor, Vector2 autoSizeHint)
        {
            _effectAnchor = effectAnchor;
            SetAutoSizeHint(autoSizeHint);
            EnsureVisualObjects();
            UpdateEffectRootPosition();
            SetLevel(_level);
        }

        public void SetAutoSizeHint(Vector2 autoSizeHint)
        {
            _hasAutoSizeHint = autoSizeHint.x > 0.01f && autoSizeHint.y > 0.01f;
            _autoSizeHint = _hasAutoSizeHint ? autoSizeHint : Vector2.zero;
        }

        public void SetLevel(int level)
        {
            _level = Mathf.Max(1, level);
            EnsureVisualObjects();

            bool showAura = _level >= 2;
            if (auraRenderer != null)
            {
                auraRenderer.gameObject.SetActive(showAura);
                auraRenderer.color = _level >= 3
                    ? levelThreeAuraColor
                    : levelTwoAuraColor;
            }

            _currentAuraBaseScale = GetAuraScale(_level);

            if (levelThreeParticles != null)
            {
                if (_level >= 3 && !levelThreeParticles.isPlaying)
                {
                    levelThreeParticles.Play();
                }
                else if (_level < 3 && levelThreeParticles.isPlaying)
                {
                    levelThreeParticles.Stop();
                }
            }
        }

        public void PlayMergeBurst(int level)
        {
            SetLevel(level);

            _burstSecondsRemaining = Mathf.Max(0.01f, burstDuration);
            _currentBurstColor = _level >= 3
                ? levelThreeBurstColor
                : levelTwoBurstColor;
            _currentBurstTargetScale = _level >= 3
                ? levelThreeBurstScale
                : levelTwoBurstScale;

            if (burstRenderer != null)
            {
                burstRenderer.gameObject.SetActive(true);
                burstRenderer.color = _currentBurstColor;
                burstRenderer.transform.localScale = Vector3.zero;
            }
        }

        private void Update()
        {
            EnsureVisualObjects();
            UpdateEffectRootPosition();
            UpdateAuraPulse();
            UpdateBurst();
        }

        private void UpdateAuraPulse()
        {
            if (auraRenderer == null || !auraRenderer.gameObject.activeSelf)
            {
                return;
            }

            // 常驻光晕只做轻微呼吸，避免二星/三星持续抢走战斗主体注意力。
            float pulse = 1f
                + Mathf.Sin(Time.time * auraPulseSpeed) * auraPulseScale;
            auraRenderer.transform.localScale = _currentAuraBaseScale * pulse;
        }

        private Vector3 GetAuraScale(int level)
        {
            Vector3 configuredScale = level >= 3
                ? levelThreeAuraScale
                : levelTwoAuraScale;

            if (!useAutoSizeHint || !_hasAutoSizeHint)
            {
                return configuredScale;
            }

            Vector2 padding = level >= 3
                ? levelThreeAutoSizePadding
                : levelTwoAutoSizePadding;
            Vector2 paddedSize = new Vector2(
                Mathf.Max(minimumAutoAuraSize.x, _autoSizeHint.x * padding.x),
                Mathf.Max(minimumAutoAuraSize.y, _autoSizeHint.y * padding.y));

            // 自动尺寸只负责把默认光晕撑到角色范围附近；手动配置得更大时仍以手动值为准。
            return new Vector3(
                Mathf.Max(configuredScale.x, paddedSize.x),
                Mathf.Max(configuredScale.y, paddedSize.y),
                configuredScale.z);
        }

        private void UpdateBurst()
        {
            if (burstRenderer == null
                || !burstRenderer.gameObject.activeSelf
                || _burstSecondsRemaining <= 0f)
            {
                return;
            }

            _burstSecondsRemaining -= Time.deltaTime;
            float duration = Mathf.Max(0.01f, burstDuration);
            float progress = 1f - Mathf.Clamp01(_burstSecondsRemaining / duration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);

            burstRenderer.transform.localScale =
                Vector3.Lerp(Vector3.zero, _currentBurstTargetScale, easedProgress);

            Color color = _currentBurstColor;
            color.a *= 1f - progress;
            burstRenderer.color = color;

            if (_burstSecondsRemaining <= 0f)
            {
                burstRenderer.gameObject.SetActive(false);
            }
        }

        private void EnsureVisualObjects()
        {
            if (effectRoot == null)
            {
                effectRoot = FindDirectChild(transform, EffectRootName)
                    ?? CreateChild(transform, EffectRootName);
            }

            if (auraRenderer == null)
            {
                auraRenderer = GetOrCreateRenderer(AuraName, -20);
            }

            if (burstRenderer == null)
            {
                burstRenderer = GetOrCreateRenderer(BurstName, -10);
                burstRenderer.gameObject.SetActive(false);
            }
        }

        private SpriteRenderer GetOrCreateRenderer(
            string objectName,
            int sortingOrder)
        {
            Transform child = FindDirectChild(effectRoot, objectName)
                ?? CreateChild(effectRoot, objectName);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            if (renderer.sprite == null)
            {
                renderer.sprite = GetGlowSprite();
            }

            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void UpdateEffectRootPosition()
        {
            if (effectRoot == null || _effectAnchor == null)
            {
                return;
            }

            // 光效跟随 SelectionAnchor，避免不同体型棋子的光晕偏到脚底外。
            Vector3 localPosition =
                transform.InverseTransformPoint(_effectAnchor.position);
            localPosition.z = 0f;
            effectRoot.localPosition = localPosition;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform CreateChild(Transform parent, string childName)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Sprite GetGlowSprite()
        {
            if (s_glowSprite != null)
            {
                return s_glowSprite;
            }

            Texture2D texture = new Texture2D(
                GlowTextureSize,
                GlowTextureSize,
                TextureFormat.RGBA32,
                false)
            {
                filterMode = FilterMode.Bilinear,
                name = "GeneratedPieceLevelGlow",
                hideFlags = HideFlags.HideAndDontSave,
            };

            for (int y = 0; y < GlowTextureSize; y++)
            {
                for (int x = 0; x < GlowTextureSize; x++)
                {
                    float normalizedX =
                        (x + 0.5f) / GlowTextureSize * 2f - 1f;
                    float normalizedY =
                        (y + 0.5f) / GlowTextureSize * 2f - 1f;
                    float distance = Mathf.Sqrt(
                        normalizedX * normalizedX + normalizedY * normalizedY);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            s_glowSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, GlowTextureSize, GlowTextureSize),
                new Vector2(0.5f, 0.5f),
                GlowTextureSize);
            s_glowSprite.name = "GeneratedPieceLevelGlowSprite";
            s_glowSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_glowSprite;
        }
    }
}
