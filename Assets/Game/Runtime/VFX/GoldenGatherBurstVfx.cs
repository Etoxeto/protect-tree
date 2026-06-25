using UnityEngine;

namespace ProtectTree.Runtime.VFX
{
    [DisallowMultipleComponent]
    public sealed class GoldenGatherBurstVfx : MonoBehaviour
    {
        private const int CurrentTuningVersion = 1;
        private const float OldCoreMaxScaleDefault = 0.62f;
        private const float NewCoreMaxScaleDefault = 0.82f;

        [Header("Playback")]
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool autoDestroy;

        [Header("Timing")]
        [SerializeField] private float gatherDuration = 0.75f;
        [SerializeField] private float coreHoldDuration = 0.18f;
        [SerializeField] private float burstDuration = 0.55f;
        [SerializeField] private AnimationCurve gatherCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Gather")]
        [SerializeField] private int gatherParticleCount = 72;
        [SerializeField] private float gatherRadius = 1.65f;
        [SerializeField] private float gatherRadiusJitter = 0.35f;
        [SerializeField] private float gatherParticleSize = 0.08f;

        [Header("Core")]
        [SerializeField] private float coreMaxScale = 0.82f;
        [SerializeField] private float flashMaxScale = 2.1f;

        [Header("Burst")]
        [SerializeField] private int burstParticleCount = 56;
        [SerializeField] private float burstMinSpeed = 2.5f;
        [SerializeField] private float burstMaxSpeed = 5.2f;
        [SerializeField] private float burstParticleSize = 0.11f;
        [SerializeField] private float burstUpwardBias = 0.32f;
        [SerializeField] private float burstGravityModifier = 0.38f;

        [Header("Color")]
        [SerializeField] private Color gatherColor = new Color(1f, 0.72f, 0.18f, 0.92f);
        [SerializeField] private Color coreColor = new Color(1f, 0.9f, 0.38f, 0.95f);
        [SerializeField] private Color burstColor = new Color(1f, 0.63f, 0.05f, 0.95f);
        [SerializeField] private Color flashColor = new Color(1f, 0.86f, 0.25f, 0.62f);

