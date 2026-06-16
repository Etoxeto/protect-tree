# XLua Startup Experiment

This experiment is intentionally performed in the Unity Editor by the project
owner. The XLua foundation is already installed and verified.

## Current Startup Flow

1. A scene GameObject owns `LuaBootstrap`.
2. `LuaBootstrap.Awake` creates `LuaRuntime`.
3. `LuaRuntime` creates one `LuaEnv` and registers `FileSystemLuaLoader`.
4. The runtime calls `require("Bootstrap.Main")`.
5. `LuaBootstrap.Update` calls `LuaEnv.Tick`.
6. `LuaBootstrap.OnDestroy` disposes the Lua environment.

The current file-system loader is an M1 learning loader. It reads
`Assets/Game/Lua` directly and is intended for Editor experiments only.
Addressables or another packaged loader will replace it before player builds.

## Experiment 1 - Start XLua From A Scene

1. Wait until Unity finishes compiling. Use `Assets > Refresh` if the new files
   are not visible.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Create an empty GameObject named `LuaBootstrap`.
4. Add the `Lua Bootstrap` component.
5. Keep `Entry Module` as `Bootstrap.Main`.
6. Clear the Console and enter Play Mode.

Expected Console output:

```text
LUA: [ProtectTree][Lua] Bootstrap.Main loaded
[ProtectTree][XLua] Started entry module: Bootstrap.Main
```

Stop Play Mode and confirm that no XLua disposal exception appears.

## Experiment 2 - Replace A Lua Module

1. Open `Assets/Game/Lua/Bootstrap/Main.lua.txt`.
2. Change the text inside the existing `print` call.
3. Enter Play Mode again.
4. Confirm that the Console displays the new text without changing C# code.

This is a script replacement experiment, not live reload during Play Mode.
`require` caches loaded modules for the lifetime of one `LuaEnv`.

## Experiment 3 - Call C# From Lua

Add the following before `return M` in `Main.lua.txt`:

```lua
local max_players = CS.ProtectTree.Runtime.ProjectRuntime.MaxSupportedPlayers
print("[ProtectTree][Lua] Max players from C#: " .. max_players)
```

Enter Play Mode and confirm the Console prints `4`.

## Files To Read

- `Assets/Game/Runtime/Lua/LuaBootstrap.cs`
- `Assets/Game/Runtime/Lua/LuaRuntime.cs`
- `Assets/Game/Runtime/Lua/FileSystemLuaLoader.cs`
- `Assets/Game/Lua/Bootstrap/Main.lua.txt`

## Experiment 4 - Bind A Lua Function To A C# Delegate

This experiment replaces repeated string lookup plus `LuaFunction.Call` with a
typed C# delegate that is bound once during startup.

Infrastructure prepared for this experiment:

- `ProtectTree.LuaContracts` owns delegate signatures without depending on
  Unity or XLua.
- `BuildWelcomeDelegate` describes `string Function(string playerName)`.
- `ProtectTreeXLuaConfig` marks the delegate as `CSharpCallLua`.
- XLua generated code is configured to enter `Assets/XLua/Src/Gen`, where it
  compiles into `XLua.Runtime`.

### Generate The Delegate Bridge

1. Wait for Unity compilation to finish.
2. Select `XLua > Generate Code`.
3. Confirm that `Assets/XLua/Src/Gen/DelegatesGensBridge.cs` exists.
4. Open it and find `ProtectTree.LuaContracts.BuildWelcomeDelegate`.

Generated XLua code must be committed. Regenerate it whenever configured
delegate signatures or generated C# bindings change.

### Bind The Delegate

In `LuaRuntime.cs`, import the contracts namespace:

```csharp
using ProtectTree.LuaContracts;
```

Add a field:

```csharp
private BuildWelcomeDelegate _buildWelcome;
```

After `_entryModule` is assigned in `Start`, bind the Lua function:

```csharp
_buildWelcome = _entryModule.Get<BuildWelcomeDelegate>("build_welcome")
    ?? throw new InvalidOperationException("Lua function not found: build_welcome");
```

Add a typed public method:

```csharp
public string BuildWelcome(string playerName)
{
    if (!IsStarted || _buildWelcome == null)
    {
        throw new InvalidOperationException("The Lua runtime has not started.");
    }

    return _buildWelcome(playerName);
}
```

Before disposing the entry table and Lua environment, release the delegate:

```csharp
_buildWelcome = null;
```

In `LuaBootstrap.StartLua`, replace the previous `CallEntryFunction` experiment
with:

```csharp
string welcome = runtime.BuildWelcome("Player Two");
Debug.Log($"[ProtectTree][CSharp Delegate] {welcome}", this);
```

