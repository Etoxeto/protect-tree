# Lua Gameplay Modules

This folder contains hot-updateable gameplay modules loaded by XLua.

During milestone M1, `FileSystemLuaLoader` loads modules directly from this
folder in the Unity Editor. The Windows build tool copies these scripts into
the Player's `StreamingAssets/ProtectTreeLua` folder. A built Player first
checks `persistentDataPath/ProtectTreeLua` for updated scripts, then falls back
to the packaged scripts. Android builds temporarily stage the initial scripts
as Unity Resources so they can be loaded synchronously from inside the APK.

Name Lua files with the `.lua.txt` suffix so Unity imports them as text assets.
For example, `require("Bootstrap.Main")` resolves to
`Bootstrap/Main.lua.txt`.

`Config/ScriptVersion.lua.txt` is a learning-time version marker. Change it
before deploying scripts to the Windows update folder to confirm which script
set the Player loaded.

Keep Lua gameplay independent from scene GameObjects. Access stable C# services
through narrow adapters, and return gameplay state or events for Unity views to
display.

Planned module groups:

- `Bootstrap`: Lua entry point and module lifecycle
- `Match`: authoritative match phases and settlement
- `Economy`: shop, currency, purchase, refresh, and leveling
- `Board`: bench, fixed-grid deployment, merge, and synergies
- `Combat`: waves, targeting, damage, joint defense, and shared boss rules
- `Config`: hot-updateable gameplay configuration
