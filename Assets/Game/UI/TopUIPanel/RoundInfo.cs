using System;
using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Core.Network;
using ProtectTree.Runtime.Lua;
using ProtectTree.Runtime.Network;
using ProtectTree.Runtime.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProtectTree.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class RoundInfo : MatchSceneFeature
    {
        private const string BossBattlePhase = "BossBattle";
        private const string RoundTimeRootName = "RoundTime";

        [SerializeField] private TextMeshProUGUI roundNum;
        [SerializeField] private TextMeshProUGUI roundState;
        [SerializeField] private TextMeshProUGUI roundTime;
        [SerializeField] private TextMeshProUGUI playerHp;
        [SerializeField] private Button roundReadyButton;
        [SerializeField] private ProtectTree.UIMessageBox messageBoxPrefab;
        [SerializeField] private string mainMenuSceneName = "Menu";
        [SerializeField] private GameObject leakNumArea;
        [SerializeField] private TextMeshProUGUI leakNum;

        private readonly HashSet<string> _knownEnemyKeys =
            new HashSet<string>();
        private readonly HashSet<string> _defeatedEnemyKeys =
            new HashSet<string>();
        private readonly HashSet<string> _leakedEnemyKeys =
            new HashSet<string>();
        private readonly HashSet<string> _jointDefenseKnownEnemyKeys =
            new HashSet<string>();
        private readonly HashSet<string> _jointDefenseDefeatedEnemyKeys =
            new HashSet<string>();
        private readonly HashSet<string> _jointDefenseLeakedEnemyKeys =
            new HashSet<string>();

        private LuaRuntime _runtime;
        private TextMeshProUGUI _readyButtonText;
        private ProtectTree.UIMessageBox _messageBox;
        private string _shownResult;
        private int _localPlayerId;
        private bool _canRequestReady;
        private bool _hasReadyButtonState;
        private bool _lastReadyButtonInteractable;
        private string _lastReadyButtonLabel;
        private bool _shownLanDisconnect;

        private void Awake()
        {
            _readyButtonText = roundReadyButton != null
                ? roundReadyButton.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;
            roundReadyButton?.onClick.AddListener(RequestReady);
            SetReadyButton(false, "准备");
            SetLeakCounter(false, 0);
        }

        private void OnDestroy()
        {
            roundReadyButton?.onClick.RemoveListener(RequestReady);

            if (_messageBox != null)
            {
                Destroy(_messageBox.gameObject);
                _messageBox = null;
            }
        }

        public override void OnRuntimeChanged(LuaRuntime runtime)
        {
            _runtime = runtime;
        }

        public override void Refresh(MatchSceneContext context)
        {
            _runtime = context.Runtime;
            _localPlayerId = context.LocalPlayerId;

            bool showBossBattleTimerOnly = IsBossBattleTimerOnly(context.Flow);
            if (!showBossBattleTimerOnly)
            {
                SetBossBattleTimerOnly(false);
            }

            RenderFlow(context);
            RenderPlayerHp(context.Players, context.LocalPlayerId);
            RenderReadyButton(context.Flow, context.Players, context.LocalPlayerId);
            RenderMatchResult(context.Flow);
            RenderLanDisconnect(context.Flow, context.LanMatch);

            if (showBossBattleTimerOnly)
            {
                SetLeakCounter(false, 0);
                SetBossBattleTimerOnly(true);
            }
        }

        public override void OnRuntimeUnavailable()
        {
            HideMatchResult();
            _runtime = null;
            _localPlayerId = 0;
            _canRequestReady = false;
            _shownLanDisconnect = false;
            SetBossBattleTimerOnly(false);
            SetRoundInfoVisible(true);
            ClearEnemyCounters();
            SetText(roundNum, "--");
            SetText(roundState, "未开始");
            SetText(roundTime, "--");
            SetText(playerHp, "--");
            SetLeakCounter(false, 0);
            SetReadyButton(false, "准备");
        }

        private void RenderFlow(MatchSceneContext context)
        {
            MatchFlowSnapshot flow = context.Flow;
            if (flow == null)
            {
                OnRuntimeUnavailable();
                return;
            }

            RememberEnemyCounters(context);
            int observedPlayerId = context.ObservedPlayerId > 0
                ? context.ObservedPlayerId
                : context.LocalPlayerId;
            bool isJointDefense = flow.Phase == "JointDefense";
            EnemyCounters counters = isJointDefense
                ? GetJointDefenseCounters(flow.Wave, observedPlayerId)
                : GetEnemyCounters(flow.Wave, observedPlayerId);
            int leakCount = isJointDefense
                ? CountKeys(
                    _jointDefenseLeakedEnemyKeys,
                    flow.Wave,
                    context.LocalPlayerId)
                : counters.Leaked;
            bool shouldShowLeakCounter = isJointDefense
                ? context.LocalPlayerId != observedPlayerId && leakCount > 0
                : flow.Phase == "Battle" && leakCount > 0;

            SetText(roundNum, flow.IsFinished ? "结束" : $" {flow.Wave} ");
            SetText(roundState, GetPhaseLabel(flow, counters));
            SetText(roundTime, GetTimeLabel(flow));
            SetLeakCounter(shouldShowLeakCounter, leakCount);
        }

        private void RenderPlayerHp(PlayerRosterSnapshot players, int localPlayerId)
        {
            PlayerSnapshot player = FindPlayer(players, localPlayerId);
            SetText(playerHp, player != null ? $"{player.Health}" : "--");
        }

        private void RenderReadyButton(
            MatchFlowSnapshot flow,
            PlayerRosterSnapshot players,
            int localPlayerId)
        {
            PlayerSnapshot player = FindPlayer(players, localPlayerId);
            bool isPreparation = flow != null
                && (flow.Phase == "Preparation" || flow.Phase == "BossPreparation");
            bool canReady = isPreparation
                && player != null
                && player.Status == "Alive"
                && !player.IsReady;

            _canRequestReady = canReady;
            if (player != null && player.IsReady)
            {
                SetReadyButton(false, "已准备");
                return;
            }

            SetReadyButton(canReady, isPreparation ? "准备" : GetReadyUnavailableLabel(flow));
        }

        private void RenderMatchResult(MatchFlowSnapshot flow)
        {
            if (flow == null || !flow.IsFinished)
            {
                HideMatchResult();
                return;
            }

            if (_shownResult == flow.Result)
            {
                return;
            }

            _shownResult = flow.Result;
            ProtectTree.UIMessageBox box = EnsureMessageBox();
            if (box == null)
            {
                return;
            }

            bool isVictory = flow.Result == "Victory";
            string titleText = isVictory ? "胜利" : "失败";
            string messageText = isVictory
                ? "成功击败 Boss，守护完成。"
                : "防线失守，本次守护失败。";

            box.Show(
                titleText,
                messageText,
                "重新开始",
                RestartCurrentScene,
                "返回主菜单",
                LoadMainMenu,
                "关闭",
                box.Hide);
        }

        private ProtectTree.UIMessageBox EnsureMessageBox()
        {
            if (_messageBox != null)
            {
                return _messageBox;
            }

            ProtectTree.UIMessageBox prefab = messageBoxPrefab != null
                ? messageBoxPrefab
                : Resources.Load<ProtectTree.UIMessageBox>("Prefabs/UIMessageBox");
            if (prefab == null)
            {
                Debug.LogWarning(
                    "UIMessageBox prefab was not found under Resources/Prefabs.",
                    this);
                return null;
            }

            _messageBox = Instantiate(prefab);
            _messageBox.Hide();
            return _messageBox;
        }

        private void HideMatchResult()
        {
            if (_shownResult == null)
            {
                return;
            }

            _shownResult = null;
            _messageBox?.Hide();
        }

        private void RenderLanDisconnect(
            MatchFlowSnapshot flow,
            LanMatchRuntime lanMatch)
        {
            if (flow != null && flow.IsFinished)
            {
                return;
            }

            if (lanMatch == null
                || !lanMatch.IsActive
                || !lanMatch.IsClient
                || !lanMatch.IsClientRetryExhausted)
            {
                if (lanMatch == null
                    || !lanMatch.IsActive
                    || lanMatch.HasLiveMatchTransport)
                {
                    _shownLanDisconnect = false;
                }

                return;
            }

            if (_shownLanDisconnect)
            {
                return;
            }

            _shownLanDisconnect = true;
            ProtectTree.UIMessageBox box = EnsureMessageBox();
            if (box == null)
            {
                return;
            }

            box.Show(
                "连接中断",
                "无法重新连接到主机，本局连接已经中断。可以返回主菜单重新进入房间。",
                "返回主菜单",
                LoadMainMenu,
                null,
                null,
                "关闭",
                box.Hide);
        }

        private void RequestReady()
        {
            Debug.Log(
                $"[ProtectTree][RoundInfo] Ready button clicked. CanRequest={_canRequestReady}, LocalPlayer={_localPlayerId}, Lan={GetLanStatus()}.",
                this);

            if (!_canRequestReady || _runtime == null || _localPlayerId <= 0)
            {
                return;
            }

            LanMatchRuntime lanMatch = LanMatchRuntime.Instance;
            if (lanMatch != null && lanMatch.IsActive && lanMatch.IsClient)
            {
                if (!lanMatch.TrySendCommand(MatchCommand.SetReady(true)))
                {
                    Debug.LogWarning(
                        "Ready command was not sent because LAN match transport is not ready.",
                        this);
                }

                return;
            }

            try
            {
                _runtime.SetPlayerReady(_localPlayerId, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Ready command was rejected by authority: {exception.Message}",
                    this);
            }
        }

        private void RestartCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
        }

        private void LoadMainMenu()
        {
            if (string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                Debug.LogWarning("Main menu scene name is empty.", this);
                return;
            }

            Debug.Log("[ProtectTree][LAN Match] Returning to main menu and clearing LAN session.", this);
            LanMatchRuntime.ClearActiveSession();
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }

        private void SetReadyButton(bool isInteractable, string label)
        {
            if (roundReadyButton != null)
            {
                roundReadyButton.interactable = isInteractable;
            }

            SetText(_readyButtonText, label);
            if (!_hasReadyButtonState
                || _lastReadyButtonInteractable != isInteractable
                || _lastReadyButtonLabel != label)
            {
                _hasReadyButtonState = true;
                _lastReadyButtonInteractable = isInteractable;
                _lastReadyButtonLabel = label;
                Debug.Log(
                    $"[ProtectTree][RoundInfo] Ready button state changed. Interactable={isInteractable}, Label={label}, LocalPlayer={_localPlayerId}, Lan={GetLanStatus()}.",
                    this);
            }
        }

        private void SetRoundInfoVisible(bool isVisible)
        {
            SetGameObjectVisible(roundNum, isVisible);
            SetGameObjectVisible(roundState, isVisible);
            SetGameObjectVisible(roundTime, isVisible);
            SetGameObjectVisible(playerHp, isVisible);

            if (roundReadyButton != null)
            {
                roundReadyButton.gameObject.SetActive(isVisible);
            }

            if (!isVisible && leakNumArea != null)
            {
                leakNumArea.SetActive(false);
            }
        }

        private void SetBossBattleTimerOnly(bool isTimerOnly)
        {
            Transform roundTimeRoot = FindRoundTimeRoot();
            if (isTimerOnly)
            {
                gameObject.SetActive(true);
            }

            // Boss战中顶部信息只保留倒计时，避免和Boss血条、战斗表现互相遮挡。
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                bool shouldShow = !isTimerOnly || child == roundTimeRoot;
                child.gameObject.SetActive(shouldShow);
            }

            if (roundTimeRoot != null)
            {
                roundTimeRoot.gameObject.SetActive(true);
            }

            SetGameObjectVisible(roundTime, true);

            if (roundReadyButton != null)
            {
                roundReadyButton.gameObject.SetActive(!isTimerOnly);
            }
        }

        private Transform FindRoundTimeRoot()
        {
            if (roundTime != null)
            {
                Transform current = roundTime.transform;
                while (current != null && current.parent != transform)
                {
                    current = current.parent;
                }

                if (current != null)
                {
                    return current;
                }
            }

            return transform.Find(RoundTimeRootName);
        }

        private static void SetGameObjectVisible(Component component, bool isVisible)
        {
            if (component != null)
            {
                component.gameObject.SetActive(isVisible);
            }
        }

        private static string GetLanStatus()
        {
            LanMatchRuntime lanMatch = LanMatchRuntime.Instance;
            if (lanMatch == null || !lanMatch.IsActive)
            {
                return "None";
            }

            return lanMatch.GetLifecycleDebugStatus();
        }

        private static bool IsBossBattleTimerOnly(MatchFlowSnapshot flow)
        {
            return flow != null
                && !flow.IsFinished
                && flow.Phase == BossBattlePhase;
        }

        private static string GetReadyUnavailableLabel(MatchFlowSnapshot flow)
        {
            if (flow == null)
            {
                return "准备";
            }

            if (flow.IsFinished)
            {
                return "结束";
            }

            return flow.Phase == "Settlement" ? "结算中" : "战斗中";
        }

        private static string GetPhaseLabel(
            MatchFlowSnapshot flow,
            EnemyCounters counters)
        {
            if (flow.IsFinished)
            {
                return flow.Result == "Victory" ? "胜利" : "失败";
            }

            switch (flow.Phase)
            {
                case "Preparation":
                    return "准备阶段";
                case "Battle":
                    return counters.Total > 0
                        ? $"{counters.Defeated}/{counters.Total}"
                        : "战斗中";
                case "JointDefense":
                    return $"{counters.Defeated}/{counters.Total}";
                case "JointDefenseIntro":
                    return "联防准备";
                case "Settlement":
                    return "结算阶段";
                case "BossPreparation":
                    return "Boss 准备";
                case "BossBattle":
                    return "Boss 战";
                default:
                    return flow.Phase;
            }
        }

        private static string GetTimeLabel(MatchFlowSnapshot flow)
        {
            if (flow.IsFinished || flow.RemainingSeconds <= 0d)
            {
                return "--";
            }

            return Mathf.CeilToInt((float)flow.RemainingSeconds).ToString();
        }

        private static PlayerSnapshot FindPlayer(
            PlayerRosterSnapshot players,
            int playerId)
        {
            if (players == null)
            {
                return null;
            }

            foreach (PlayerSnapshot player in players.Players)
            {
                if (player.PlayerId == playerId)
                {
                    return player;
                }
            }

            return null;
        }

        private void RememberEnemyCounters(MatchSceneContext context)
        {
            if (context.Enemies != null)
            {
                foreach (EnemySnapshot enemy in context.Enemies.Enemies)
                {
                    if (enemy == null || enemy.IsBoss)
                    {
                        continue;
                    }

                    string key = CreateEnemyKey(
                        enemy.Wave,
                        enemy.TargetPlayerId,
                        enemy.InstanceId);
                    _knownEnemyKeys.Add(key);

                    if (enemy.Status == "Defeated")
                    {
                        _defeatedEnemyKeys.Add(key);
                    }
                    else if (enemy.Status == "ReachedEndpoint")
                    {
                        _leakedEnemyKeys.Add(key);
                    }

                    RememberJointDefenseSnapshotCounter(context, enemy, key);
                }
            }

            if (context.Events == null)
            {
                return;
            }

            foreach (MatchEvent matchEvent in context.Events)
            {
                RememberJointDefenseEventCounter(matchEvent);

                if (matchEvent == null
                    || matchEvent.IsBoss == true
                    || !matchEvent.Wave.HasValue
                    || !matchEvent.TargetPlayerId.HasValue
                    || !matchEvent.EnemyInstanceId.HasValue)
                {
                    continue;
                }

                string key = CreateEnemyKey(
                    matchEvent.Wave.Value,
                    matchEvent.TargetPlayerId.Value,
                    matchEvent.EnemyInstanceId.Value);

                if (matchEvent.Type == "EnemyCreated")
                {
                    _knownEnemyKeys.Add(key);
                }
                else if (matchEvent.Type == "EnemyDefeated")
                {
                    _knownEnemyKeys.Add(key);
                    _defeatedEnemyKeys.Add(key);
                }
                else if (matchEvent.Type == "EnemyReachedEndpoint")
                {
                    _knownEnemyKeys.Add(key);
                    _leakedEnemyKeys.Add(key);
                }
            }
        }

        private void RememberJointDefenseSnapshotCounter(
            MatchSceneContext context,
            EnemySnapshot enemy,
            string key)
        {
            if (context.Flow == null || context.Flow.Phase != "JointDefense")
            {
                return;
            }

            if (enemy.Status == "Alive")
            {
                _jointDefenseKnownEnemyKeys.Add(key);
            }
            else if (_jointDefenseKnownEnemyKeys.Contains(key)
                && enemy.Status == "Defeated")
            {
                _jointDefenseDefeatedEnemyKeys.Add(key);
            }
        }

        private void RememberJointDefenseEventCounter(MatchEvent matchEvent)
        {
            if (matchEvent == null
                || matchEvent.IsBoss == true
                || !matchEvent.Wave.HasValue
                || !matchEvent.EnemyInstanceId.HasValue
                || !matchEvent.LeakOwnerPlayerId.HasValue)
            {
                return;
            }

            int defenderPlayerId = matchEvent.TargetPlayerId
                ?? matchEvent.DefenderPlayerId
                ?? 0;
            if (defenderPlayerId <= 0)
            {
                return;
            }

            string defenderKey = CreateEnemyKey(
                matchEvent.Wave.Value,
                defenderPlayerId,
                matchEvent.EnemyInstanceId.Value);
            string leakOwnerKey = CreateEnemyKey(
                matchEvent.Wave.Value,
                matchEvent.LeakOwnerPlayerId.Value,
                matchEvent.EnemyInstanceId.Value);

            if (matchEvent.Type == "JointDefenseEnemyTransferred"
                || matchEvent.Type == "EnemyDefeated"
                || matchEvent.Type == "LeakedEnemyRescued"
                || matchEvent.Type == "EnemyReachedEndpoint"
                || matchEvent.Type == "JointDefenseEnemyEscaped")
            {
                _jointDefenseKnownEnemyKeys.Add(defenderKey);
            }

            if (matchEvent.Type == "EnemyDefeated"
                || matchEvent.Type == "LeakedEnemyRescued")
            {
                _jointDefenseDefeatedEnemyKeys.Add(defenderKey);
            }
            else if (matchEvent.Type == "EnemyReachedEndpoint"
                || matchEvent.Type == "JointDefenseEnemyEscaped")
            {
                _jointDefenseLeakedEnemyKeys.Add(leakOwnerKey);
            }
        }

        private EnemyCounters GetEnemyCounters(int wave, int playerId)
        {
            return new EnemyCounters(
                CountKeys(_defeatedEnemyKeys, wave, playerId),
                CountKeys(_leakedEnemyKeys, wave, playerId),
                CountKeys(_knownEnemyKeys, wave, playerId));
        }

        private EnemyCounters GetJointDefenseCounters(int wave, int defenderPlayerId)
        {
            return new EnemyCounters(
                CountKeys(_jointDefenseDefeatedEnemyKeys, wave, defenderPlayerId),
                0,
                CountKeys(_jointDefenseKnownEnemyKeys, wave, defenderPlayerId));
        }

        private void SetLeakCounter(bool isVisible, int count)
        {
            if (leakNumArea != null)
            {
                leakNumArea.SetActive(isVisible);
            }

            SetText(leakNum, count.ToString());
        }

        private void ClearEnemyCounters()
        {
            _knownEnemyKeys.Clear();
            _defeatedEnemyKeys.Clear();
            _leakedEnemyKeys.Clear();
            _jointDefenseKnownEnemyKeys.Clear();
            _jointDefenseDefeatedEnemyKeys.Clear();
            _jointDefenseLeakedEnemyKeys.Clear();
        }

        private static int CountKeys(
            HashSet<string> keys,
            int wave,
            int playerId)
        {
            string prefix = wave + ":" + playerId + ":";
            int count = 0;
            foreach (string key in keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static string CreateEnemyKey(
            int wave,
            int targetPlayerId,
            int enemyInstanceId)
        {
            return wave + ":" + targetPlayerId + ":" + enemyInstanceId;
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private struct EnemyCounters
        {
            public EnemyCounters(int defeated, int leaked, int total)
            {
                Defeated = defeated;
                Leaked = leaked;
                Total = total;
            }

            public int Defeated { get; }

            public int Leaked { get; }

            public int Total { get; }
        }
    }
}
