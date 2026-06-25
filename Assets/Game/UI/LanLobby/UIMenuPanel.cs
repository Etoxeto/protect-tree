using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree
{
    public class UIMenuPanel : MonoBehaviour
    {
        [SerializeField] private Button buttonCreate;
        [SerializeField] private Button buttonJoin;
        [SerializeField] private TMP_InputField inviteCode;
        [SerializeField] private Button buttonBackToMainMenu;

        public event Action CreateRequested;

        public event Action<string> JoinRequested;

        public event Action BackToMainMenuRequested;

        public string InviteCode =>
            inviteCode != null ? inviteCode.text : string.Empty;

        private void OnEnable()
        {
            buttonCreate?.onClick.AddListener(RequestCreate);
            buttonJoin?.onClick.AddListener(RequestJoin);
            buttonBackToMainMenu?.onClick.AddListener(RequestBackToMainMenu);
        }

        private void OnDisable()
        {
            buttonCreate?.onClick.RemoveListener(RequestCreate);
            buttonJoin?.onClick.RemoveListener(RequestJoin);
            buttonBackToMainMenu?.onClick.RemoveListener(RequestBackToMainMenu);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            SetInteractable(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetInviteCode(string value)
        {
            if (inviteCode != null)
            {
                inviteCode.text = value ?? string.Empty;
            }
        }

        public void SetInteractable(bool isInteractable)
        {
            SetButtonInteractable(buttonCreate, isInteractable);
            SetButtonInteractable(buttonJoin, isInteractable);
            SetButtonInteractable(buttonBackToMainMenu, isInteractable);
            if (inviteCode != null)
            {
                inviteCode.interactable = isInteractable;
            }
        }

        private void RequestCreate()
        {
            CreateRequested?.Invoke();
        }

        private void RequestJoin()
        {
            JoinRequested?.Invoke(InviteCode);
        }

        private void RequestBackToMainMenu()
        {
            BackToMainMenuRequested?.Invoke();
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
