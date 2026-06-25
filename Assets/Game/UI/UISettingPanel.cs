using ProtectTree.Runtime;
using ProtectTree.Runtime.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree
{
    public class UISettingPanel : MonoBehaviour
    {
        [SerializeField] private Button exitButton;
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button confirmNameButton;
        [SerializeField] private Slider mainVolume;
        [SerializeField] private Slider bgmVolume;
        [SerializeField] private Slider sfxVolume;
        [SerializeField] private Button avatarSetting;
        [SerializeField] private UIAvatarSetting avatarSettingPanel;
        [SerializeField] private Image currentAvatarImage;

        private void Awake()
        {
            if (avatarSettingPanel == null)
            {
                avatarSettingPanel = GetComponentInChildren<UIAvatarSetting>(true);
            }

            if (currentAvatarImage == null && avatarSetting != null)
            {
                currentAvatarImage = avatarSetting.GetComponent<Image>();
            }

            avatarSettingPanel?.Hide();
        }

        private void OnEnable()
        {
            if (nameInputField != null)
            {
                nameInputField.text = PlayerProfileOptions.PlayerName;
            }
            InitializeVolumeSliders();
            avatarSettingPanel?.Hide();

            exitButton?.onClick.AddListener(Hide);
            confirmNameButton?.onClick.AddListener(ConfirmName);
            avatarSetting?.onClick.AddListener(ShowAvatarSetting);
            if (avatarSettingPanel != null)
            {
                avatarSettingPanel.AvatarSelected += OnAvatarSelected;
            }
            mainVolume?.onValueChanged.AddListener(AudioManager.SetMasterVolume);
            bgmVolume?.onValueChanged.AddListener(AudioManager.SetBgmVolume);
            sfxVolume?.onValueChanged.AddListener(AudioManager.SetSfxVolume);
            RefreshAvatar();
        }

        private void OnDisable()
        {
            exitButton?.onClick.RemoveListener(Hide);
            confirmNameButton?.onClick.RemoveListener(ConfirmName);
            avatarSetting?.onClick.RemoveListener(ShowAvatarSetting);
            if (avatarSettingPanel != null)
            {
                avatarSettingPanel.AvatarSelected -= OnAvatarSelected;
            }
            mainVolume?.onValueChanged.RemoveListener(AudioManager.SetMasterVolume);
            bgmVolume?.onValueChanged.RemoveListener(AudioManager.SetBgmVolume);
            sfxVolume?.onValueChanged.RemoveListener(AudioManager.SetSfxVolume);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (nameInputField != null)
            {
                nameInputField.text = PlayerProfileOptions.PlayerName;
            }
            RefreshAvatar();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void ConfirmName()
        {
            PlayerProfileOptions.PlayerName =
                nameInputField != null ? nameInputField.text : null;
            if (nameInputField != null)
            {
                nameInputField.text = PlayerProfileOptions.PlayerName;
            }
        }

        private void InitializeVolumeSliders()
        {
            SetSliderValue(mainVolume, AudioManager.MasterVolume);
            SetSliderValue(bgmVolume, AudioManager.BgmVolume);
            SetSliderValue(sfxVolume, AudioManager.SfxVolume);
        }

        private void ShowAvatarSetting()
        {
            avatarSettingPanel?.Show();
        }

        private void OnAvatarSelected(string avatarResourcePath, Sprite avatarSprite)
        {
            SetAvatarSprite(avatarSprite);
        }

        private void RefreshAvatar()
        {
            Sprite avatarSprite = UIResourceLoader.LoadSprite(
                PlayerProfileOptions.AvatarResourcePath);
            if (avatarSprite == null)
            {
                avatarSprite = UIResourceLoader.LoadSprite(
                    PlayerProfileOptions.DefaultAvatarResourcePath);
            }

            SetAvatarSprite(avatarSprite);
        }

        private void SetAvatarSprite(Sprite avatarSprite)
        {
            Image targetImage = currentAvatarImage != null
                ? currentAvatarImage
                : avatarSetting != null
                    ? avatarSetting.GetComponent<Image>()
                    : null;
            if (targetImage == null || avatarSprite == null)
            {
                return;
            }

            targetImage.sprite = avatarSprite;
            targetImage.enabled = true;
            targetImage.preserveAspect = true;
        }

        private static void SetSliderValue(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(value);
            }
        }
    }
}
