# Current Project State

Updated: 2026-06-25.

This file is the fast context refresh for long-running work. Read it first,
then open the referenced detailed documents before editing code.

## Active Direction

The project is a Unity demo for a networked auto-chess plus tower-defense game.
The first target is Windows. Android is planned later, initially as a client.

Current development priority is the original demo spine:

- stable LAN host/client flow;
- authoritative multiplayer gameplay;
- joint defense and observation clarity;
- shared Boss battle;
- core loop correctness.

Avoid drifting into content volume, UI polish, sound, or visual effects unless
the current task explicitly asks for them or they unblock the core flow.

## Authority Model

- Lua owns gameplay authority: phases, waves, shop, gold, pieces, deployment,
  combat, leaks, joint defense, synergies, and Boss rules.
- Unity/C# owns stable runtime services: scene lifecycle, input adapters,
  presentation, audio, networking, protocol serialization, build tools, and
  diagnostics.
- Single player uses the same Lua authority through a local runtime.
- LAN Host owns the authoritative Lua match. LAN Clients submit player intent
  and render recipient-scoped Host snapshots.
- LAN Clients still start scene-local Lua for static config/board access, but
  their local simulation is paused after scene binding. Client gameplay state
  must come from Host snapshots.
- Ordinary attack animation and damage timing are split by authority events:
  `PieceAttackStarted` / `EnemyAttackStarted` start the visual attack, while
  `EnemyDamageRequested` / `PieceDamageRequested` are emitted later at the
  configured impact delay and remain the only ordinary-hit damage requests.

Detailed reference: `Docs/ARCHITECTURE.md`.

## Key Runtime Entry Points

Lua:

- `Assets/Game/Lua/Bootstrap/Main.lua.txt` - public Lua entry module.
- `Assets/Game/Lua/Match/Session.lua.txt` - match coordinator and event router.
- `Assets/Game/Lua/Match/Flow.lua.txt` - phase state machine and timers.
- `Assets/Game/Lua/Match/WaveSpawner.lua.txt` - wave preset selection and spawn
  request timing.
- `Assets/Game/Lua/Match/EnemyRoster.lua.txt` - enemy state, movement, Boss
  transfer, damage, and endpoint handling.
- `Assets/Game/Lua/Match/PieceRoster.lua.txt` - piece ownership, bench, board,
  merge, health, traits, and synergy layers.
- `Assets/Game/Lua/Match/LeakResolver.lua.txt` - personal leak recording,
  joint-defense rescue, and final leak settlement.
- `Assets/Game/Lua/Match/BoardValidator.lua.txt` - board and route validation
  before session start.

C# runtime:

- `Assets/Game/Runtime/Lua/LuaBootstrap.cs` and `LuaRuntime.cs` - XLua startup,
  fixed ticking, snapshot bridge, and command bridge.
- `Assets/Game/Runtime/Presentation/MatchSceneController.cs` and
  `MatchSceneContext.cs` - per-frame snapshot collection and UI/view context.
- `Assets/Game/Runtime/Network/LanMatchRuntime.cs` - cross-scene LAN match
  identity, Host/Client snapshot flow, and command submission.
- `Assets/Game/UI/LanLobby/UILanLobby.cs` - visible LAN create/join/ready/start
  scene adapter.
- `Assets/Game/UI/TopUIPanel/RoundInfo.cs`,
  `Assets/Game/UI/PlayerInfos/UIPlayerInfos.cs`, `Assets/Game/UI/Shop/UIShop.cs`,
  and `Assets/Game/UI/UIPieceInspectPanel.cs` - current formal match UI.

## Current Gameplay Configuration

Timing is in `Assets/Game/Lua/Config/MatchFlow.lua.txt`:

- preparation: 90 seconds;
- normal battle defense timer: 90 seconds;
- joint-defense intro/freeze: 1.5 seconds;
- joint-defense timer: 90 seconds;
- Boss preparation: 90 seconds;
- Boss battle defense timer: 300 seconds;
- Boss transfer intro/freeze: 0.75 seconds;
- settlement: 2 seconds.

