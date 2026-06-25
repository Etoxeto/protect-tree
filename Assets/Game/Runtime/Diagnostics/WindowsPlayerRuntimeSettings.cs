using UnityEngine;

namespace ProtectTree.Runtime.Diagnostics
{
    public static class WindowsPlayerRuntimeSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // 联机调试常同时打开多个客户端；失焦暂停会让网络 Pump 停住。
            Application.runInBackground = true;
            Debug.Log("[ProtectTree][Runtime] Windows Player runInBackground enabled.");
#endif
        }
    }
}
