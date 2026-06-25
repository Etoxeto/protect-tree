using TMPro;
using UnityEngine;

namespace ProtectTree
{
    public class UIBattleResult : MonoBehaviour
    {
        [SerializeField] private UILoadingStatus loadingStatus;
        [SerializeField] private TextMeshProUGUI hpDecrease;

        public void Show(int hpLoss, bool showLoading)
        {
            gameObject.SetActive(true);

            if (loadingStatus != null)
            {
                if (showLoading)
                {
                    loadingStatus.Show();
                }
                else
                {
                    loadingStatus.Hide();
                }
            }

            if (hpDecrease != null)
            {
                bool hasHpLoss = hpLoss > 0;
                hpDecrease.gameObject.SetActive(hasHpLoss);
                if (hasHpLoss)
                {
                    hpDecrease.text = $"生命值减少{hpLoss}";
                }
            }
        }

        public void Hide()
        {
            if (loadingStatus != null)
            {
                loadingStatus.Hide();
            }

            if (hpDecrease != null)
            {
                hpDecrease.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
        }
    }
}
