using ProtectTree.Runtime;
using ProtectTree.Runtime.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ProtectTree.Runtime.Menu
{
    public sealed class UIMainMenu : MonoBehaviour
    {
        private const string MatchSceneName = "SampleScene";
        private const string LanLobbySceneName = "LanLobby";

        [FormerlySerializedAs("ButtonSingleGame")]
        [SerializeField] private Button buttonSingleGame;

        [FormerlySerializedAs("ButtonLANGame")]
        [SerializeField] private Button buttonLanGame;

        [FormerlySerializedAs("ButtonInternetGame")]
        [SerializeField] private Button buttonInternetGame;

        [FormerlySerializedAs("ButtonGallery")]
        [SerializeField] private Button buttonGallery;

        [FormerlySerializedAs("ButtonSetting")]
        [SerializeField] private Button buttonSetting;

        [SerializeField] private UISettingPanel settingPanel;

        private void Awake()
        {
            LanMatchRuntime.ClearActiveSession();

            if (buttonSingleGame == null)
            {
                Debug.LogError("Single-player button is not assigned.", this);
                return;
            }

            buttonSingleGame.onClick.AddListener(PlayButtonSound);
            buttonSingleGame.onClick.AddListener(EnterSinglePlayerMode);
            buttonLanGame?.onClick.AddListener(PlayButtonSound);
            buttonLanGame?.onClick.AddListener(EnterLanLobby);
            buttonInternetGame?.onClick.AddListener(PlayButtonSound);
            buttonGallery?.onClick.AddListener(PlayButtonSound);
            buttonSetting?.onClick.AddListener(PlayButtonSound);
            buttonSetting?.onClick.AddListener(OpenSettingPanel);
            settingPanel?.Hide();
            AudioManager.PlayBgm("bgm_normal");
        }

        private void OnDestroy()
        {
            buttonSingleGame?.onClick.RemoveListener(PlayButtonSound);
            buttonSingleGame?.onClick.RemoveListener(EnterSinglePlayerMode);
            buttonLanGame?.onClick.RemoveListener(PlayButtonSound);
            buttonLanGame?.onClick.RemoveListener(EnterLanLobby);
            buttonInternetGame?.onClick.RemoveListener(PlayButtonSound);
            buttonGallery?.onClick.RemoveListener(PlayButtonSound);
            buttonSetting?.onClick.RemoveListener(PlayButtonSound);
            buttonSetting?.onClick.RemoveListener(OpenSettingPanel);
        }

        private void EnterSinglePlayerMode()
        {
            LanMatchRuntime.ClearActiveSession();
            MatchStartupOptions.UseSinglePlayer();
            SetMenuButtonsInteractable(false);
            SceneManager.LoadScene(MatchSceneName, LoadSceneMode.Single);
        }

        private void EnterLanLobby()
        {
            LanMatchRuntime.ClearActiveSession();
            SetMenuButtonsInteractable(false);
            SceneManager.LoadScene(LanLobbySceneName, LoadSceneMode.Single);
        }

        private void OpenSettingPanel()
        {
            settingPanel?.Show();
        }

        private static void PlayButtonSound()
        {
            AudioManager.PlayButtonClick();
        }

        private void SetMenuButtonsInteractable(bool isInteractable)
        {
            SetButtonInteractable(buttonSingleGame, isInteractable);
            SetButtonInteractable(buttonLanGame, isInteractable);
            SetButtonInteractable(buttonInternetGame, isInteractable);
            SetButtonInteractable(buttonGallery, isInteractable);
            SetButtonInteractable(buttonSetting, isInteractable);
        }

        private static void SetButtonInteractable(Button button, bool isInteractable)
        {
            if (button != null)
            {
                button.interactable = isInteractable;
            }
        }
    }
}
