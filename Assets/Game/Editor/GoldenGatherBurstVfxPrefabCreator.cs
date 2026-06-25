using System.IO;
using ProtectTree.Runtime.VFX;
using UnityEditor;
using UnityEngine;

namespace ProtectTree.Editor
{
    public static class GoldenGatherBurstVfxPrefabCreator
    {
        private const string PrefabFolder = "Assets/Resources/Prefabs/VFX";
        private const string PrefabPath =
            "Assets/Resources/Prefabs/VFX/GoldenGatherBurstVfx.prefab";

        [MenuItem("Protect Tree/VFX/Create Golden Gather Burst Prefab")]
        public static void CreatePrefab()
        {
            Directory.CreateDirectory(PrefabFolder);

            GameObject root = new GameObject("GoldenGatherBurstVfx");
            root.AddComponent<GoldenGatherBurstVfx>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log($"Created golden gather burst VFX prefab: {PrefabPath}");
        }
    }
}
