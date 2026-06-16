using ProtectTree.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ProtectTree.Runtime.Menu
{
    public sealed class UIMainMenu : MonoBehaviour
    {
        private const string MatchSceneName = "SampleScene";
        private const int LocalPrototypePlayerCount = 2;

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

        private void Awake()
        {
            if (buttonSingleGame == null)
            {
                Debug.LogError("Single-player button is not assigned.", this);
                return;
            }

            buttonSingleGame.onClick.AddListener(EnterSinglePlayerMode);
            buttonLanGame?.onClick.AddListener(EnterLocalMultiplayerPrototype);
        }

        private void OnDestroy()
        {
            buttonSingleGame?.onClick.RemoveListener(EnterSinglePlayerMode);
            buttonLanGame?.onClick.RemoveListener(EnterLocalMultiplayerPrototype);
        }

        private void EnterSinglePlayerMode()
        {
            MatchStartupOptions.UseSinglePlayer();
            SetMenuButtonsInteractable(false);
            SceneManager.LoadScene(MatchSceneName, LoadSceneMode.Single);
        }

        private void EnterLocalMultiplayerPrototype()
        {
            MatchStartupOptions.UseLocalMultiplayer(LocalPrototypePlayerCount);
            SetMenuButtonsInteractable(false);
            SceneManager.LoadScene(MatchSceneName, LoadSceneMode.Single);
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