Waves are in `Assets/Game/Lua/Config/Waves.lua.txt`:

- `schedule.normal_wave_count = 6`;
- `schedule.boss_wave = 7`;
- normal waves draw from `wave_pools[1]` through `wave_pools[6]`;
- wave 7 currently uses `Boss_Goat`.

Important: flow wave counts and wave content are both owned by `Waves.lua.txt`.
Do not reintroduce a separate Boss-wave count in `MatchFlow.lua.txt`.

Player economy defaults are in `Assets/Game/Lua/Config/Players.lua.txt`:

- first-wave settlement reward is `round_reward`;
- each later wave adds `round_reward_per_wave_increment` more gold. With the
  current default, wave 1/2/3 settlement rewards are 5/6/7 gold.

Designer-facing editing guide: `Docs/GAME_DATA_GUIDE.md`.

## Board Rules

Gameplay board source of truth is `Assets/Game/Lua/Config/Board.lua.txt`.
`Assets/Game/Board/Prototype` and `DefaultSizeMap_11x7.asset` are visual or
prototype references, not runtime gameplay authority.

Current board:

- size: 11 x 7;
- y = 0 is the normal reserve row with 11 slots;
- y = 1 is the temporary reserve row with 11 slots;
- spawn cells: `(10, 2)` and `(10, 5)`;
- endpoint cell: `(2, 2)`;
- route 1 and route 2 are normal enemy routes and must start at Spawn and end
  at Endpoint;
- route 3 and route 4 are Boss route segments declared by `board.boss_routes`.

Critical distinction:

- normal enemy routes are full `Spawn -> Endpoint` routes;
- Boss routes are route segments. They are valid even when they do not end at
  the normal Endpoint. `BoardValidator` must keep this exception.

Board visuals are edited through `Assets/Resources/Board/DefaultBoardVisual.asset`.
Visual material overrides must never change Lua terrain, zones, routes, or
deployment rules.

Detailed reference: `Docs/ARCHITECTURE.md`, `Docs/BOSS_WAVE_RULES.md`.

## Boss Battle Rules

Current Boss wave: `Boss_Goat`.

- Sheep and Goat are Boss-wave minions.
- `Mysterious_Goat` is the single shared Boss.
- Boss-wave minions spawn independently for each alive player.
- The Boss spawns once, has one shared health pool, and visits one player's
  board at a time through `target_player_id`.
- Boss-wave minion leaks do not damage player health; they respawn for the same
  player and route to maintain pressure.
- When the Boss appears, Boss-wave minions become enraged: movement x2,
  attack speed x2, presentation Animator speed x2, and prefab `Eyes` shown.
- The Boss does not cause defeat by reaching an endpoint. Boss battle failure
  is timer-based.
- Boss transfer happens after its route segment loop or when all deployed board
  pieces on the current target board are downed. Bench/reserve pieces do not
  count for this condition.
- Boss transfer first publishes `BossTransferRequested`, waits
  `boss_transfer_intro_seconds`, then retargets and publishes `BossRetargeted`.
- Boss defeat ends the match in Victory.
- During `BossBattle`, `RoundInfo` keeps only the `RoundTime` timer visible;
  Boss health is shown by the separate Boss UI.

Detailed reference: `Docs/BOSS_WAVE_RULES.md`, `Docs/BOSS_SKILLS.md`.

## Deferred Item System Notes

Two consumable shop items are planned but not yet implemented. They should wait
until the current higher-priority gameplay issues are handled.

Shared item rules:

- Items refresh in the reserved equipment/shop item slots, not in the normal
  chess-piece offer slots.
- Items are purchased with gold.
- After purchase, items are placed into the owner's normal reserve row and
  occupy one reserve slot, like pieces do.
- Items are dragged onto a target piece to use and are consumed on successful
  use.
- Items are gameplay authority data, not UI-only state. LAN Clients must send
  use intent; Host Lua must validate and apply the result.

Planned items:

