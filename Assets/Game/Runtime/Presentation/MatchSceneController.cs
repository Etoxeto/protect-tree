using System.Collections.Generic;
using ProtectTree;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Lua;
using ProtectTree.Runtime.Network;
using UnityEngine;

namespace ProtectTree.Runtime.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LuaBootstrap))]
    public sealed class MatchSceneController : MonoBehaviour
    {
        [SerializeField] private int localPlayerId = 1;

        [SerializeField] private int observedPlayerId;

        private readonly MatchSceneContext _context = new MatchSceneContext();
        private readonly MatchAudioFeedback _audioFeedback = new MatchAudioFeedback();

        private LuaBootstrap _bootstrap;
        private LuaRuntime _observedRuntime;
        private MatchSceneFeature[] _features;
        private bool _loggedLanMatchRuntime;
        private bool _isObservedPlayerForced;
        private int _observedPlayerBeforeForced;

        private void Awake()
        {
            localPlayerId = MatchStartupOptions.LocalPlayerId;
            if (observedPlayerId <= 0)
            {
                observedPlayerId = MatchStartupOptions.InitialObservedPlayerId;
            }

            _bootstrap = GetComponent<LuaBootstrap>();
            _features = FindSceneFeatures();

            if (LanMatchRuntime.HasActiveSession)
            {
                Debug.Log(
                    "[ProtectTree][LAN Match] Entered match scene with "
                    + LanMatchRuntime.Instance.Describe(),
                    this);
                _loggedLanMatchRuntime = true;
            }
        }

        public void SetObservedPlayer(int playerId)
        {
            if (playerId <= 0)
            {
                return;
            }

            if (_isObservedPlayerForced)
            {
                _observedPlayerBeforeForced = playerId;
                return;
            }

            if (observedPlayerId == playerId)
            {
                return;
            }

            observedPlayerId = playerId;
            _context.SelectPiece(0);
            _context.SetPieceInspectBlocked(false);
        }

        private MatchSceneFeature[] FindSceneFeatures()
        {
            MatchSceneFeature[] discovered = FindObjectsOfType<MatchSceneFeature>(true);
            List<MatchSceneFeature> sceneFeatures =
                new List<MatchSceneFeature>(discovered.Length);

            foreach (MatchSceneFeature feature in discovered)
            {
                if (feature.gameObject.scene == gameObject.scene)
                {
                    sceneFeatures.Add(feature);
                }
            }

            sceneFeatures.Sort((left, right) =>
                left.RefreshOrder.CompareTo(right.RefreshOrder));

            return sceneFeatures.ToArray();
        }

        private void Update()
        {
            LuaRuntime runtime = _bootstrap.Runtime;
            if (runtime == null || !runtime.IsStarted)
            {
                if (_observedRuntime != null)
                {
                    _observedRuntime = null;
                    _audioFeedback.Reset();
                    _context.Clear();
                    foreach (MatchSceneFeature feature in _features)
                    {
                        feature.OnRuntimeUnavailable();
                    }
                }

                return;
            }

            if (runtime != _observedRuntime)
            {
                _observedRuntime = runtime;
                _audioFeedback.Reset();
                LogLanMatchRuntimeOnce();
                if (LanMatchRuntime.HasActiveSession)
                {
                    LanMatchRuntime.Instance.BindRuntime(runtime);
                }

                foreach (MatchSceneFeature feature in _features)
                {
                    feature.OnRuntimeChanged(runtime);
                }
            }

            _context.Capture(
                runtime,
                localPlayerId,
                observedPlayerId,
                SetObservedPlayer);

            if (ApplyObservationOverride(_context))
            {
                _context.Capture(
                    runtime,
                    localPlayerId,
                    observedPlayerId,
                    SetObservedPlayer);
            }

            foreach (MatchSceneFeature feature in _features)
            {
                if (feature.isActiveAndEnabled)
                {
                    feature.Refresh(_context);
                }
            }

            _audioFeedback.Refresh(_context);
        }

        private void LogLanMatchRuntimeOnce()
        {
            if (_loggedLanMatchRuntime || !LanMatchRuntime.HasActiveSession)
            {
                return;
            }

            Debug.Log(
                "[ProtectTree][LAN Match] Runtime available for "
                + LanMatchRuntime.Instance.Describe(),
                this);
            _loggedLanMatchRuntime = true;
        }

        private bool ApplyObservationOverride(MatchSceneContext context)
        {
            if (TryResolveForcedObservedPlayer(context, out int forcedObservedPlayerId))
            {
                if (!_isObservedPlayerForced)
                {
                    _observedPlayerBeforeForced = observedPlayerId > 0
                        ? observedPlayerId
                        : localPlayerId;
                    _isObservedPlayerForced = true;
                }

                return SwitchObservedPlayerIfNeeded(forcedObservedPlayerId);
            }

            if (!_isObservedPlayerForced)
            {
                return false;
            }

            _isObservedPlayerForced = false;
            int restoredPlayerId = _observedPlayerBeforeForced > 0
                ? _observedPlayerBeforeForced
                : localPlayerId;
            _observedPlayerBeforeForced = 0;

            if (!ContainsPlayer(context.Players, restoredPlayerId))
            {
                restoredPlayerId = localPlayerId;
            }

            return SwitchObservedPlayerIfNeeded(restoredPlayerId);
        }

        private bool SwitchObservedPlayerIfNeeded(int playerId)
        {
            if (playerId <= 0 || observedPlayerId == playerId)
            {
                return false;
            }

            observedPlayerId = playerId;
            _context.SelectPiece(0);
            _context.SetPieceInspectBlocked(false);
            return true;
        }

        private static bool TryResolveForcedObservedPlayer(
            MatchSceneContext context,
            out int playerId)
        {
            playerId = 0;
            if (context.Flow == null
                || (context.Flow.Phase != "JointDefenseIntro"
                    && context.Flow.Phase != "JointDefense"))
            {
                return false;
            }

            playerId = FindJointDefenseDefender(context);
            return playerId > 0 && ContainsPlayer(context.Players, playerId);
        }

        private static int FindJointDefenseDefender(MatchSceneContext context)
        {
            if (context.Events != null)
            {
                foreach (MatchEvent matchEvent in context.Events)
                {
                    if (matchEvent != null
                        && matchEvent.Type == "JointDefenseStarted"
                        && matchEvent.DefenderPlayerId.HasValue)
                    {
                        return matchEvent.DefenderPlayerId.Value;
                    }
                }
            }

            if (context.Enemies == null || context.Flow == null)
            {
                return 0;
            }

            foreach (EnemySnapshot enemy in context.Enemies.Enemies)
            {
                if (enemy.Wave == context.Flow.Wave
                    && !enemy.IsBoss
                    && enemy.Status == "Alive"
                    && enemy.TargetPlayerId > 0)
                {
                    return enemy.TargetPlayerId;
                }
            }

            return 0;
        }

        private static bool ContainsPlayer(
            PlayerRosterSnapshot players,
            int playerId)
        {
            if (players == null)
            {
                return false;
            }

            foreach (PlayerSnapshot player in players.Players)
            {
                if (player.PlayerId == playerId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
