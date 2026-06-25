using System.Collections.Generic;
using ProtectTree.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProtectTree
{
    public class UIAvatarSetting : MonoBehaviour
    {
        [SerializeField] private Transform content;
        [SerializeField] private Button[] avatarButtons;
        [SerializeField] private string avatarResourceRoot =
            "UI/Infos/Player/Avatars";

        private readonly Dictionary<Button, UnityAction> _buttonListeners =
            new Dictionary<Button, UnityAction>();

        public event System.Action<string, Sprite> AvatarSelected;

        private void Awake()
        {
            ResolveContentIfNeeded();
            RefreshButtons();
        }

        private void OnEnable()
        {
            RefreshButtons();
            foreach (Button button in avatarButtons)
            {
                if (button == null)
                {
                    continue;
                }

                if (!_buttonListeners.TryGetValue(button, out UnityAction listener))
                {
                    Button capturedButton = button;
                    listener = () => SelectAvatar(capturedButton);
                    _buttonListeners.Add(button, listener);
                }

                button.onClick.AddListener(listener);
            }
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<Button, UnityAction> entry in _buttonListeners)
            {
                if (entry.Key != null)
                {
                    entry.Key.onClick.RemoveListener(entry.Value);
                }
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void RefreshButtons()
        {
            if (avatarButtons != null && avatarButtons.Length > 0)
            {
                return;
            }

            if (content == null)
            {
                return;
            }

            avatarButtons = content.GetComponentsInChildren<Button>(true);
        }

        private void SelectAvatar(Button button)
        {
            Image avatarImage = FindAvatarImage(button);
            if (avatarImage == null || avatarImage.sprite == null)
            {
                Debug.LogWarning(
                    "Avatar button must contain an Image with a Sprite.",
                    button);
                return;
            }

            string avatarResourcePath = BuildAvatarResourcePath(avatarImage.sprite);
            PlayerProfileOptions.AvatarResourcePath = avatarResourcePath;
            AvatarSelected?.Invoke(avatarResourcePath, avatarImage.sprite);
            Hide();
        }

        private string BuildAvatarResourcePath(Sprite sprite)
        {
            string root = string.IsNullOrWhiteSpace(avatarResourceRoot)
                ? string.Empty
                : avatarResourceRoot.Trim().TrimEnd('/');
            return string.IsNullOrEmpty(root)
                ? sprite.name
                : root + "/" + sprite.name;
        }

        private void ResolveContentIfNeeded()
        {
            if (content != null)
            {
                return;
            }

            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child != transform && child.name == "Content")
                {
                    content = child;
                    return;
                }
            }
        }

        private static Image FindAvatarImage(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Image[] images = button.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image != null
                    && image.sprite != null
                    && image != button.targetGraphic)
                {
                    return image;
                }
            }

            foreach (Image image in images)
            {
                if (image != null && image.sprite != null)
                {
                    return image;
                }
            }

            return null;
        }
    }
}