- `Beacon`: after dragging the item onto one of the owner's pieces, the shop
  area temporarily shows other players' avatars. Clicking a target player sends
  the chosen piece to that player's reserve row. In single player, the shop
  should not refresh this item.
- `Copy`: after dragging the item onto one of the owner's pieces, the owner
  gains a new level-1 copy of that piece. The result should be equivalent to
  buying a fresh level-1 piece of the same `piece_id`.

Recommended implementation order:

1. Add an item config and authoritative item roster/snapshot, while reusing
   reserve-cell presentation where possible.
2. Add item shop refresh/purchase for equipment slots.
3. Implement `Copy` first because it can call the existing level-1 piece grant
   path.
4. Implement `Beacon` second because it needs target-player selection,
   cross-player ownership transfer, reserve capacity validation, and LAN
   command/snapshot support.

## LAN State

Current LAN scope:

- direct-IP TCP LAN lobby and match flow;
- up to four-player-capable protocol, usually validated with two players;
- Host assigns player IDs and starts the match;
- player commands go Client -> Host -> Lua authority;
- Host sends recipient-scoped snapshots back to Clients;
- Clients render Host snapshots and do not author gameplay state;
- short reconnect is currently process-local and token-based. Returning to menu
  or closing the process is treated as leaving the session, not reconnect.

Current protocol version is `NetworkProtocol.CurrentVersion` in
`Assets/Game/Core/Network/NetworkProtocol.cs`. When snapshot/envelope fields
change, update protocol version and codec order together.

Detailed reference: `Docs/M4_LAN_MULTIPLAYER.md`.

## Build And Script Loading Notes

Editor loads Lua from `Assets/Game/Lua`.

Windows Player loads Lua from:

1. `Application.persistentDataPath/ProtectTreeLua`;
2. `Application.streamingAssetsPath/ProtectTreeLua`.

After changing Lua, a Windows packaged client needs either a rebuild or a
script deployment into the update folder. Otherwise the Player can still run
old Lua from `StreamingAssets`, which is a common source of "Editor fixed,
build still broken" confusion.

Windows Player has a development log overlay. Press `F12` to view recent logs
and the Player.log folder.

Detailed reference: `Docs/XLUA_STARTUP_EXPERIMENT.md`.

## Current Validation Agreement

The project owner performs Unity hands-on validation. Codex should not run
Unity Test Runner unless explicitly asked. For code tasks, Codex may run:

- Lua syntax checks with `lua`;
- targeted Lua config validation, such as `BoardValidator.validate(board)`;
- `dotnet build .\Assembly-CSharp.csproj --no-restore`;
- log inspection and code-level review.

When returning work, include exact owner validation steps when Unity behavior
needs confirmation.

## Common Traps

- Do not treat C# presentation state as gameplay authority.
- Do not let UI drain Lua producer queues. `Session` owns routing and public
  event publishing.
- Do not put `target_player_id` into wave config. Session assigns spawned
  enemies to players.
- Do not make every route satisfy `Spawn -> Endpoint`; Boss route segments are
  exceptions declared by `board.boss_routes`.
- Do not rebuild static board terrain when switching observed players. Reuse
  the personal-board view and change dynamic pieces/enemies/effects.
- Do not show or synchronize another player's private shop to a Client.
- Do not use local Client Lua simulation as LAN gameplay truth.
- Do not assume Windows Player is using the latest Lua file from `Assets`.
- Do not drive ordinary attack animations from damage request events. Use
  `PieceAttackStarted` and `EnemyAttackStarted`; damage requests are impact
  timing events.
- Do not revert dirty worktree changes unless the owner explicitly requests it.

## Task Start Checklist

Before implementing:

1. Read this file.
2. Open the one or two detailed docs related to the task.
3. Inspect the current code entry points before editing.
4. State the intended approach briefly.
5. Update docs if the task changes rules, boundaries, workflow, or validation
   status.

After implementing:

1. Run lightweight validation appropriate to the touched files.
2. Give exact Unity validation steps to the owner.
3. Mention any manual scene/prefab/resource bindings still required.
