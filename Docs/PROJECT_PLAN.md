# Project Plan

## Demo Scope

The first complete demo targets:

- Two-player validation with data structures that support four players
- Five normal waves and one shared boss wave
- Three chess pieces, three enemies, one boss, and two synergies
- Fixed deployment cells
- Local single player and LAN host/client
- Placeholder art until the full gameplay loop is stable

## Milestones

### M0 - Project Foundation

Status: Complete on 2026-06-09.

Deliverables:

- Git repository and Unity-aware ignore rules
- Visible Meta Files and Force Text serialization
- Initial assembly boundaries and project documentation
- Unity batch import without compile errors

Learning goals:

- Understand Unity project version control and assembly dependencies
- Practice defining a minimum playable scope

### M1 - XLua Minimum Experiment

Status: Complete on 2026-06-10.

Validation completed:

- Editor startup, lifecycle, typed delegates, and generated wrappers
- Development-time module replacement
- Windows packaged scripts, persistent-data override, and rollback
- Android ARM64/IL2CPP APK build, signing, and native-library alignment
- Android packaged Resources and XLua startup in a MuMu Android emulator

A physical Android-device smoke test remains useful before release, but it is
not required to begin the single-player vertical slice.

Deliverables:

- One Lua environment with loader, tick, and disposal
- C# to Lua and Lua to C# calls
- Generated XLua bindings
- Replace a Lua module without rebuilding the client
- Early Windows and Android ARM64/IL2CPP smoke tests

Learning goals:

- Understand XLua binding, module, and lifecycle fundamentals
- Distinguish Lua script update, C# hotfix, and asset update

### M2 - Single-player Vertical Slice

Status: In progress. The fixed-step authoritative simulation clock was added
on 2026-06-10. The Lua match phase state machine, C# snapshot bridge, and
three-wave integration test are complete. Phase transition events and their
batch bridge, deterministic wave spawn planner, and `Match.Session`
coordinator are complete. The independent authoritative enemy roster is
integrated with Session and C# enemy snapshots. Deterministic enemy path
progress and its fixed-tick Session update order are complete. Battle now
completes only after spawning finishes and no enemies remain alive. An
independent authoritative player-health roster is complete. Its damage input
now consumes final leak-settlement results, and the independent per-wave leak
resolver is complete. Target-player propagation and single-player leak
settlement are integrated through `Match.Session`, including preservation of
enemy target-player ownership in the C# snapshot bridge.
The independent authoritative enemy-damage and defeat contract is implemented
and has passed owner-run validation. The independent fixed-grid piece roster
is implemented and has passed static review. The next task adds a deterministic
piece attack planner before integrating the first playable combat chain and
starting Unity presentation work. The planner, Session combat integration,
stable C# piece command bridge, and piece snapshot bridge are now implemented
and awaiting owner-run validation. The first Unity presentation layer is now
implemented with placeholder graphics, fixed-cell deployment input, enemy
movement, health bars, hit feedback, phase display, and player-health display.
Owner-run Unity validation passed. Ground, high-ground, and obstacle terrain,
deterministic blocking, melee enemy counterattacks, piece health, downed state,
timed recovery, and battle-end recovery are implemented and passed owner-run
validation. Grid-relative attack ranges, four-direction facing,
Preparation-only deployment changes, a deterministic starter shop, purchases,
refresh costs, player gold, and round rewards are implemented and passed
owner-run validation. The M3 foundation now includes per-player bench
capacity, deployment population limits, preparation-only return-to-bench
commands, and atomic full-bench purchase rejection. These rules passed
owner-run validation. Three deterministic shop levels, upgrade costs, and
shop-level deployment-limit growth are implemented and passed owner-run
validation. Preparation-only piece selling, capacity release, configured sale
value, and sale gold rewards are implemented and passed owner-run validation.
Data-driven three-copy piece merging, stable surviving instance IDs, cascading
upgrades, upgraded combat values, and merge-aware capacity release are
implemented and passed owner-run validation. Data-driven shop rarity weights,
deterministic rarity selection, placeholder rarity pools, and rarity-aware shop
snapshots are implemented and passed owner-run validation. Two data-driven
synergies, unique-board-piece counting, authoritative damage bonuses, and
synergy snapshots are implemented and passed owner-run validation. Three
distinct melee enemy archetypes and differentiated normal-wave content are
implemented and passed owner-run validation. The single-player authority now
supports five normal waves, a Boss preparation and battle finish, explicit
Victory/Defeat results, Boss endpoint defeat, and immediate defeat when all
players are eliminated. This Boss-flow foundation passed owner-run validation.
Authoritative player-ready state and all-surviving-player early
preparation completion are implemented and passed owner-run validation. The
temporary Unity presentation is now explicitly composed from a scene
controller, isolated demo setup, keyboard input adapter, and replaceable
placeholder view; this presentation-boundary migration passed owner-run
validation. Temporary diagnostic controls and displays now expose the complete
implemented single-player loop, including shop upgrades, piece selection,
benching, selling, capacity, synergies, Boss distinction, and match result;
this diagnostic-loop completion passed owner-run validation. The pseudo-3D
perspective-board technical prototype has now been separated into reusable
`ProtectTree.Runtime.Board` presentation components and an isolated prototype
scene. It provides read-only visual layout data, projection, sorting, static
terrain rendering, collider-free cell picking, highlights, and a single visible
observed-board container. The Lua authority now publishes a deterministically
ordered, copied board snapshot containing logical coordinates, presentation
coordinates, terrain height, visual keys, and route samples. Pure C# board
contracts, `LuaRuntime` bridging, and once-per-runtime `MatchSceneContext`
caching are implemented. The formal match scene now converts that snapshot
into one reusable pseudo-3D personal-board view, builds static terrain once per
runtime, and provides collider-free cell picking and highlights. The temporary
four-cell authority map has now been replaced by the complete 11x7 default
personal-defense map derived from
`DefaultSizeMap_11x7.asset`. It defines battlefield, reserve, temporary-reserve,
spawn, and endpoint zones; eleven normal reserve slots; eleven temporary
reserve slots; two validated fixed enemy routes; and shared-route cell
positions used by blocking. Temporary-reserve overflow grants, player
resolution, and battle-start automatic sale are implemented. The formal match
view now visibly
distinguishes Reserve, TemporaryReserve, Spawn, and Endpoint cells and draws
both authority routes, including separated lines on their shared section.
Formal pseudo-3D board presentation has replaced the old placeholder arena for
current board, route, piece, and enemy display. Board visuals are edited through
`DefaultBoardVisual.asset`, including per-cell material overrides and front-edge
depth presentation. Formal UI components under `Assets/Game/UI` now observe
`MatchSceneContext`: shop, round information, player info, synergy bar, and
piece inspection are connected to authoritative snapshots. Mouse placement uses
a click-vs-drag threshold, hides the inspection panel during drag/facing
confirmation, auto-deselects after completed placement, and stops interaction
after match end. Camera framing is presentation-only: it avoids the piece
inspection panel only when that panel should be visible, uses a battle/Boss
battle zoom, and returns to the initial camera state outside those temporary
states. The shop hides during combat and after match end. The scene-side
starting-piece demo setup has now been removed, keyboard/text diagnostics
default to disabled, and match-end `UIMessageBox` result-popup flow has passed
owner-run validation.

