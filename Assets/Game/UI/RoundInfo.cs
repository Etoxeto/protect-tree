using System;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Lua;
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
        [SerializeField] private TextMeshProUGUI roundNum;
        [SerializeField] private TextMeshProUGUI roundState;    // 显示当前回合状态，如“准备阶段”、“战斗中”、“结算阶段”等
        [SerializeField] private TextMeshProUGUI roundTime;
        [SerializeField] private TextMeshProUGUI playerHp;
        [SerializeField] private Button roundReadyButton;
        [SerializeField] private ProtectTree.UIMessageBox messageBoxPrefab;
        [SerializeField] private string mainMenuSceneName = "Menu";

        private LuaRuntime _runtime;
        private TextMeshProUGUI _readyButtonText;
        private ProtectTree.UIMessageBox _messageBox;
        private string _shownResult;
        private int _localPlayerId;
        private bool _canRequestReady;

        private void Awake()
        {
            _readyButtonText = roundReadyButton != null
                ? roundReadyButton.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;
            roundReadyButton?.onClick.AddListener(RequestReady);
            SetReadyButton(false, "准备");
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
            RenderFlow(context.Flow);
            RenderPlayerHp(context.Players, context.LocalPlayerId);
            RenderReadyButton(context.Flow, context.Players, context.LocalPlayerId);
            RenderMatchResult(context.Flow);
        }

        public override void OnRuntimeUnavailable()
        {
            HideMatchResult();
            _runtime = null;
            _localPlayerId = 0;
            _canRequestReady = false;
            SetText(roundNum, "--");
            SetText(roundState, "未开始");
            SetText(roundTime, "--");
            SetText(playerHp, "--");
            SetReadyButton(false, "准备");
        }

        private void RenderFlow(MatchFlowSnapshot flow)
        {
            if (flow == null)
            {
                OnRuntimeUnavailable();
                return;
            }

            SetText(roundNum, flow.IsFinished ? "结束" : $" {flow.Wave} ");
            SetText(roundState, GetPhaseLabel(flow));
            SetText(roundTime, GetTimeLabel(flow));
        }

        private void RenderPlayerHp(PlayerRosterSnapshot players, int localPlayerId)
        {
            PlayerSnapshot player = FindPlayer(players, localPlayerId);
            SetText(
                playerHp,
                player != null ? $"{player.Health}" : "--");
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

            // 结算弹窗只处理流程选择；真正的胜负结果仍然来自 Lua 权威状态。
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
            _shownResult = null;
            _messageBox?.Hide();
        }

        private void RequestReady()
        {
            if (!_canRequestReady || _runtime == null || _localPlayerId <= 0)
            {
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

            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }

        private void SetReadyButton(bool isInteractable, string label)
        {
            if (roundReadyButton != null)
            {
                roundReadyButton.interactable = isInteractable;
            }

            SetText(_readyButtonText, label);
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

        private static string GetPhaseLabel(MatchFlowSnapshot flow)
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
                    return "战斗中";
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

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}