        [Header("Rendering")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 3000;
        [SerializeField, HideInInspector] private int tuningVersion;

        private static Sprite s_softCircleSprite;
        private static Material s_particleMaterial;
        private static Material s_spriteMaterial;

        private ParticleSystem _gatherSystem;
        private ParticleSystem _burstSystem;
        private SpriteRenderer _coreRenderer;
        private SpriteRenderer _flashRenderer;
        private ParticleSystem.Particle[] _gatherParticles;
        private ParticleSystem.Particle[] _burstParticles;
        private Vector3[] _gatherStartPositions;
        private Vector3[] _burstVelocities;
        private float _elapsedSeconds;
        private float _flashElapsedSeconds;
        private bool _isPlaying;
        private bool _hasBurst;

        private float BurstStartSeconds => gatherDuration + coreHoldDuration;

        private float TotalDuration => gatherDuration + coreHoldDuration + burstDuration;

        public static GoldenGatherBurstVfx Spawn(
            Vector3 position,
            Transform parent = null,
            int sortingOrder = 3000)
        {
            GameObject owner = new GameObject("GoldenGatherBurstVfx");
            owner.SetActive(false);
            if (parent != null)
            {
                owner.transform.SetParent(parent, false);
            }

            owner.transform.position = position;
            GoldenGatherBurstVfx vfx = owner.AddComponent<GoldenGatherBurstVfx>();
            vfx.sortingOrder = sortingOrder;
            vfx.autoDestroy = true;
            vfx.playOnEnable = false;
            owner.SetActive(true);
            vfx.Play();
            return vfx;
        }

        public void SetAutoDestroy(bool shouldAutoDestroy)
        {
            autoDestroy = shouldAutoDestroy;
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

        private void Awake()
        {
            ApplyTuningMigration();
            EnsureVisuals();
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                Play();
            }
        }

        private void OnValidate()
        {
            ApplyTuningMigration();
            gatherDuration = Mathf.Max(0.05f, gatherDuration);
            coreHoldDuration = Mathf.Max(0f, coreHoldDuration);
            burstDuration = Mathf.Max(0.05f, burstDuration);
            gatherParticleCount = Mathf.Clamp(gatherParticleCount, 1, 512);
            burstParticleCount = Mathf.Clamp(burstParticleCount, 1, 512);
            gatherRadius = Mathf.Max(0.05f, gatherRadius);
            gatherRadiusJitter = Mathf.Max(0f, gatherRadiusJitter);
            gatherParticleSize = Mathf.Max(0.01f, gatherParticleSize);
            burstParticleSize = Mathf.Max(0.01f, burstParticleSize);
            burstMinSpeed = Mathf.Max(0.01f, burstMinSpeed);
            burstMaxSpeed = Mathf.Max(burstMinSpeed, burstMaxSpeed);
            coreMaxScale = Mathf.Max(0.01f, coreMaxScale);
            flashMaxScale = Mathf.Max(coreMaxScale, flashMaxScale);
            burstUpwardBias = Mathf.Max(0f, burstUpwardBias);
            burstGravityModifier = Mathf.Max(0f, burstGravityModifier);
            ConfigureParticleSystems();
            ApplySorting();
        }

        private void ApplyTuningMigration()
        {
            if (tuningVersion >= CurrentTuningVersion)
            {
                return;
            }

            if (tuningVersion < 1
                && Mathf.Approximately(coreMaxScale, OldCoreMaxScaleDefault))
            {
                coreMaxScale = NewCoreMaxScaleDefault;
            }

            tuningVersion = CurrentTuningVersion;
        }

        public void Play()
        {
            EnsureVisuals();
            _elapsedSeconds = 0f;
            _flashElapsedSeconds = 0f;
            _hasBurst = false;
            _isPlaying = true;

            ResetRenderers();
            EmitGatherParticles();
            _burstSystem.Clear(true);
            _burstSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void StopAndClear()
        {
            _isPlaying = false;
            _gatherSystem?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _burstSystem?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_coreRenderer != null)
            {
                _coreRenderer.enabled = false;
            }

            if (_flashRenderer != null)
            {
                _flashRenderer.enabled = false;
            }
        }

        private void Update()
        {
            if (!_isPlaying)
            {
                return;
            }

            _elapsedSeconds += Time.deltaTime;
            UpdateGatherParticles();
            UpdateCore();

            if (!_hasBurst && _elapsedSeconds >= BurstStartSeconds)
            {
                TriggerBurst();
            }

            UpdateBurstParticles();
            UpdateFlash();

            if (_elapsedSeconds >= TotalDuration)
            {
                if (autoDestroy)
                {
                    _isPlaying = false;
                    Destroy(gameObject);
                    return;
                }

                StopAndClear();
            }
        }

        private void EnsureVisuals()
        {
            if (_gatherSystem == null)
            {
                _gatherSystem = CreateParticleSystem("GatherParticles", gatherParticleCount);
            }

            if (_burstSystem == null)
            {
                _burstSystem = CreateParticleSystem("BurstParticles", burstParticleCount);
            }

            if (_coreRenderer == null)
            {
                _coreRenderer = CreateSpriteRenderer("CoreLight", sortingOrder + 1);
            }

            if (_flashRenderer == null)
            {
                _flashRenderer = CreateSpriteRenderer("BurstFlash", sortingOrder);
            }

            int maxGatherParticles = Mathf.Max(1, gatherParticleCount);
            if (_gatherParticles == null || _gatherParticles.Length < maxGatherParticles)
            {
                _gatherParticles = new ParticleSystem.Particle[maxGatherParticles];
            }

            if (_gatherStartPositions == null
                || _gatherStartPositions.Length < maxGatherParticles)
            {
                _gatherStartPositions = new Vector3[maxGatherParticles];
            }

            int maxBurstParticles = Mathf.Max(1, burstParticleCount);
            if (_burstParticles == null || _burstParticles.Length < maxBurstParticles)
            {
                _burstParticles = new ParticleSystem.Particle[maxBurstParticles];
            }

            if (_burstVelocities == null || _burstVelocities.Length < maxBurstParticles)
            {
                _burstVelocities = new Vector3[maxBurstParticles];
            }

            ConfigureParticleSystems();
            ApplySorting();
        }

        private void ConfigureParticleSystems()
        {
            if (_gatherSystem != null)
            {
                ParticleSystem.MainModule main = _gatherSystem.main;
                main.maxParticles = Mathf.Max(1, gatherParticleCount);
                main.startLifetime = TotalDuration;
                main.startSize = gatherParticleSize;
                main.gravityModifier = 0f;
            }

            if (_burstSystem != null)
            {
                ParticleSystem.MainModule main = _burstSystem.main;
                main.maxParticles = Mathf.Max(1, burstParticleCount);
                main.startLifetime = burstDuration;
                main.startSize = burstParticleSize;
                // 直接用脚本控制爆发粒子的下落，避免短生命周期下 Unity 内置重力不明显。
                main.gravityModifier = 0f;
            }
        }

        private ParticleSystem CreateParticleSystem(string objectName, int maxParticles)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(transform, false);

            ParticleSystem particleSystem = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.startSpeed = 0f;
            main.startLifetime = TotalDuration;
            main.startSize = gatherParticleSize;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer =
                particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = GetParticleMaterial();

            return particleSystem;
        }

