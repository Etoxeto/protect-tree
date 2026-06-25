# Project Plan

## Demo Scope

The first complete demo targets:

- Two-player validation with data structures that support four players
- Configurable normal waves, currently six normal waves and one shared boss wave
- Three chess pieces, three enemies, one boss, and two synergies
- Fixed deployment cells
- Local single player and LAN host/client
- Placeholder art until the full gameplay loop is stable

Planner-facing game data editing is documented in `Docs/GAME_DATA_GUIDE.md`.
Use that guide when adding or adjusting pieces, enemies, synergies, waves,
shop data, player numbers, flow timing, and Resources catalog entries.

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
supports configurable normal waves, a Boss preparation and battle finish, explicit
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
- Three fixed-grid pieces, three enemies, one boss, one path, and configurable normal waves
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
spawn independent normal-wave enemies per alive player. The local authority now
also includes joint-defense transfer/rescue and a board-hopping shared Boss.
`LuaMatchCommandRouter` connects accepted host-side protocol commands to the
existing Lua authority calls and reports Lua authority rejection as
`GameplayRejected`, while `LuaMatchSnapshotFactory` creates recipient-scoped
host snapshots from the same authority. `LuaLoopbackMatchHost` now composes
command routing, authority ticking, and snapshot creation into one
transport-free host pipeline. `MatchSnapshotReceiver` validates and caches
recipient-scoped snapshots on the client side. `MatchCommandEnvelopeFactory`
creates per-player sequenced command envelopes for the future client send path.
`LoopbackMatchClient` closes the no-socket development loop between a local
client endpoint and `LuaLoopbackMatchHost`. `IMatchHostTransport` and
`IMatchClientTransport` now define the byte-level boundary for a future LAN
package adapter, and `IMatchProtocolCodec` defines the protocol envelope to
byte payload boundary. `BinaryMatchProtocolCodec` now serializes player command
envelopes, recipient-scoped server snapshot envelopes, and lobby snapshots.
`LoopbackMatchByteHost` connects that codec to the local loopback path so
commands and snapshots can travel as `byte[]` before returning to envelope form.
`EncodedLoopbackDebugInput` provides an opt-in Play Mode validation hook for
that encoded path. `TcpMatchHostTransport` and `TcpMatchClientTransport` now
provide the first direct-IP LAN byte transport using length-framed TCP payloads.
`LobbyHostService` and `LobbyClientService` now provide the first room service:
Host assigns player IDs, clients receive assignments, ready/name commands travel
over the codec, and shared lobby snapshots are broadcast. `LanLobby` now exposes
the first visible create/join/ready room flow. `MatchStartEnvelope` now connects
the lobby Start button to a synchronized `SampleScene` transition: Host and
clients enter the match with the same player count and each client's assigned
local player ID; this flow has passed owner-run validation. `LanMatchRuntime`
now preserves the LAN match identity across the transition into `SampleScene`
and exposes it through `MatchSceneContext`; this identity handoff has passed
owner-run validation. The first in-match LAN loop is now implemented for Ready,
basic shop operations, and formal piece placement/sale: Client sends encoded
player commands to Host, Host routes them through Lua authority, sends
recipient-scoped snapshots back, and Client uses those Host snapshots when
available. Host now also broadcasts recipient-scoped snapshots at a low fixed
rate to bound in-match clients, giving the first LAN battle playback path for
enemy movement and phase changes. Clients now automatically send a no-op
`RequestSnapshot` command after connecting so Host can bind the in-match
connection and return an initial authoritative snapshot before any gameplay
click. Enemy presentation now smooths small snapshot position changes on the
Client without predicting gameplay, and accepted-snapshot logs are throttled for
readability. In-match identity binding now uses an explicit `MatchJoin` message
before gameplay commands, replacing the earlier first-command binding shortcut;
`MatchJoin` now validates a Host-authored token delivered during match start,
and Host preserves that token table across scene LuaRuntime binding and match
transport rebuilds. The LAN Demo now also supports short reconnect for the same
running Client process: after Host observes the old match connection disconnect,
the Client may reconnect with the same player ID and token and resume receiving
authoritative snapshots. The first formal observation entry is implemented:
player info entries can switch the currently observed active player, with `F1`
to `F4` as a keyboard fallback, while board interaction remains restricted to
the local player's board. Authoritative match snapshots now carry public match
events: protocol version `8` covered joint-defense start, rescue, leak
resolution, and final damage, and protocol version `9` extends the same path to
shared Boss creation, target-board retargeting, damage, endpoint/defeat result,
and final match result logs. Owner-provided Windows player logs have
validated the normal `MatchJoin`/token path, scene-bind token preservation,
periodic snapshot playback, and Client command round trips. Owner-run
observation switching, LAN joint-defense event validation, LAN shared Boss
event validation, and controlled short reconnect validation have also passed.
LAN Client local Lua simulation is now paused after scene entry, removing
misleading non-authoritative phase logs; owner-provided Host and Client logs
confirmed that this pause does not break snapshot-driven joint-defense, Boss,
or match-end playback. A small LAN lifecycle feedback pass adds minimal
connection/reconnect/failure labels and popups for owner-run validation, but
this is a supporting technical slice rather than the player-facing multiplayer
flow UI.
Bandwidth profiling, broader effect playback, saved-token process resume,
reconnect token lifetime rules, and richer spectator playback are still later
multiplayer work.

