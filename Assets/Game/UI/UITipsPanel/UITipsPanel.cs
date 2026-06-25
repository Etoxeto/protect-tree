using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree
{
    public class UITipsPanel : MatchSceneFeature
    {
        private const int AllClearResultIndex = 0;
        private const int HpDecreaseResultIndex = 1;
        private const int JointDefenseResultIndex = 2;

        [SerializeField] private UILoadingStatus roundLoadingStatus;
        [SerializeField] private Image readyButton;
        [SerializeField] private TextMeshProUGUI roundTime;
        [SerializeField] private UIBattleResult[] battleResults;
        [SerializeField] private float loadingSeconds = 0.6f;
        [SerializeField] private float resultHoldSeconds = 1.5f;

        private readonly HashSet<string> _shownResultKeys =
            new HashSet<string>();

        private string _lastPhase;
        private int _lastWave;
        private int _lastLocalHealth = -1;
        private float _loadingUntil;
        private float _resultUntil;
        private int _activeResultIndex = -1;

        private void Awake()
        {
            ResolveRoundLoadingStatus();
            HideLoading();
            HideAllBattleResults();
        }

        public override void Refresh(MatchSceneContext context)
        {
            if (context == null || context.Flow == null)
            {
                OnRuntimeUnavailable();
                return;
            }

            ResolveRoundLoadingStatus();
            UpdatePhaseLoading(context.Flow);
            TryShowEventDrivenResult(context);
            TryShowFallbackSettlementResult(context);
            UpdateLoadingVisibility(context.Flow);
            UpdateResultVisibility();
            RememberLocalHealth(context);
        }

        public override void OnRuntimeUnavailable()
        {
            _lastPhase = null;
            _lastWave = 0;
            _lastLocalHealth = -1;
            _loadingUntil = 0f;
            _resultUntil = 0f;
            _activeResultIndex = -1;
            _shownResultKeys.Clear();
            HideLoading();
            SetRoundControlsVisible(true);
            HideAllBattleResults();
        }

        private void UpdatePhaseLoading(MatchFlowSnapshot flow)
        {
            if (_lastPhase == null)
            {
                _lastPhase = flow.Phase;
                _lastWave = flow.Wave;
                return;
            }

            bool changed = _lastPhase != flow.Phase || _lastWave != flow.Wave;
            _lastPhase = flow.Phase;
            _lastWave = flow.Wave;

            if (!changed || flow.IsFinished)
            {
                return;
            }

            _loadingUntil = Mathf.Max(
                _loadingUntil,
                Time.unscaledTime + loadingSeconds);
        }

        private void TryShowEventDrivenResult(MatchSceneContext context)
        {
            if (context.Events == null || context.Events.Count == 0)
            {
                return;
            }

            bool hasJointDefenseStarted = false;
            int jointWave = context.Flow != null ? context.Flow.Wave : 0;
            int defenderPlayerId = 0;
            bool hasLeakResolution = false;
            int resolutionWave = context.Flow != null ? context.Flow.Wave : 0;
            int finalLeakCount = 0;
            int localHpLoss = 0;

            foreach (MatchEvent matchEvent in context.Events)
            {
                if (matchEvent == null)
                {
                    continue;
                }

                if (matchEvent.Type == "JointDefenseStarted")
                {
                    hasJointDefenseStarted = true;
                    jointWave = matchEvent.Wave ?? jointWave;
                    defenderPlayerId = matchEvent.DefenderPlayerId ?? 0;
                    continue;
                }

                if (matchEvent.Type == "PlayerLeakResolved")
                {
                    hasLeakResolution = true;
                    resolutionWave = matchEvent.Wave ?? resolutionWave;
                    finalLeakCount += matchEvent.FinalLeakCount ?? 0;
                    continue;
                }

                if (matchEvent.Type == "PlayerDamaged"
                    && matchEvent.PlayerId == context.LocalPlayerId)
                {
                    localHpLoss += matchEvent.Damage ?? 0;
                }
            }

            if (hasJointDefenseStarted)
            {
                ShowBattleResult(
                    JointDefenseResultIndex,
                    0,
                    true,
                    "joint:" + jointWave + ":" + defenderPlayerId);
            }

            if (!hasLeakResolution)
            {
                return;
            }

            string key = "resolve:"
                + resolutionWave
                + ":"
                + finalLeakCount
                + ":"
                + localHpLoss;
            ShowBattleResult(
                finalLeakCount > 0 ? HpDecreaseResultIndex : AllClearResultIndex,
                localHpLoss,
                false,
                key);
        }

        private void TryShowFallbackSettlementResult(MatchSceneContext context)
        {
            if (context.Flow == null
                || context.Flow.Phase != "Settlement"
                || context.Events.Count > 0)
            {
                return;
            }

            string key = "settlement:"
                + context.Flow.Wave
                + ":"
                + GetTotalFinalLeaks(context)
                + ":"
                + GetLocalHealth(context);
            if (_shownResultKeys.Contains(key))
            {
                return;
            }

            int hpLoss = 0;
            int currentHealth = GetLocalHealth(context);
            if (_lastLocalHealth >= 0 && currentHealth >= 0)
            {
                hpLoss = Mathf.Max(0, _lastLocalHealth - currentHealth);
            }

            int leakCount = GetCurrentWaveEndpointCount(context);
            ShowBattleResult(
                leakCount > 0 || hpLoss > 0
                    ? HpDecreaseResultIndex
                    : AllClearResultIndex,
                hpLoss,
                false,
                key);
        }

        private void ShowBattleResult(
            int resultIndex,
            int hpLoss,
            bool showLoading,
            string key)
        {
            if (string.IsNullOrWhiteSpace(key)
                || _shownResultKeys.Contains(key)
                || !TryGetBattleResult(resultIndex, out UIBattleResult result))
            {
                return;
            }

            _shownResultKeys.Add(key);
            HideAllBattleResults();
            _activeResultIndex = resultIndex;
            _resultUntil = Time.unscaledTime + resultHoldSeconds;
            result.Show(hpLoss, showLoading);
        }

        private void UpdateLoadingVisibility(MatchFlowSnapshot flow)
        {
            bool shouldShow = roundLoadingStatus != null
                && !flow.IsFinished
                && Time.unscaledTime < _loadingUntil;

            if (shouldShow)
            {
                roundLoadingStatus.Show();
            }
            else
            {
                HideLoading();
            }

            SetRoundControlsVisible(!shouldShow);
        }

        private void UpdateResultVisibility()
        {
            if (_activeResultIndex < 0 || Time.unscaledTime < _resultUntil)
            {
                return;
            }

            HideAllBattleResults();
        }

        private void RememberLocalHealth(MatchSceneContext context)
        {
            int health = GetLocalHealth(context);
            if (health >= 0)
            {
                _lastLocalHealth = health;
            }
        }

        private void ResolveRoundLoadingStatus()
        {
            if (roundLoadingStatus != null)
            {
                return;
            }

            UILoadingStatus[] candidates =
                GetComponentsInChildren<UILoadingStatus>(true);
            foreach (UILoadingStatus candidate in candidates)
            {
                if (candidate != null && !IsInsideBattleResult(candidate))
                {
                    roundLoadingStatus = candidate;
                    return;
                }
            }
        }

        private bool IsInsideBattleResult(UILoadingStatus loadingStatus)
        {
            if (battleResults == null)
            {
                return false;
            }

            foreach (UIBattleResult result in battleResults)
            {
                if (result != null
                    && loadingStatus.transform.IsChildOf(result.transform))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetBattleResult(
            int index,
            out UIBattleResult result)
        {
            result = null;
            if (battleResults == null
                || index < 0
                || index >= battleResults.Length)
            {
                return false;
            }

            result = battleResults[index];
            return result != null;
        }

        private void HideLoading()
        {
            if (roundLoadingStatus != null)
            {
                roundLoadingStatus.Hide();
            }
        }

        private void HideAllBattleResults()
        {
            _activeResultIndex = -1;
            if (battleResults == null)
            {
                return;
            }

            foreach (UIBattleResult result in battleResults)
            {
                result?.Hide();
            }
        }

        private void SetRoundControlsVisible(bool isVisible)
        {
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(isVisible);
            }

            if (roundTime != null)
            {
                roundTime.gameObject.SetActive(isVisible);
            }
        }

        private static int GetLocalHealth(MatchSceneContext context)
        {
            if (context.Players == null)
            {
                return -1;
            }

            foreach (PlayerSnapshot player in context.Players.Players)
            {
                if (player.PlayerId == context.LocalPlayerId)
                {
                    return player.Health;
                }
            }

            return -1;
        }

        private static int GetTotalFinalLeaks(MatchSceneContext context)
        {
            int count = 0;
            if (context.Events == null)
            {
                return count;
            }

            foreach (MatchEvent matchEvent in context.Events)
            {
                if (matchEvent != null
                    && matchEvent.Type == "PlayerLeakResolved")
                {
                    count += matchEvent.FinalLeakCount ?? 0;
                }
            }

            return count;
        }

        private static int GetCurrentWaveEndpointCount(MatchSceneContext context)
        {
            if (context.Enemies == null || context.Flow == null)
            {
                return 0;
            }

            int count = 0;
            foreach (EnemySnapshot enemy in context.Enemies.Enemies)
            {
                if (enemy.Wave == context.Flow.Wave
                    && !enemy.IsBoss
                    && enemy.Status == "ReachedEndpoint")
                {
                    count++;
                }
            }

            return count;
        }
    }
}
