using System.Collections.Generic;
using ProtectTree.Runtime.Lua;
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

        private LuaBootstrap _bootstrap;
        private LuaRuntime _observedRuntime;
        private MatchSceneFeature[] _features;

        private void Awake()
        {
            localPlayerId = MatchStartupOptions.LocalPlayerId;
            if (observedPlayerId <= 0)
            {
                observedPlayerId = MatchStartupOptions.InitialObservedPlayerId;
            }

            _bootstrap = GetComponent<LuaBootstrap>();
            _features = FindSceneFeatures();
        }

        public void SetObservedPlayer(int playerId)
        {
            if (playerId <= 0 || observedPlayerId == playerId)
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

            foreach (MatchSceneFeature feature in _features)
            {
                if (feature.isActiveAndEnabled)
                {
                    feature.Refresh(_context);
                }
            }
        }
    }
}