Deliverables:

- Authoritative lobby and match state
- Join, ready, shop, deployment, battle, and settlement synchronization
- Two-device LAN play; four-player-capable protocol

Current LAN stabilization stage goals:

1. Confirm LAN Client local Lua simulation is paused while Host snapshots still
   drive gameplay. Status: passed through owner-provided dual-client logs.
2. Add a controlled short reconnect validation path. Status: passed through
   owner-provided dual-client logs using the `Ctrl+F5` Client debug disconnect.
   Returning to menu and closing the process remain intentional session exit
   cases, not reconnect.
3. Make player-facing multiplayer flow readable. Status: pending dedicated UI.
   The UI should explain waiting for other players, ready state, whose board is
   being watched, personal defense, joint defense, rescued/final leaks, shared
   Boss target, and match-end cause. It should not expose technical connection
   states unless the player must act.
4. Clarify and validate Host/Client cleanup behavior for leaving a lobby,
   leaving a match, Host shutdown, and match-end return flow.
5. Exercise command, snapshot, public-event, and observation behavior around
   reconnect or temporary transport loss.
6. Prepare an Android Client LAN smoke-test path after the Windows two-client
   lifecycle is stable.
7. Only after LAN lifecycle stability, return to feedback/content work such as
   sound, damage/gold effects, balance, and additional pieces or enemies.

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

- One shared boss state that visits surviving players' personal boards
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
4. Reuse the personal-board view for Boss battle. The shared boss authority
   state retargets between surviving players' boards instead of requiring one
   oversized shared arena.
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

As of 2026-06-25, `Docs/CURRENT_STATE.md` is the required fast-start context
document for new tasks. It summarizes the current authority model, LAN state,
board/Boss rules, validation agreement, and common traps. Use it before reading
the longer milestone history.

Windows build clients now include a development log overlay for owner-run LAN
validation. Press `F12` in the packaged Windows player to view recent logs,
error stacks, and the `Player.log` folder path.
Windows clients also run in background while unfocused so multi-window LAN tests
continue dispatching network events.

## Pending Decisions

- Company name and final application identifier
- Exact networking service used for Relay/session discovery
- Final content names, values, and art direction
- Consumable item system implementation timing. Planned first items are
  `Beacon` and `Copy`; both refresh in equipment slots, are bought with gold,
  occupy reserve cells after purchase, and are dragged onto pieces to use.
  `Copy` should be implemented before `Beacon` because it can reuse level-1
  piece grant logic, while `Beacon` requires cross-player transfer and LAN
  validation. Detailed deferred rules are in `Docs/CURRENT_STATE.md`.
