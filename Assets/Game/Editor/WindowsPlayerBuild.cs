using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProtectTree.Editor
{
    public static class WindowsLuaUpdateTools
    {
        private const string PlayerLuaFolder = "ProtectTreeLua";

        [MenuItem("Protect Tree/Lua/Deploy Scripts To Windows Update Folder")]
        public static void DeployScripts()
        {
            string sourceRoot = Path.Combine(Application.dataPath, "Game", "Lua");
            string destinationRoot = GetWindowsUpdateRoot();

            LuaScriptCopyUtility.CopyLuaScripts(sourceRoot, destinationRoot);
            Debug.Log($"[ProtectTree][Lua Update] Deployed scripts to: {destinationRoot}");
        }

        [MenuItem("Protect Tree/Lua/Clear Windows Update Scripts")]
        public static void ClearScripts()
        {
            string updateRoot = GetWindowsUpdateRoot();

            if (Directory.Exists(updateRoot))
            {
                Directory.Delete(updateRoot, true);
            }

            Debug.Log($"[ProtectTree][Lua Update] Cleared scripts from: {updateRoot}");
        }

        private static string GetWindowsUpdateRoot()
        {
            return Path.Combine(Application.persistentDataPath, PlayerLuaFolder);
        }
    }

    public static class WindowsPlayerBuild
    {
        private const string BuildPath = "Builds/Windows/ProtectTree.exe";

        [MenuItem("Protect Tree/Build Windows Player")]
        public static void Build()
        {
            ConfigureWindowedPlayerSettings();

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes exist in Build Settings.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath)
                ?? throw new InvalidOperationException("Windows build directory is invalid."));

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Windows Player build failed: {report.summary.result}");
            }

            Debug.Log($"[ProtectTree][Build] Windows Player: {report.summary.outputPath}");
        }

        private static void ConfigureWindowedPlayerSettings()
        {
            // 联机调试通常需要双开客户端，默认窗口化比全屏更容易观察和操作。
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.runInBackground = true;
        }
    }

    public sealed class WindowsLuaBuildProcessor : IPostprocessBuildWithReport
    {
        private const string PlayerLuaFolder = "ProtectTreeLua";

        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64)
            {
                return;
            }

            string sourceRoot = Path.Combine(Application.dataPath, "Game", "Lua");
            string playerDirectory = Path.GetDirectoryName(report.summary.outputPath)
                ?? throw new BuildFailedException("Windows Player output directory is invalid.");
            string playerName = Path.GetFileNameWithoutExtension(report.summary.outputPath);
            string destinationRoot = Path.Combine(
                playerDirectory,
                playerName + "_Data",
                "StreamingAssets",
                PlayerLuaFolder);

            LuaScriptCopyUtility.CopyLuaScripts(sourceRoot, destinationRoot);
            Debug.Log($"[ProtectTree][Build] Copied Lua scripts to: {destinationRoot}");
        }
    }

    internal static class LuaScriptCopyUtility
    {
        public static void CopyLuaScripts(string sourceRoot, string destinationRoot)
        {
            if (Directory.Exists(destinationRoot))
            {
                Directory.Delete(destinationRoot, true);
            }

            foreach (string sourcePath in Directory.GetFiles(
                         sourceRoot,
                         "*.lua.txt",
                         SearchOption.AllDirectories))
            {
                string relativePath = sourcePath.Substring(sourceRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destinationPath = Path.Combine(destinationRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)
                    ?? throw new InvalidOperationException("Lua destination directory is invalid."));
                File.Copy(sourcePath, destinationPath, true);
            }
        }
    }
}