Deliverables:

- Preparation, battle, settlement, Boss preparation/battle, and end phases
- Three fixed-grid pieces, three enemies, one boss, one path, and five normal waves
- Deterministic gameplay state separated from Unity views
- Pseudo-3D personal-board presentation connected to authoritative board snapshots

### M3 - Auto-chess Core Loop

Deliverables:

- Shop refresh and level, bench, sell, merge, synergies, and population cap
- Data-driven pieces, enemies, waves, and Addressables content
- Complete small single-player demo

### M4 - LAN Multiplayer

Status: In progress. A framework-independent, four-player-capable protocol
foundation now defines player-intent commands, host identity and command-order
checks, lobby snapshots, complete authoritative match snapshots, and client
snapshot-order checks. A local two-player authority prototype now keeps the
default scene single-player while allowing tests/debug entry points to start
players 1 and 2, verify private shops, require all active players to ready, and
spawn independent normal-wave enemies per alive player. No network transport,
joint-defense flow, or multiplayer shared-Boss arena has been connected yet.

Deliverables:

- Authoritative lobby and match state
- Join, ready, shop, deployment, battle, and settlement synchronization
- Two-device LAN play; four-player-capable protocol

### M5 - Joint Defense and Spectating

Deliverables:

- Remaining-enemy snapshots and joint-defense queues
- Elimination, spectator permissions, arena switching, and full snapshots
- One visible personal-board view whose static terrain is reused while
  `observedPlayerId` changes
- Authority-produced timestamped combat playback records and periodic keyframes
- Observation switching that restores and catches up presentation without
  resimulating or guessing combat results on the client

Scope fallback:

- If formal observation threatens the first multiplayer schedule, normal-round
  observation may remain disabled while retaining the same architecture.
- Joint-defense observation remains automatic because it is required to explain
  the shared defense result.

### M6 - Shared Boss and Internet Play

Deliverables:

- One shared boss arena for surviving players
- Host plus Relay connection flow
- Dedicated Server build exercise
- Script, content, and protocol versioning

## Board Rendering Roadmap

The agreed rendering direction is:

1. Use one pseudo-3D personal-board presentation because every player's normal
   defense terrain is identical.
2. Keep every player's logical board and combat state independent in Lua/server
   authority.
3. Render only the currently observed player's pieces, enemies, animations, and
   effects; switching players does not rebuild static terrain.
4. Use a separate shared Boss board view for surviving players.
5. Profile the organized runtime implementation before adding Editor-time mesh
   baking or material batching.

The current `Assets/Game/Board/Prototype` content is a visual validation tool,
not a gameplay configuration source. Formal board data must flow from Lua
authority through C# snapshots into `Assets/Game/Runtime/Board`.

## Working Agreement

As of 2026-06-12, Codex directly implements both gameplay and framework work
to accelerate the demo. Important gameplay and authority-boundary logic should
contain concise Chinese comments. The project owner performs Unity compilation,
EditMode tests, and hands-on play validation, while Codex performs code-level
inspection and provides exact validation steps.

As of 2026-06-16, every Codex implementation task must first consult the
relevant `Docs/` files for current project state and constraints. When a task
changes gameplay rules, architecture boundaries, scene/UI workflow, validation
status, or agreed development direction, Codex must update the matching
documentation in the same task.

## Pending Decisions

- Company name and final application identifier
- Exact networking service used for Relay/session discovery
- Final content names, values, and art direction
