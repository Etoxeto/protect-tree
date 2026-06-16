# Protect Tree

A Unity 2D cooperative auto-chess and tower-defense game demo.

## Current Targets

- Unity: 2022.3.57f1c2
- First client platform: Windows
- Later client platform: Android
- Final player count: up to four players
- First multiplayer milestone: two-player LAN host/client
- Hot update: XLua for gameplay scripts, Addressables for assets
- XLua foundation: `2.1.16_android_16kb`, embedded Lua 5.3.5

## Repository Layout

- `Assets/Game/Core`: pure C# gameplay primitives and contracts; no UnityEngine dependency.
- `Assets/Game/Runtime`: stable Unity-facing runtime and composition code.
- `Assets/Game/Runtime/Board`: reusable pseudo-3D board presentation components.
- `Assets/Game/Board/Prototype`: isolated visual board prototype and test assets.
- `Assets/Game/Lua`: hot-updateable gameplay modules, added during the XLua phase.
- `Assets/Game/Tests`: automated gameplay and integration tests.
- `Docs`: project decisions, architecture, and milestone definitions.

See [Docs/PROJECT_PLAN.md](Docs/PROJECT_PLAN.md) and
[Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md) before adding gameplay systems.
The first hands-on XLua exercise is documented in
[Docs/XLUA_STARTUP_EXPERIMENT.md](Docs/XLUA_STARTUP_EXPERIMENT.md).
