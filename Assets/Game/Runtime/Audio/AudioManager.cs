using System.Collections.Generic;
using UnityEngine;

namespace ProtectTree
{
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        public const string ButtonClickId = "button_01";
        public const string CoinSpendId = "coin_spend";

        private const string SfxResourceRoot = "Audio/SFX/";
        private const string BgmResourceRoot = "Audio/BGM/";
        private const string MasterVolumeKey = "ProtectTree.Audio.MasterVolume";
        private const string BgmVolumeKey = "ProtectTree.Audio.BgmVolume";
        private const string SfxVolumeKey = "ProtectTree.Audio.SfxVolume";

        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource SfxSource;
        [SerializeField] private AudioSource BgmSource;

        private readonly Dictionary<string, AudioClip> _clipCache =
            new Dictionary<string, AudioClip>();

        private float _masterVolume = 1f;
        private float _bgmVolume = 1f;
        private float _sfxVolume = 1f;
        private bool _isInitialized;

        public static float MasterVolume => EnsureInstance()._masterVolume;

        public static float BgmVolume => EnsureInstance()._bgmVolume;

        public static float SfxVolume => EnsureInstance()._sfxVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Initialize();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void PlayButtonClick()
        {
            PlaySfx(ButtonClickId);
        }

        public static void PlayCoinSpend()
        {
            PlaySfx(CoinSpendId);
        }

        public static void PlaySfx(string sfxId)
        {
            EnsureInstance().PlaySfxInternal(sfxId);
        }

        public static void PlayBgm(string bgmId, bool loop = true)
        {
            EnsureInstance().PlayBgmInternal(bgmId, loop);
        }

        public static void StopBgm()
        {
            AudioManager manager = EnsureInstance();
            if (manager.BgmSource != null)
            {
                manager.BgmSource.Stop();
                manager.BgmSource.clip = null;
            }
        }

        public static void SetMasterVolume(float value)
        {
            AudioManager manager = EnsureInstance();
            manager._masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, manager._masterVolume);
            PlayerPrefs.Save();
            manager.ApplyVolumes();
        }

        public static void SetBgmVolume(float value)
        {
            AudioManager manager = EnsureInstance();
            manager._bgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BgmVolumeKey, manager._bgmVolume);
            PlayerPrefs.Save();
            manager.ApplyVolumes();
        }

        public static void SetSfxVolume(float value)
        {
            AudioManager manager = EnsureInstance();
            manager._sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, manager._sfxVolume);
            PlayerPrefs.Save();
            manager.ApplyVolumes();
        }

        private static AudioManager EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            AudioManager existing = FindObjectOfType<AudioManager>();
            if (existing != null)
            {
                Instance = existing;
                existing.Initialize();
                return existing;
            }

            GameObject gameObject = new GameObject("AudioManager");
            return gameObject.AddComponent<AudioManager>();
        }

        private void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            DontDestroyOnLoad(gameObject);
            EnsureAudioSources();
            LoadVolumeSettings();
            ApplyVolumes();
            _isInitialized = true;
        }

        private void EnsureAudioSources()
        {
            AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
            if (SfxSource == null && sources.Length > 0)
            {
                SfxSource = sources[0];
            }

            if (BgmSource == null && sources.Length > 1)
            {
                BgmSource = sources[1];
            }

            if (SfxSource == null)
            {
                SfxSource = CreateAudioSource("SfxSource");
            }

            if (BgmSource == null)
            {
                BgmSource = CreateAudioSource("BgmSource");
            }

            SfxSource.playOnAwake = false;
            SfxSource.loop = false;
            SfxSource.spatialBlend = 0f;

            BgmSource.playOnAwake = false;
            BgmSource.loop = true;
            BgmSource.spatialBlend = 0f;
        }

        private AudioSource CreateAudioSource(string sourceName)
        {
            GameObject child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            return child.AddComponent<AudioSource>();
        }

        private void LoadVolumeSettings()
        {
            _masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            _bgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
            _sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        }

        private void ApplyVolumes()
        {
            if (BgmSource != null)
            {
                BgmSource.volume = _masterVolume * _bgmVolume;
            }

            if (SfxSource != null)
            {
                SfxSource.volume = _masterVolume * _sfxVolume;
            }
        }

        private void PlaySfxInternal(string sfxId)
        {
            if (string.IsNullOrWhiteSpace(sfxId) || SfxSource == null)
            {
                return;
            }

            AudioClip clip = LoadClip(SfxResourceRoot + sfxId);
            if (clip == null)
            {
                Debug.LogWarning($"SFX not found: {sfxId}", this);
                return;
            }

            SfxSource.PlayOneShot(clip);
        }

        private void PlayBgmInternal(string bgmId, bool loop)
        {
            if (string.IsNullOrWhiteSpace(bgmId) || BgmSource == null)
            {
                return;
            }

            AudioClip clip = LoadClip(BgmResourceRoot + bgmId);
            if (clip == null)
            {
                Debug.LogWarning($"BGM not found: {bgmId}", this);
                return;
            }

            if (BgmSource.clip == clip && BgmSource.isPlaying)
            {
                return;
            }

            BgmSource.clip = clip;
            BgmSource.loop = loop;
            BgmSource.Play();
        }

        private AudioClip LoadClip(string resourcePath)
        {
            if (_clipCache.TryGetValue(resourcePath, out AudioClip cachedClip))
            {
                return cachedClip;
            }

            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip != null)
            {
                _clipCache.Add(resourcePath, clip);
            }

            return clip;
        }
    }
}