Expected Console output:

```text
[ProtectTree][CSharp Delegate] Welcome Player Two, message built by Lua
```

The typed delegate avoids repeated table lookup, temporary `LuaFunction`
objects, `object[]` arguments, and manual result casting on every call.

## Experiment 5 - Call Generated C# Wrappers From Lua

`LuaLearningApi` is a small pure C# API exposed through the `LuaCallCSharp`
configuration. XLua generates a wrapper for it so Lua can call the API without
using reflection as the primary invocation path.

### Generate The C# Wrapper

1. Wait for Unity compilation to finish.
2. Select `XLua > Generate Code`.
3. Find the generated file whose name ends with `LuaLearningApiWrap.cs` under
   `Assets/XLua/Src/Gen`.
4. Open it and locate the generated wrappers for `Add` and `MaxPlayers`.

### Call The API

Add the following before `return M` in `Main.lua.txt`:

```lua
local sum = CS.ProtectTree.LuaContracts.LuaLearningApi.Add(7, 5)
local generated_max = CS.ProtectTree.LuaContracts.LuaLearningApi.MaxPlayers
print("[ProtectTree][LuaCallCSharp] sum=" .. sum .. ", max=" .. generated_max)
```

Enter Play Mode. Expected Console output:

```text
LUA: [ProtectTree][LuaCallCSharp] sum=12, max=4
```

Generated wrappers are especially important for AOT platforms such as Android
IL2CPP, where code that exists only through runtime reflection may be stripped
or unavailable. Regenerate and commit XLua generated code whenever the
`LuaCallCSharp` list or an exposed API signature changes.

## Experiment 6 - Lua Module Lifecycle

The C# runtime now binds three typed Lua callbacks:

- `start()` runs once after the entry module is loaded.
- `update(delta_time)` runs once per Unity frame.
- `shutdown()` runs once before the Lua environment is disposed.

`Bootstrap.Main` is the stable entry module. It uses
`require("Bootstrap.LifecycleDemo")` and forwards lifecycle calls to the
separate module. Future gameplay modules will follow the same pattern.

`LuaRuntime` owns the typed entry callbacks. During disposal it invokes
`shutdown`, explicitly releases each callback's XLua delegate bridge, and only
then disposes the entry table and `LuaEnv`. Disposing the environment while a
C# callback bridge is still alive causes XLua to throw
`try to dispose a LuaEnv with C# callback`.

### Generate The New Delegate Bridges

1. Wait for Unity compilation to finish.
2. Select `XLua > Generate Code`.
3. Open `Assets/XLua/Src/Gen/DelegatesGensBridge.cs`.
4. Confirm that it contains `LuaLifecycleAction` and `LuaUpdateAction`.

### Observe The Lifecycle

1. Clear the Console and enter Play Mode.
2. Confirm that `start` and `first update` appear once.
3. Leave Play Mode and confirm that `shutdown` reports the update count and
   elapsed time.

### Module Experiment

In `Bootstrap/LifecycleDemo.lua.txt`, change the first-update condition so it
prints once after one second has elapsed:

```lua
local has_printed_one_second = false

if not has_printed_one_second and elapsed_seconds >= 1 then
    has_printed_one_second = true
    print("[ProtectTree][Lua Lifecycle] reached one second")
end
```

The new variable belongs near the other module-local state. The condition
belongs inside `M.update`. Enter Play Mode again and confirm that the message
appears once, roughly one second after startup.

## Experiment 7 - Reload One Lua Module During Play Mode

Lua's `require` caches loaded modules in `package.loaded`. Editing a file does
not change a module that has already been required by the current `LuaEnv`.
The entry module now exposes `reload_module(module_name)` to intentionally
replace the reloadable lifecycle module without recreating the whole runtime.

The reload flow is:

1. Call `shutdown` on the current module.
2. Remove its value from `package.loaded`.
3. Call `require` to read and execute the updated file.
4. Replace the active module reference and call its `start`.

If loading fails, the old module is restored and restarted before the error is
reported to C#.

### Generate The Reload Delegate Bridge

1. Wait for Unity compilation to finish.
2. Select `XLua > Generate Code`.
3. Confirm that `DelegatesGensBridge.cs` contains `LuaReloadModuleAction`.

### Reload During Play Mode

1. Enter Play Mode and wait for the one-second lifecycle message.
2. While still in Play Mode, change the message in
   `Bootstrap/LifecycleDemo.lua.txt` and save the file.
3. Select the GameObject with `LuaBootstrap`.
4. Open the component context menu and select `Reload Lua Module`.
5. Confirm that the Console shows the old module's `shutdown`, followed by the
   new module's `start` and updated message.