        private SpriteRenderer CreateSpriteRenderer(string objectName, int order)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(transform, false);

            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSoftCircleSprite();
            renderer.sharedMaterial = GetSpriteMaterial();
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        private void ResetRenderers()
        {
            if (_coreRenderer != null)
            {
                _coreRenderer.enabled = false;
                _coreRenderer.transform.localScale = Vector3.zero;
                _coreRenderer.color = Color.clear;
            }

            if (_flashRenderer != null)
            {
                _flashRenderer.enabled = false;
                _flashRenderer.transform.localScale = Vector3.zero;
                _flashRenderer.color = Color.clear;
            }
        }

        private void EmitGatherParticles()
        {
            _gatherSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _gatherSystem.Clear(true);

            for (int index = 0; index < gatherParticleCount; index++)
            {
                float angle = (Mathf.PI * 2f * index / gatherParticleCount)
                    + Random.Range(-0.18f, 0.18f);
                float radius = gatherRadius + Random.Range(
                    -gatherRadiusJitter,
                    gatherRadiusJitter);
                Vector3 localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    Random.Range(-0.04f, 0.04f));

                _gatherStartPositions[index] = localPosition;

                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = localPosition,
                    startColor = gatherColor,
                    startLifetime = TotalDuration,
                    startSize = gatherParticleSize * Random.Range(0.75f, 1.25f),
                };
                _gatherSystem.Emit(emitParams, 1);
            }

