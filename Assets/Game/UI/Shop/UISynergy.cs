using ProtectTree.Core.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree.Runtime.UI
{
    public sealed class UISynergy : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI text;

        public void Render(ShopSynergySnapshot synergy)
        {
            GameObject viewRoot = root != null ? root : gameObject;
            viewRoot.SetActive(true);

            if (icon != null)
            {
                icon.sprite = UIResourceLoader.LoadSprite(
                    $"UI/Icons/Synergy/{synergy.SynergyId}");
            }

            if (text != null)
            {
                text.text = synergy.DisplayName;
            }
        }

        public void Hide()
        {
            GameObject viewRoot = root != null ? root : gameObject;
            viewRoot.SetActive(false);
        }
    }
}
