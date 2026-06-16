using System;
using System.Collections.Generic;
using CSObjectWrapEditor;
using ProtectTree.LuaContracts;
using UnityEngine;
using XLua;

namespace ProtectTree.Editor.Lua
{
    public static class ProtectTreeXLuaConfig
    {
        [GenPath]
        public static string GeneratedCodePath => Application.dataPath + "/XLua/Src/Gen/";

        [CSharpCallLua]
        public static List<Type> CSharpCallLuaTypes => new List<Type>
        {
            typeof(BuildWelcomeDelegate),
            typeof(LuaLifecycleAction),
            typeof(LuaReloadModuleAction),
            typeof(LuaUpdateAction),
        };

        [LuaCallCSharp]
        public static List<Type> LuaCallCSharpTypes => new List<Type>
        {
            typeof(LuaLearningApi),
        };
    }
}
