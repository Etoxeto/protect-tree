using UnityEngine;

namespace ProtectTree.Runtime.Lua
{
    public static class PackagedResourcesLuaLoader
    {
        public static byte[] Load(ref string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                return null;
            }

            string resourcePath = moduleName.Replace('.', '/') + ".lua";
            TextAsset script = Resources.Load<TextAsset>(resourcePath);

            if (script == null)
            {
                return null;
            }

            byte[] bytes = script.bytes;
            Resources.UnloadAsset(script);
            moduleName = "Resources/" + resourcePath;
            Debug.Log($"[ProtectTree][Lua Loader] Loaded: {moduleName}");
            return bytes;
        }
    }
}
