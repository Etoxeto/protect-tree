using UnityEngine;
using ProtectTree.Runtime.Network;

namespace ProtectTree.Runtime.Lua
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class LuaBootstrap : MonoBehaviour
    {
        [SerializeField]
        private string entryModule = "Bootstrap.Main";

        [SerializeField]
        private string reloadModule = "Bootstrap.LifecycleDemo";

        private LuaRuntime _runtime;
        private bool _loggedLanClientTickPaused;

        public LuaRuntime Runtime => _runtime;

        private void Awake()
        {
            StartLua();
        }

        private void Update()
        {
            if (_runtime == null)
            {
                return;
            }

            if (ShouldPauseLocalSimulationForLanClient())
            {
                if (!_loggedLanClientTickPaused)
                {
                    Debug.Log(
                        "[ProtectTree][LAN Match] Local Lua simulation tick paused on Client; authoritative snapshots drive gameplay.",
                        this);
                    _loggedLanClientTickPaused = true;
                }

                return;
            }

            _runtime.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            StopLua();
        }

        [ContextMenu("Start Lua")]
        public void StartLua()
        {
            if (_runtime != null)
            {
                return;
            }

            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start(entryModule);
                ApplyStartupOptions(runtime);
                string welcome = runtime.BuildWelcome("Player Two");
                Debug.Log($"[ProtectTree][CSharp Delegate] {welcome}", this);
                _runtime = runtime;
            }
            catch
            {
                runtime.Dispose();
                throw;
            }

            Debug.Log($"[ProtectTree][XLua] Started entry module: {entryModule}", this);
        }

        private static bool ShouldPauseLocalSimulationForLanClient()
        {
            LanMatchRuntime lanMatch = LanMatchRuntime.Instance;
            return lanMatch != null && lanMatch.IsActive && lanMatch.IsClient;
        }

        private static void ApplyStartupOptions(LuaRuntime runtime)
        {
            if (!MatchStartupOptions.IsLocalMultiplayer)
            {
                return;
            }

            runtime.StartLocalMultiplayer(MatchStartupOptions.PlayerCount);
        }

        [ContextMenu("Stop Lua")]
        public void StopLua()
        {
            _runtime?.Dispose();
            _runtime = null;
        }

        [ContextMenu("Reload Lua Module")]
        public void ReloadLuaModule()
        {
            if (_runtime == null)
            {
                Debug.LogWarning("[ProtectTree][XLua] Start Lua before reloading a module.", this);
                return;
            }

            _runtime.ReloadModule(reloadModule);
            Debug.Log($"[ProtectTree][XLua] Reloaded module: {reloadModule}", this);
        }
    }
}
