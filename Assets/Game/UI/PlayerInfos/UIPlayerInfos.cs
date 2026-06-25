using System;
using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Runtime;
using ProtectTree.Runtime.Presentation;
using UnityEngine;

namespace ProtectTree.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class UIPlayerInfos : MatchSceneFeature
    {
        [SerializeField] private UIPlayerInfo[] playerInfos = new UIPlayerInfo[4];
        [SerializeField] private bool enableFunctionKeyObservation = true;
        [SerializeField] private string defaultAvatarResourcePath =
            PlayerProfileOptions.DefaultAvatarResourcePath;

        private Sprite _defaultAvatar;
        private readonly HashSet<string> _seenEnemyKeys =
            new HashSet<string>();

        private void Awake()
        {
            _defaultAvatar = UIResourceLoader.LoadSprite(
                PlayerProfileOptions.NormalizeAvatarResourcePath(
                    defaultAvatarResourcePath));
            if (_defaultAvatar == null)
            {
                _defaultAvatar = UIResourceLoader.LoadSprite(
                    PlayerProfileOptions.DefaultAvatarResourcePath);
            }

            HideAll();
        }

        public override void Refresh(MatchSceneContext context)
        {
            if (context.Players == null || playerInfos == null)
            {
                HideAll();
                return;
            }

            RememberSeenEnemies(context);

            int index = 0;
            foreach (PlayerSnapshot player in context.Players.Players)
            {
                if (index >= playerInfos.Length)
                {
                    break;
                }

                Action<int> observePlayer = context.RequestObservePlayer;
                playerInfos[index]?.Render(
                    player,
                    ResolveAvatar(context, player),
                    player.PlayerId == context.ObservedPlayerId,
                    ShouldShowConfirmFx(context, player),
                    observePlayer);
                index++;
            }

            for (; index < playerInfos.Length; index++)
            {
                playerInfos[index]?.Hide();
            }

            HandleObservationShortcuts(context);
        }

        public override void OnRuntimeUnavailable()
        {
            HideAll();
        }

        private void HideAll()
        {
            if (playerInfos == null)
            {
                return;
            }

            foreach (UIPlayerInfo playerInfo in playerInfos)
            {
                playerInfo?.Hide();
            }
        }

        private void HandleObservationShortcuts(MatchSceneContext context)
        {
            if (!enableFunctionKeyObservation || context.Players == null)
            {
                return;
            }

            int requestedPlayerId = Input.GetKeyDown(KeyCode.F1)
                ? 1
                : Input.GetKeyDown(KeyCode.F2)
                    ? 2
                    : Input.GetKeyDown(KeyCode.F3)
                        ? 3
                        : Input.GetKeyDown(KeyCode.F4) ? 4 : 0;
            if (requestedPlayerId <= 0)
            {
                return;
            }

            foreach (PlayerSnapshot player in context.Players.Players)
            {
                if (player.PlayerId == requestedPlayerId)
                {
                    context.RequestObservePlayer(requestedPlayerId);
                    return;
                }
            }
        }

        private void RememberSeenEnemies(MatchSceneContext context)
        {
            if (context.Flow == null)
            {
                return;
            }

            if (context.Enemies != null)
            {
                foreach (EnemySnapshot enemy in context.Enemies.Enemies)
                {
                    if (enemy != null && !enemy.IsBoss)
                    {
                        _seenEnemyKeys.Add(CreateEnemyKey(
                            enemy.Wave,
                            enemy.TargetPlayerId,
                            enemy.InstanceId));
                    }
                }
            }

            if (context.Events == null)
            {
                return;
            }

            foreach (MatchEvent matchEvent in context.Events)
            {
                if (matchEvent == null
                    || matchEvent.IsBoss == true
                    || !matchEvent.Wave.HasValue
                    || !matchEvent.TargetPlayerId.HasValue
                    || !matchEvent.EnemyInstanceId.HasValue)
                {
                    continue;
                }

                if (matchEvent.Type == "EnemyCreated"
                    || matchEvent.Type == "EnemyDefeated"
                    || matchEvent.Type == "EnemyReachedEndpoint")
                {
                    _seenEnemyKeys.Add(CreateEnemyKey(
                        matchEvent.Wave.Value,
                        matchEvent.TargetPlayerId.Value,
                        matchEvent.EnemyInstanceId.Value));
                }
            }
        }

        private bool ShouldShowConfirmFx(
            MatchSceneContext context,
            PlayerSnapshot player)
        {
            if (context.Flow == null || player == null || player.Status != "Alive")
            {
                return false;
            }

            switch (context.Flow.Phase)
            {
                case "Preparation":
                case "BossPreparation":
                    return player.IsReady;
                case "Battle":
                    return HasSeenEnemyForPlayer(context.Flow.Wave, player.PlayerId)
                        && !HasActiveEnemyForPlayer(context, player.PlayerId);
                case "JointDefenseIntro":
                case "JointDefense":
                case "Settlement":
                    return true;
                default:
                    return false;
            }
        }

        private bool HasSeenEnemyForPlayer(int wave, int playerId)
        {
            string prefix = wave + ":" + playerId + ":";
            foreach (string key in _seenEnemyKeys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasActiveEnemyForPlayer(
            MatchSceneContext context,
            int playerId)
        {
            if (context.Enemies == null || context.Flow == null)
            {
                return false;
            }

            foreach (EnemySnapshot enemy in context.Enemies.Enemies)
            {
                if (enemy.Wave == context.Flow.Wave
                    && enemy.TargetPlayerId == playerId
                    && !enemy.IsBoss
                    && enemy.Status == "Alive")
                {
                    return true;
                }
            }

            return false;
        }

        private static string CreateEnemyKey(int wave, int playerId, int enemyInstanceId)
        {
            return wave + ":" + playerId + ":" + enemyInstanceId;
        }

        private Sprite ResolveAvatar(MatchSceneContext context, PlayerSnapshot player)
        {
            string avatarResourcePath = null;
            if (context.LanMatch != null && context.LanMatch.IsActive)
            {
                avatarResourcePath =
                    context.LanMatch.GetPlayerAvatarResourcePath(player.PlayerId);
            }

            if (string.IsNullOrWhiteSpace(avatarResourcePath)
                && player.PlayerId == context.LocalPlayerId)
            {
                avatarResourcePath = PlayerProfileOptions.AvatarResourcePath;
            }

            if (!string.IsNullOrWhiteSpace(avatarResourcePath))
            {
                Sprite avatar = UIResourceLoader.LoadSprite(avatarResourcePath);
                if (avatar != null)
                {
                    return avatar;
                }
            }

            return _defaultAvatar;
        }
    }
}