This is a development-time module reload exercise. Production hot update still
requires downloading versioned scripts, verifying them, and loading them from
a packaged or persistent-data location.

## Experiment 8 - Windows Player Script Loading

The Editor reads Lua directly from `Assets/Game/Lua`. A Windows Player instead
uses two file-system roots in priority order:

1. `Application.persistentDataPath/ProtectTreeLua`
2. `Application.streamingAssetsPath/ProtectTreeLua`

The first root represents downloaded hot-update scripts. The second root is
the initial script set shipped with the Player. The Windows build processor
copies `.lua.txt` files into that packaged root after each successful build.

### Build The Windows Player

1. Select `Protect Tree > Build Windows Player`.
2. Open `Builds/Windows/ProtectTree_Data/StreamingAssets/ProtectTreeLua`.
3. Confirm that the `Bootstrap` Lua scripts were copied.
4. Run `Builds/Windows/ProtectTree.exe`.
5. Open the Player log and confirm that the normal XLua startup and lifecycle
   messages appear without a missing-module error.

This file-system implementation is suitable for Windows. Android packages
StreamingAssets inside the APK, so Android needs a different packaged-script
reader before its smoke test.

## Experiment 9 - Override Packaged Scripts From Persistent Data

The Lua loader logs the full path of every module it loads. This makes the
active script source visible in both the Editor Console and Player log.

### Establish The Packaged Version

1. Keep `Config/ScriptVersion.lua.txt` as `return "package-1"`.
2. Select `Protect Tree > Build Windows Player`.
3. Select `Protect Tree > Lua > Clear Windows Update Scripts`.
4. Run the Windows Player.
5. Confirm that the log prints `Script version: package-1` and loader paths
   under `StreamingAssets/ProtectTreeLua`.

### Deploy An Updated Version

1. Change `Config/ScriptVersion.lua.txt` to `return "update-1"`.
2. Select `Protect Tree > Lua > Deploy Scripts To Windows Update Folder`.
3. Run the same previously built Windows Player without rebuilding it.
4. Confirm that the log prints `Script version: update-1` and loader paths
   under `persistentDataPath/ProtectTreeLua`.

### Roll Back To The Packaged Version

1. Select `Protect Tree > Lua > Clear Windows Update Scripts`.
2. Run the same Player again.
3. Confirm that it falls back to `Script version: package-1`.

This simulates script deployment and rollback locally. A production updater
will add downloading, hashes, signatures, manifests, atomic replacement, and
version compatibility checks.

## Experiment 10 - Android Packaged Script Preparation

Android cannot read files inside an APK through normal `System.IO` paths. The
Android build tool therefore stages the initial Lua scripts as Unity Resources
before building and removes the generated resource folder afterward.

At runtime, Android uses this priority:

1. `Application.persistentDataPath/ProtectTreeLua`
2. Packaged Unity Resources

The Android build is configured for ARM64 and IL2CPP. The installed XLua
ARM64 native library includes Android 16 KB page-size support.

### Validate Script Packaging Without Android Build Support

Select:

```text
Protect Tree > Lua > Validate Android Packaged Scripts
```

Expected Console output includes:

```text
[ProtectTree][Android] Validated packaged Lua resource
[ProtectTree][Android] Cleared staged Lua resources
```

### Build And Test On A Device

1. Install Android Build Support, Android SDK & NDK Tools, and OpenJDK for the
   project's Unity Editor version.
2. Select `Protect Tree > Build Android Player`.
3. Install `Builds/Android/ProtectTree.apk` on an ARM64 Android device.
4. Use `adb logcat` and confirm the normal XLua startup and script-version
   messages appear.

Validated APK characteristics:

- ARM64 ABI only
- IL2CPP scripting backend
- `libxlua.so` included with `0x10000` ELF LOAD alignment
- `libil2cpp.so` included with `0x4000` ELF LOAD alignment
- APK signing and zip alignment verified

The current development application identifier is
`com.DefaultCompany.ProtectTree`. Replace `DefaultCompany` before publishing.

### Emulator Startup Result

The ARM64 IL2CPP APK was installed and started in a MuMu Android 12 emulator.
ADB logs confirmed:

- Unity started with IL2CPP and the ARM64-v8a APK.
- XLua loaded `Bootstrap.Main`, `Bootstrap.LifecycleDemo`, and
  `Config.ScriptVersion` from packaged Resources.
- Lua-to-C#, typed C#-to-Lua delegate calls, and lifecycle updates completed.
- No Unity or AndroidRuntime fatal startup error occurred.
