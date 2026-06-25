using ProtectTree.Core.Match;
using ProtectTree.Runtime.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree
{
    public class UIBattleCanvas : MatchSceneFeature
    {
        [SerializeField] private Slider sliderBosssHp;
        [SerializeField] private TextMeshProUGUI bossName;
        [SerializeField] private GameObject bossInfoRoot;
        [SerializeField] private bool showHealthText = true;

        private void Awake()
        {
            ResolveBossInfoRoot();
            HideBossInfo();
        }

        public override void Refresh(MatchSceneContext context)
        {
            EnemySnapshot boss = FindBoss(context);
            if (boss == null || context.Flow == null || context.Flow.IsFinished)
            {
                HideBossInfo();
                return;
            }

            ShowBossInfo(boss);
        }

        public override void OnRuntimeUnavailable()
        {
            HideBossInfo();
        }

        private void ShowBossInfo(EnemySnapshot boss)
        {
            ResolveBossInfoRoot();

            if (bossInfoRoot != null)
            {
                bossInfoRoot.SetActive(true);
            }

            if (sliderBosssHp != null)
            {
                sliderBosssHp.gameObject.SetActive(true);
                sliderBosssHp.minValue = 0f;
                sliderBosssHp.maxValue = Mathf.Max(1, boss.MaxHealth);
                sliderBosssHp.value = Mathf.Clamp(
                    boss.Health,
                    0,
                    sliderBosssHp.maxValue);
            }

            if (bossName != null)
            {
                bossName.gameObject.SetActive(true);
                bossName.text = showHealthText
                    ? $"{FormatBossName(boss.EnemyId)} {boss.Health}/{boss.MaxHealth}"
                    : FormatBossName(boss.EnemyId);
            }
        }

        private void HideBossInfo()
        {
            ResolveBossInfoRoot();

            if (bossInfoRoot != null && bossInfoRoot != gameObject)
            {
                bossInfoRoot.SetActive(false);
                return;
            }

            if (sliderBosssHp != null)
            {
                sliderBosssHp.gameObject.SetActive(false);
            }

            if (bossName != null)
            {
                bossName.gameObject.SetActive(false);
            }
        }

        private void ResolveBossInfoRoot()
        {
            if (bossInfoRoot != null || sliderBosssHp == null)
            {
                return;
            }

            Transform sliderParent = sliderBosssHp.transform.parent;
            bossInfoRoot = sliderParent != null
                ? sliderParent.gameObject
                : sliderBosssHp.gameObject;
        }

        private static EnemySnapshot FindBoss(MatchSceneContext context)
        {
            if (context == null
                || context.Flow == null
                || context.Enemies == null
                || context.Flow.Phase != "BossBattle")
            {
                return null;
            }

            foreach (EnemySnapshot enemy in context.Enemies.Enemies)
            {
                if (enemy != null
                    && enemy.IsBoss
                    && enemy.Wave == context.Flow.Wave)
                {
                    return enemy;
                }
            }

            return null;
        }

        private static string FormatBossName(string enemyId)
        {
            return string.IsNullOrWhiteSpace(enemyId)
                ? "Boss"
                : enemyId;
        }
    }
}
