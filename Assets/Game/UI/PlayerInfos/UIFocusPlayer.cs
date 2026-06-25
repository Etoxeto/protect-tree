using ProtectTree.Core.Match;
using ProtectTree.Runtime.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree
{
    public class UIFocusPlayer : MatchSceneFeature
    {
        [SerializeField] private Image Bg;
        [SerializeField] private Image observeIcon;
        [SerializeField] private TextMeshProUGUI playerName;

        public override void Refresh(MatchSceneContext context)
        {
            if (context == null || context.Players == null || context.Flow == null)
            {
                SetVisible(false);
                return;
            }

            int observedPlayerId = context.ObservedPlayerId;
            PlayerSnapshot observedPlayer = FindPlayer(context.Players, observedPlayerId);
            if (observedPlayer == null)
            {
                SetVisible(false);
                return;
            }

            string phase = context.Flow.Phase;
            bool isJointDefense =
                phase == "JointDefenseIntro" || phase == "JointDefense";
            bool isWatchingSelf = observedPlayerId == context.LocalPlayerId;
            bool shouldShow = isJointDefense || !isWatchingSelf;

            if (!shouldShow)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            if (observeIcon != null)
            {
                observeIcon.gameObject.SetActive(!isWatchingSelf);
            }

            if (playerName != null)
            {
                playerName.text = BuildLabel(
                    phase,
                    observedPlayerId,
                    isWatchingSelf);
            }
        }

        public override void OnRuntimeUnavailable()
        {
            SetVisible(false);
        }

        private void SetVisible(bool isVisible)
        {
            if (Bg != null)
            {
                Bg.gameObject.SetActive(isVisible);
            }

            if (observeIcon != null)
            {
                observeIcon.gameObject.SetActive(isVisible);
            }

            if (playerName != null)
            {
                playerName.gameObject.SetActive(isVisible);
            }
        }

        private static string BuildLabel(
            string phase,
            int observedPlayerId,
            bool isWatchingSelf)
        {
            string playerLabel = isWatchingSelf ? "自己" : $"P{observedPlayerId}";
            if (phase == "BossBattle")
            {
                return $"观察：{playerLabel}";
            }

            switch (phase)
            {
                case "JointDefenseIntro":
                case "JointDefense":
                    return $"联防：{playerLabel}";
                case "BossBattle":
                    return $"Boss目标：{playerLabel}";
                default:
                    return $"观察：{playerLabel}";
            }
        }

        private static PlayerSnapshot FindPlayer(
            PlayerRosterSnapshot players,
            int playerId)
        {
            foreach (PlayerSnapshot player in players.Players)
            {
                if (player.PlayerId == playerId)
                {
                    return player;
                }
            }

            return null;
        }
    }
}
