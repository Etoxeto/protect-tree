using System;
using System.IO;
using System.Linq;
using System.Text;
using ProtectTree.Runtime.Lua;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProtectTree.Editor
{
    public static class AndroidPlayerBuild
    {
        private const string BuildPath = "Builds/Android/ProtectTree.apk";
        private const string GeneratedRoot = "Assets/Game/Generated";
        private const string GeneratedResourcesRoot = GeneratedRoot + "/Resources";

        [MenuItem("Protect Tree/Build Android Player")]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new BuildFailedException(
                    "Android Build Support is not installed for this Unity Editor.");
            }

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android,
                ScriptingImplementation.IL2CPP);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes exist in Build Settings.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath)
                ?? throw new InvalidOperationException("Android build directory is invalid."));

            StagePackagedScripts();

            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = BuildPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development,
                });

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        $"Android Player build failed: {report.summary.result}");
                }

                Debug.Log($"[ProtectTree][Build] Android Player: {report.summary.outputPath}");
            }
            finally
            {
                ClearPackagedScripts();
            }
        }

        [MenuItem("Protect Tree/Lua/Validate Android Packaged Scripts")]
        public static void ValidatePackagedScripts()
        {
            StagePackagedScripts();

            try
            {
                string moduleName = "Config.ScriptVersion";
                byte[] bytes = PackagedResourcesLuaLoader.Load(ref moduleName);

                if (bytes == null || bytes.Length == 0)
                {
                    throw new InvalidOperationException(
                        "The staged Android Lua resource could not be loaded.");
                }

                Debug.Log(
                    $"[ProtectTree][Android] Validated packaged Lua resource: " +
                    $"{moduleName}, contents={Encoding.UTF8.GetString(bytes).Trim()}");
            }
            finally
            {
                ClearPackagedScripts();
            }
        }

        private static void StagePackagedScripts()
        {
            string sourceRoot = Path.Combine(Application.dataPath, "Game", "Lua");
            string destinationRoot = Path.Combine(
                Application.dataPath,
                "Game",
                "Generated",
                "Resources");

            LuaScriptCopyUtility.CopyLuaScripts(sourceRoot, destinationRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[ProtectTree][Android] Staged Lua resources: {destinationRoot}");
        }

        private static void ClearPackagedScripts()
        {
            if (AssetDatabase.IsValidFolder(GeneratedRoot))
            {
                AssetDatabase.DeleteAsset(GeneratedRoot);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[ProtectTree][Android] Cleared staged Lua resources.");
        }
    }
}
