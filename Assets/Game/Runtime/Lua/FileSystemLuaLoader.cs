using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ProtectTree.Runtime.Lua
{
    public sealed class FileSystemLuaLoader
    {
        private const string LuaFileSuffix = ".lua.txt";

        private readonly IReadOnlyList<string> _rootPaths;
        private readonly IReadOnlyList<string> _rootPathsWithSeparator;

        public FileSystemLuaLoader(params string[] rootPaths)
        {
            if (rootPaths == null || rootPaths.Length == 0)
            {
                throw new ArgumentException("At least one Lua script root path is required.", nameof(rootPaths));
            }

            _rootPaths = rootPaths
                .Where(rootPath => !string.IsNullOrWhiteSpace(rootPath))
                .Select(Path.GetFullPath)
                .ToArray();

            if (_rootPaths.Count == 0)
            {
                throw new ArgumentException("At least one Lua script root path is required.", nameof(rootPaths));
            }

            _rootPathsWithSeparator = _rootPaths
                .Select(rootPath => rootPath.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar)
                .ToArray();
        }

        public static FileSystemLuaLoader CreateForProjectScripts()
        {
#if UNITY_EDITOR
            return new FileSystemLuaLoader(Path.Combine(Application.dataPath, "Game", "Lua"));
#elif UNITY_ANDROID
            return new FileSystemLuaLoader(
                Path.Combine(Application.persistentDataPath, "ProtectTreeLua"));
#else
            return new FileSystemLuaLoader(
                Path.Combine(Application.persistentDataPath, "ProtectTreeLua"),
                Path.Combine(Application.streamingAssetsPath, "ProtectTreeLua"));
#endif
        }

        public byte[] Load(ref string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                return null;
            }

            string relativePath = moduleName
                .Replace('.', Path.DirectorySeparatorChar)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                + LuaFileSuffix;
            for (int index = 0; index < _rootPaths.Count; index++)
            {
                string fullPath = Path.GetFullPath(Path.Combine(_rootPaths[index], relativePath));

                if (!fullPath.StartsWith(
                        _rootPathsWithSeparator[index],
                        StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(fullPath))
                {
                    continue;
                }

                moduleName = fullPath;
                Debug.Log($"[ProtectTree][Lua Loader] Loaded: {fullPath}");
                return File.ReadAllBytes(fullPath);
            }

            return null;
        }
    }
}
