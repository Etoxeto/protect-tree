using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree
{
    public class UILoadingStatus : MonoBehaviour
    {
        [SerializeField] private Image[] arrows;
        [SerializeField] private float stepSeconds = 0.16f;

        private Color[] _baseColors;
        private float _elapsedSeconds;
        private int _offset;

        private void Awake()
        {
            CacheColors();
        }

        private void OnEnable()
        {
            CacheColors();
            _elapsedSeconds = 0f;
            _offset = 0;
            ApplyColors();
        }

        private void Update()
        {
            if (arrows == null || arrows.Length == 0)
            {
                return;
            }

            _elapsedSeconds += Time.unscaledDeltaTime;
            if (_elapsedSeconds < stepSeconds)
            {
                return;
            }

            _elapsedSeconds -= stepSeconds;
            _offset = (_offset + 1) % arrows.Length;
            ApplyColors();
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void CacheColors()
        {
            if (arrows == null)
            {
                _baseColors = null;
                return;
            }

            if (_baseColors != null && _baseColors.Length == arrows.Length)
            {
                return;
            }

            _baseColors = new Color[arrows.Length];
            for (int i = 0; i < arrows.Length; i++)
            {
                _baseColors[i] = arrows[i] != null ? arrows[i].color : Color.white;
            }
        }

        private void ApplyColors()
        {
            if (arrows == null || _baseColors == null || _baseColors.Length == 0)
            {
                return;
            }

            for (int i = 0; i < arrows.Length; i++)
            {
                if (arrows[i] == null)
                {
                    continue;
                }

                int sourceIndex = i - _offset;
                while (sourceIndex < 0)
                {
                    sourceIndex += _baseColors.Length;
                }

                sourceIndex %= _baseColors.Length;
                arrows[i].color = _baseColors[sourceIndex];
            }
        }
    }
}