            _gatherSystem.Play(true);
        }

        private void UpdateGatherParticles()
        {
            int count = _gatherSystem.GetParticles(_gatherParticles);
            float t = Mathf.Clamp01(_elapsedSeconds / gatherDuration);
            float curvedT = gatherCurve.Evaluate(t);

            for (int index = 0; index < count; index++)
            {
                Vector3 startPosition = index < _gatherStartPositions.Length
                    ? _gatherStartPositions[index]
                    : _gatherParticles[index].position;
                _gatherParticles[index].position = Vector3.Lerp(
                    startPosition,
                    Vector3.zero,
                    curvedT);

                Color color = Color.Lerp(gatherColor, coreColor, t);
                color.a *= Mathf.Lerp(1f, 0.25f, t);
                _gatherParticles[index].startColor = color;
            }

            _gatherSystem.SetParticles(_gatherParticles, count);
        }

        private void UpdateCore()
        {
            if (_coreRenderer == null)
            {
                return;
            }

            float coreStart = gatherDuration * 0.45f;
            if (_elapsedSeconds < coreStart)
            {
                _coreRenderer.enabled = false;
                return;
            }

            float t = Mathf.InverseLerp(coreStart, BurstStartSeconds, _elapsedSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            _coreRenderer.enabled = true;
            _coreRenderer.transform.localScale =
                Vector3.one * Mathf.Lerp(0.08f, coreMaxScale, eased);
            Color color = coreColor;
            color.a *= Mathf.Lerp(0.2f, 1f, eased);
            _coreRenderer.color = color;
        }

        private void TriggerBurst()
        {
            _hasBurst = true;
            _flashElapsedSeconds = 0f;
            if (_coreRenderer != null)
            {
                _coreRenderer.enabled = false;
            }

            _burstSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _burstSystem.Clear(true);

            for (int index = 0; index < burstParticleCount; index++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 radialDirection =
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 direction =
                    (radialDirection + Vector3.up * burstUpwardBias).normalized;
                float speed = Random.Range(burstMinSpeed, burstMaxSpeed);
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = Vector3.zero,
                    velocity = Vector3.zero,
                    startColor = burstColor,
                    startLifetime = burstDuration,
                    startSize = burstParticleSize * Random.Range(0.65f, 1.4f),
                };
                _burstVelocities[index] = direction * speed;
                _burstSystem.Emit(emitParams, 1);
            }

            _burstSystem.Play(true);
        }

        private void UpdateBurstParticles()
        {
            if (!_hasBurst || _burstSystem == null || _burstParticles == null)
            {
                return;
            }

            int count = _burstSystem.GetParticles(_burstParticles);
            float deltaTime = Time.deltaTime;
            float fallAcceleration = burstGravityModifier * 12f;

            for (int index = 0; index < count; index++)
            {
                if (index >= _burstVelocities.Length)
                {
                    continue;
                }

                _burstVelocities[index] += Vector3.down * fallAcceleration * deltaTime;
                _burstParticles[index].position += _burstVelocities[index] * deltaTime;
            }

            _burstSystem.SetParticles(_burstParticles, count);
        }

        private void UpdateFlash()
        {
            if (!_hasBurst || _flashRenderer == null)
            {
                return;
            }

            _flashElapsedSeconds += Time.deltaTime;
            float t = Mathf.Clamp01(_flashElapsedSeconds / burstDuration);
            _flashRenderer.enabled = t < 1f;
            _flashRenderer.transform.localScale =
                Vector3.one * Mathf.Lerp(coreMaxScale, flashMaxScale, t);

            Color color = flashColor;
            color.a *= 1f - Mathf.SmoothStep(0f, 1f, t);
            _flashRenderer.color = color;
        }

        private void ApplySorting()
        {
            if (_gatherSystem != null)
            {
                ParticleSystemRenderer renderer =
                    _gatherSystem.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerName = sortingLayerName;
                renderer.sortingOrder = sortingOrder;
            }

            if (_burstSystem != null)
            {
                ParticleSystemRenderer renderer =
                    _burstSystem.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerName = sortingLayerName;
                renderer.sortingOrder = sortingOrder + 2;
            }

            if (_coreRenderer != null)
            {
                _coreRenderer.sortingLayerName = sortingLayerName;
                _coreRenderer.sortingOrder = sortingOrder + 1;
            }

            if (_flashRenderer != null)
            {
                _flashRenderer.sortingLayerName = sortingLayerName;
                _flashRenderer.sortingOrder = sortingOrder;
            }
        }

        private static Material GetParticleMaterial()
        {
            if (s_particleMaterial != null)
            {
                return s_particleMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");
            s_particleMaterial = new Material(shader)
            {
                name = "Runtime_GoldenGatherBurst_ParticleMaterial",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return s_particleMaterial;
        }

        private static Material GetSpriteMaterial()
        {
            if (s_spriteMaterial != null)
            {
                return s_spriteMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                ?? Shader.Find("Unlit/Transparent");
            s_spriteMaterial = new Material(shader)
            {
                name = "Runtime_GoldenGatherBurst_SpriteMaterial",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return s_spriteMaterial;
        }

        private static Sprite GetSoftCircleSprite()
        {
            if (s_softCircleSprite != null)
            {
                return s_softCircleSprite;
            }

            const int textureSize = 64;
            Texture2D texture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false)
            {
                name = "Runtime_GoldenGatherBurst_SoftCircle",
                hideFlags = HideFlags.HideAndDontSave,
            };

            Vector2 center = new Vector2(textureSize * 0.5f, textureSize * 0.5f);
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center)
                        / (textureSize * 0.5f);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            s_softCircleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                textureSize);
            s_softCircleSprite.name = "Runtime_GoldenGatherBurst_SoftCircleSprite";
            return s_softCircleSprite;
        }
    }
}
