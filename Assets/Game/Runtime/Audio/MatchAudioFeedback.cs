using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Presentation;
using UnityEngine;

namespace ProtectTree
{
    public sealed class MatchAudioFeedback
    {
        private const string NormalBgmId = "bgm_normal";
        private const string BattleBgmId = "bgm_battle";
        private const string BossBgmId = "bgm_goatBoss";

        private const string MagicPowerSfxId = "atk_magic_02";
        private const string StormSfxId = "atk_storm_01";
        private const string ImpactSfxId = "atk_impact_01";
        private const string EnemyDefeatedSfxId = "die_01";
        private const string LeakSfxId = "battle_leak";
        private const string ClearSfxId = "battle_clear";

        private readonly HashSet<IReadOnlyList<MatchEvent>> _processedEventLists =
            new HashSet<IReadOnlyList<MatchEvent>>();
        private readonly HashSet<string> _playedClearKeys = new HashSet<string>();

        private string _currentBgmId;
        private float _lastAttackTime;
        private float _lastImpactTime;
        private float _lastDefeatedTime;
        private float _lastLeakTime;

        public void Reset()
        {
            _processedEventLists.Clear();
            _playedClearKeys.Clear();
            _currentBgmId = null;
            _lastAttackTime = 0f;
            _lastImpactTime = 0f;
            _lastDefeatedTime = 0f;
            _lastLeakTime = 0f;
        }

        public void Refresh(MatchSceneContext context)
        {
            if (context == null || context.Flow == null)
            {
                return;
            }

            UpdateBgm(context.Flow);
            HandleEvents(context);
        }

        private void UpdateBgm(MatchFlowSnapshot flow)
        {
            string bgmId = GetBgmId(flow);
            if (_currentBgmId == bgmId)
            {
                return;
            }

            _currentBgmId = bgmId;
            if (string.IsNullOrEmpty(bgmId))
            {
                AudioManager.StopBgm();
                return;
            }

            AudioManager.PlayBgm(bgmId);
        }

        private static string GetBgmId(MatchFlowSnapshot flow)
        {
            if (flow.IsFinished || flow.Phase == "End")
            {
                return null;
            }

            switch (flow.Phase)
            {
                case "Battle":
                case "JointDefenseIntro":
                case "JointDefense":
                    return BattleBgmId;
                case "BossBattle":
                    return BossBgmId;
                default:
                    return NormalBgmId;
            }
        }

        private void HandleEvents(MatchSceneContext context)
        {
            IReadOnlyList<MatchEvent> events = context.Events;
            if (events == null || events.Count == 0)
            {
                return;
            }

            if (_processedEventLists.Contains(events))
            {
                return;
            }

            _processedEventLists.Add(events);
            if (_processedEventLists.Count > 32)
            {
                _processedEventLists.Clear();
                _processedEventLists.Add(events);
            }

            foreach (MatchEvent matchEvent in events)
            {
                if (matchEvent == null)
                {
                    continue;
                }

                switch (matchEvent.Type)
                {
                    case "EnemyDamageRequested":
                        PlayAttackSfx(context.Pieces, matchEvent);
                        break;
                    case "PieceDamageRequested":
                        PlayEnemyAttackSfx(context.Enemies, matchEvent);
                        break;
                    case "EnemyDamaged":
                    case "PieceDamaged":
                        PlayThrottled(ImpactSfxId, ref _lastImpactTime, 0.08f);
                        break;
                    case "EnemyDefeated":
                        PlayThrottled(
                            EnemyDefeatedSfxId,
                            ref _lastDefeatedTime,
                            0.08f);
                        break;
                    case "PlayerDamaged":
                        if (matchEvent.PlayerId == context.LocalPlayerId)
                        {
                            PlayThrottled(LeakSfxId, ref _lastLeakTime, 0.1f);
                        }
                        break;
                    case "PlayerLeakResolved":
                        TryPlayClearSfx(context, matchEvent);
                        break;
                }
            }
        }

        private void PlayAttackSfx(
            PieceRosterSnapshot pieces,
            MatchEvent matchEvent)
        {
            string sfxId = GetAttackSfxId(pieces, matchEvent);
            if (string.IsNullOrEmpty(sfxId))
            {
                return;
            }

            PlayThrottled(sfxId, ref _lastAttackTime, 0.06f);
        }

        private void PlayEnemyAttackSfx(
            EnemyRosterSnapshot enemies,
            MatchEvent matchEvent)
        {
            string sfxId = GetEnemyAttackSfxId(enemies, matchEvent);
            if (string.IsNullOrEmpty(sfxId))
            {
                return;
            }

            PlayThrottled(sfxId, ref _lastAttackTime, 0.06f);
        }

        private static string GetAttackSfxId(
            PieceRosterSnapshot pieces,
            MatchEvent matchEvent)
        {
            if (matchEvent.ProjectileId == "Storm")
            {
                return StormSfxId;
            }

            if (matchEvent.ProjectileId == "MagicPower")
            {
                return MagicPowerSfxId;
            }

            PieceSnapshot sourcePiece = FindPiece(
                pieces,
                matchEvent.SourcePieceInstanceId);
            if (sourcePiece == null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(sourcePiece.AttackSfxId)
                ? null
                : sourcePiece.AttackSfxId;
        }

        private static string GetEnemyAttackSfxId(
            EnemyRosterSnapshot enemies,
            MatchEvent matchEvent)
        {
            EnemySnapshot sourceEnemy = FindEnemy(
                enemies,
                matchEvent.SourceEnemyInstanceId);
            if (sourceEnemy == null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(sourceEnemy.AttackSfxId)
                ? null
                : sourceEnemy.AttackSfxId;
        }

        private void TryPlayClearSfx(
            MatchSceneContext context,
            MatchEvent matchEvent)
        {
            if (matchEvent.PlayerId != context.LocalPlayerId
                || matchEvent.FinalLeakCount.GetValueOrDefault() != 0)
            {
                return;
            }

            string key = $"{matchEvent.Wave.GetValueOrDefault()}:{matchEvent.PlayerId}";
            if (!_playedClearKeys.Add(key))
            {
                return;
            }

            AudioManager.PlaySfx(ClearSfxId);
        }

        private void PlayThrottled(string sfxId, ref float lastPlayTime, float cooldown)
        {
            float now = Time.unscaledTime;
            if (now - lastPlayTime < cooldown)
            {
                return;
            }

            lastPlayTime = now;
            AudioManager.PlaySfx(sfxId);
        }

        private static PieceSnapshot FindPiece(
            PieceRosterSnapshot pieces,
            int? pieceInstanceId)
        {
            if (pieces == null || !pieceInstanceId.HasValue)
            {
                return null;
            }

            foreach (PieceSnapshot piece in pieces.Pieces)
            {
                if (piece.InstanceId == pieceInstanceId.Value)
                {
                    return piece;
                }
            }

            return null;
        }

        private static EnemySnapshot FindEnemy(
            EnemyRosterSnapshot enemies,
            int? enemyInstanceId)
        {
            if (enemies == null || !enemyInstanceId.HasValue)
            {
                return null;
            }

            foreach (EnemySnapshot enemy in enemies.Enemies)
            {
                if (enemy.InstanceId == enemyInstanceId.Value)
                {
                    return enemy;
                }
            }

            return null;
        }
    }
}
