using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree
{
    public class UIMessageBox : MonoBehaviour
    {
        public Button buttonYes;
        public Button buttonNo;
        public Button buttonExit;
        public TextMeshProUGUI title;
        public TextMeshProUGUI message;

        private Action _onYes;
        private Action _onNo;
        private Action _onExit;

        private void Awake()
        {
            buttonYes?.onClick.AddListener(HandleYesClicked);
            buttonNo?.onClick.AddListener(HandleNoClicked);
            buttonExit?.onClick.AddListener(HandleExitClicked);
        }

        private void OnDestroy()
        {
            buttonYes?.onClick.RemoveListener(HandleYesClicked);
            buttonNo?.onClick.RemoveListener(HandleNoClicked);
            buttonExit?.onClick.RemoveListener(HandleExitClicked);
        }

        public void Show(
            string titleText,
            string messageText,
            string yesText,
            Action onYes,
            string noText,
            Action onNo,
            string exitText,
            Action onExit)
        {
            SetText(title, titleText);
            SetText(message, messageText);
            _onYes = onYes;
            _onNo = onNo;
            _onExit = onExit;

            // 弹窗本身只负责显示和按钮回调，具体流程由调用方决定。
            ConfigureButton(buttonYes, yesText, _onYes);
            ConfigureButton(buttonNo, noText, _onNo);
            ConfigureButton(buttonExit, exitText, _onExit);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void HandleYesClicked()
        {
            _onYes?.Invoke();
        }

        private void HandleNoClicked()
        {
            _onNo?.Invoke();
        }

        private void HandleExitClicked()
        {
            _onExit?.Invoke();
        }

        private static void ConfigureButton(
            Button button,
            string label,
            Action callback)
        {
            if (button == null)
            {
                return;
            }

            bool shouldShow = callback != null || !string.IsNullOrEmpty(label);
            button.gameObject.SetActive(shouldShow);
            button.interactable = callback != null;

            TextMeshProUGUI buttonText =
                button.GetComponentInChildren<TextMeshProUGUI>(true);
            SetText(buttonText, label);
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }
    }
}
