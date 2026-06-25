# Architecture

## Core Rule

The server owns authoritative gameplay state. Clients submit player intent, and
the server validates and applies it before broadcasting state or events.

Single-player mode uses the same authoritative gameplay core through a local
command gateway. It is not a separate implementation.

The authority advances gameplay through a fixed `0.1` second simulation step.
Unity may render at a variable frame rate, but Lua gameplay rules consume only
fixed simulation time and must not read `UnityEngine.Time` directly.

## Layer Responsibilities

### C# Stable Layer

- Unity lifecycle, scene loading, input adapters, views, audio, and pooling
- XLua environment, loader, generated bindings, and version checks
- Network transport, connection lifecycle, message DTOs, and serialization
- Addressables and asset update flow
- Build tooling, logs, and platform integration

### Lua Gameplay Layer

- Match phase state machine
- Economy, shop, purchasing, refresh, and shop leveling
- Bench, fixed-grid deployment, merging, and synergies
- Combat rules, waves, settlement, joint defense, and boss rules
- Gameplay configuration and UI controllers

Lua gameplay state must not depend directly on scene GameObjects. Unity views
display state and events produced by the gameplay layer.

## Scene Presentation Composition

`LuaBootstrap` owns only Lua startup, ticking, reload experiments, and
disposal. It must not automatically add input or presentation components.

The match scene explicitly composes its Unity-facing behavior:

- `MatchSceneController` observes the current `LuaRuntime`, reads one
  authoritative snapshot set per rendered frame, and distributes it to child
  scene features.
- `LocalPlayerId` is the player controlled by this client. `ObservedPlayerId`
  is the personal board currently being displayed. When `ObservedPlayerId` is
  unset or zero, it follows `LocalPlayerId`.
- Input features translate Unity input into player-intent commands. They do
  not mutate snapshots or scene views directly.
- View features render snapshots. They do not decide gameplay results or
  consume authority-owned events.
- Formal UI features under `Assets/Game/UI` also observe `MatchSceneContext`.
  They may submit player-intent commands through `LuaRuntime`, but they must not
  become a second gameplay authority or drain Lua producer queues directly.
- Formal match entry no longer uses a scene-side demo setup component. Starting
  pieces must come from normal authority commands such as shop purchases or
  explicit scripted grants, not from presentation bootstrap code.

`MatchKeyboardInput` and `MatchDebugHud` are opt-in diagnostics. They default
to disabled in the formal match scene, display authoritative snapshots or submit
debug commands only when enabled manually, and create no board, unit, enemy,
camera, or gameplay authority objects.

## Board Presentation and Observation

The formal board uses a pseudo-3D perspective projection while gameplay remains
on a deterministic fixed grid:

- Lua authority owns cell IDs, terrain, routes, occupancy, deployment
  validation, combat state, and player ownership.
- C# presentation owns perspective projection, generated meshes, materials,
  visual sorting, picking, highlights, unit anchors, animation, and effects.
- `BoardVisualLayout` is read-only presentation input. In the formal match
  scene it must be converted from an authoritative board snapshot; prototype
  ScriptableObject maps must never become a second gameplay authority.
- `Match.BoardSnapshot` copies the Lua board configuration into deterministically
  ordered cell and route arrays. `LuaRuntime.GetBoardSnapshot()` bridges that
  data into pure C# contracts, and `MatchSceneContext` caches it once per Lua
  runtime because a match board layout is static.
- `BoardVisualLayoutConverter` translates the authority snapshot into
  presentation-only cells. `MatchBoardPresenter` builds the static match-scene
  terrain and handles visual cell selection without deciding whether a
  deployment command is legal.
- Piece attack range preview is derived from the same Lua `Config.Pieces`
  `attack_range` offsets used by `PieceAttackPlanner`. C# may render those
  offsets as orange-red board highlights, but it must treat them as display
  data from the authority/config layer, not as a separate targeting rule.
- Synergy bar and synergy detail UI render from `SynergyProgressSnapshot` and
  the current piece roster. The first detail panel shows owned pieces for the
  selected synergy and tints bench pieces gray; it is not a complete static
  collection browser until Lua exposes a read-only piece config list to UI.
- Synergy activation level and synergy layer count are separate authoritative
  values. Activation is determined by the board roster, while layer count is
  accumulated by Lua traits such as grant, kill, perfect-defense, and
  gold-spend triggers. C# and UI may display or serialize `LayerCount`, but
  they must not derive layer effects locally.
- `UIPieceInspectPanel` may render either a deployed/reserve `PieceSnapshot` or
  a pending `ShopOfferSnapshot`. Lua config owns the player-facing
  `feature_description` text and level-one shop preview stats; UI only displays
  those snapshot values. Shop preview uses the raw offer snapshot before
  purchase, while selected battlefield pieces use the current piece snapshot
  after synergy, trait, health, level, and battle-state changes.
- Shop item clicks use a two-step confirmation flow. The first click selects a
  shop slot, shows that item in `UIPieceInspectPanel`, and enables the item
  confirm effect. A second click on the same slot submits purchase intent.
  Clicking outside that shop item cancels the pending purchase. Inspecting a
  visible shop item is separate from purchasing it: the item can still open the
  raw offer preview when the player lacks gold or bench space, but the second
  click only purchases if the latest authority snapshot says the offer is
  currently affordable and usable. Shop offer preview is UI-driven and is not
  blocked by the board piece press/drag/facing-confirmation inspection guard;
  that guard only suppresses deployed/reserve piece inspection. `UIShop` also
  calls `UIPieceInspectPanel.PreviewShopOffer(...)` on the first click, so the
  preview appears immediately in the same frame instead of depending only on
  the next `MatchSceneContext` refresh.
- Unit prefab and projectile presentation are Unity-side visual configuration,
  not Lua gameplay configuration. Lua IDs such as `piece_id`, `enemy_id`,
  `portrait`, and `class_id` stay in config tables; Unity maps unit IDs to
  prefabs through Resources catalogs such as `DefaultUnitVisualCatalog` and
  `DefaultProjectileCatalog`. `class_id` directly names the icon resource under
  `Resources/UI/Icons/CharacterType`, for example `class_id = "Magician"` loads
  `Magician.png`.
- Piece star/merge visuals are presentation-only. `BoardPieceLevelVfx` may be
  attached to individual piece prefabs for art tuning, but missing components
  are created at runtime with a generic glow. Lua remains authoritative for
  the actual merge result and piece `level`; C# only renders the current level
  and the `PiecesMerged` event.
- Projectile visuals are driven by authoritative attack events. They may play
  delayed arrows, spells, trails, or hit effects, but they must not decide
  damage, target validity, blocking, leaks, or death.
- Ordinary attack visuals start from `PieceAttackStarted` and
  `EnemyAttackStarted`. Actual ordinary-hit damage is requested later through
  `EnemyDamageRequested` and `PieceDamageRequested` after the configured impact
  delay, and the target is revalidated at that impact moment.
- `BoardRouteView` can draw route snapshots under the observed board's
  dedicated `Routes` root when `Show Route Debug Lines` is enabled. Formal
  gameplay hides these debug lines; this does not remove or change authority
  route data.
- `DefaultBoardVisual.asset` is the formal board's Unity-only visual editing
  entry. It owns global terrain materials, visual-key materials, highlights,
  and optional per-coordinate material overrides. A per-cell visual override
  never changes Lua terrain, zone, height, route, deployment, or combat rules.
- `MatchBoardPresenter > Projection > Front Edge Extra Depth` extends only the
  visible front faces of the `y = 0` row below the board. This presentation
  skirt does not move cell tops, units, highlights, or authority coordinates.
- Terrain and zone are separate contracts. Terrain describes combat properties
  such as Ground, HighGround, and Obstacle. Zone describes map purpose such as
  Battlefield, Reserve, TemporaryReserve, Spawn, and Endpoint. New terrain or
  zone types must declare their capabilities instead of relying on names.
- A cell may belong to multiple routes. It stores a route-specific progress for
  each route so pieces on a shared section can block enemies from either path.
- `Match.BoardValidator` checks full coordinate coverage, terrain and zone
  references, reserve capacities, traversability, adjacent route samples,
  and route progress before a session starts. Normal enemy routes must begin at
  a Spawn cell and finish at the Endpoint cell. Boss routes listed in
  `board.boss_routes` are route segments, so they may start or finish before
  the normal Spawn/Endpoint cells while still requiring valid traversable
  samples.
- Player intent always submits a cell ID. Picking a visible cell does not prove
  that deployment is legal.

Normal defense uses the same static terrain layout for every player. The client
therefore keeps one visible personal-board view and changes only its dynamic
piece, enemy, and effect content when `observedPlayerId` changes. Static board
meshes are not rebuilt when switching between players. Boss battle now follows
the same personal-board viewing model: there is one shared boss state, but the
boss visits one surviving player's board at a time instead of forcing all
players into one oversized shared arena.

Observation switching is presentation state. `LocalPlayerId` remains the only
player this client may control, while `ObservedPlayerId` decides which active
player's board is currently rendered. Formal match UI may request observation
changes through `MatchSceneContext.RequestObservePlayer(...)`; it must not
mutate snapshots or gameplay state directly.

All players' combat continues on the authority even when a client does not
render it. Formal spectating will use authority-produced, timestamped combat
events plus periodic keyframes. When observation changes, the presentation
restores the nearest keyframe, catches up to the current battle time, and then
continues normal playback. A client must never infer authoritative combat
results from elapsed time alone.

Static board mesh baking is an allowed later optimization. Baking means
precomputing reusable mesh and lookup data in the Editor instead of rebuilding
it during play. It must preserve unit occlusion, cell highlighting, and picking;
flattening the whole board into one image is not the default plan. Until
profiling proves it necessary, the organized runtime mesh implementation is
preferred over premature batching.

Camera movement in the match scene is presentation-only. It may temporarily
avoid UI obstruction, zoom during Battle/BossBattle, or restore the scene's
initial camera state, but it must not affect picking authority, board
coordinates, deployment legality, combat timing, or simulation state.

## Gameplay Event Ownership

An event queue has one drain owner because draining consumes its contents.
Gameplay systems, UI, and C# adapters must not independently drain the same
producer queue.

The match-session owner drains internal events once, forwards them to gameplay
systems, and republishes the public events needed by views, networking, and
tests.

## Assembly Boundaries

- `ProtectTree.Core`: pure C# contracts and gameplay primitives. It cannot
  reference UnityEngine.
- `ProtectTree.Runtime`: Unity-facing stable runtime. It may reference Core.
- `ProtectTree.Core.Tests`: EditMode tests for deterministic core behavior.

Add dedicated XLua, network, editor, and presentation assemblies only when
their dependencies are introduced.

## Multiplayer Topology

Connection methods are adapters around the same authoritative session:

1. Local session for single player.
2. LAN host/client for the first multiplayer milestone.
3. Host plus Relay for public internet play.
4. Dedicated Server as a later networking exercise.

All collections and messages must support four players from the beginning,
even when the first multiplayer test only uses two.

`Config.Players` defines up to four player configurations, but the active
player set is resolved through `Match.PlayerConfig`. The default active list
remains single-player for the formal scene. Local multiplayer tests or debug
entry points may start the same Lua Session with `{ 1, 2 }` before real LAN
transport exists.

## Network Protocol Boundary

Transport, protocol, and gameplay authority are separate responsibilities:

- Transport delivers serialized bytes and reports connection lifecycle.
- `ProtectTree.Core.Network` defines versioned envelopes, player-intent
  commands, lobby snapshots, complete match snapshots, and ordering gates.
- `ProtectTree.Runtime.Network` defines transport-facing runtime adapters and
  byte-level transport interfaces. It also defines protocol codec boundaries
  that turn protocol envelopes into transport bytes. A transport implementation
  must not call Lua directly or inspect gameplay DTO contents.
- The host maps each connection to one assigned player ID before accepting a
  claimed command identity.
- Each client snapshot contains public match state and only that recipient's
  private shop. The host must not broadcast every player's shop offers.
- Passing a protocol gate only proves identity, match, version, and ordering.
  Lua authority still validates every gameplay rule.
- Clients never send authoritative Gold, health, damage, ownership, results,
  or world positions.

Client command sequences increase independently per player. Server snapshot
sequences increase globally. Snapshot simulation Tick must never move
backward.

The first concrete codec is binary and currently covers player commands,
recipient-scoped server snapshots, lobby snapshots, lobby assignment/command
messages, and match-start transition messages. It is deterministic and easy to
inspect, but it is not yet a bandwidth-tuned production protocol; when snapshot
or envelope fields change, the protocol version and codec field order must be
updated together.

The encoded loopback path uses `LoopbackMatchByteHost` to simulate the future
host-side byte boundary: client code encodes a command envelope to `byte[]`, the
host adapter decodes it before invoking authority, then the host encodes a
recipient-scoped snapshot back to `byte[]` for the client to decode. This keeps
real transport integration replaceable; a future LAN adapter should provide
connection lifecycle and payload delivery, not inspect gameplay state.

`EncodedLoopbackDebugInput` is an opt-in scene diagnostic for manually driving
that byte path in Play Mode. It must remain a debug hook; formal multiplayer UI
should later talk to lobby and transport-facing services instead of depending
on this component.

The first real LAN adapter is TCP-based. `TcpMatchHostTransport` and
`TcpMatchClientTransport` move length-framed protocol payloads over direct
`IP:Port` connections. Their network work runs on background tasks, while
`Pump()` dispatches queued connect, disconnect, failure, and message events on
the Unity main thread. Gameplay code should therefore call transport `Pump()`
from an owning MonoBehaviour before reacting to transport events.

Lobby flow now has a service boundary above transport. `LobbyHostService` owns
Host-side room state, assigns connection-scoped player IDs, validates lobby
command identity and sequence, and broadcasts `LobbySnapshot`. `LobbyClientService`
receives `LobbyAssignment`, sends ready/display-name commands, and caches the
latest lobby snapshot. UI should call these services; UI should not directly
parse TCP payloads or assign player IDs.

`LanLobby` is the first scene-level UI adapter for that service boundary. It can
create a direct-IP room, join by `IP:Port`, render lobby players, toggle ready
state, and transition all room members into `SampleScene` after receiving the
Host-authored `MatchStart` message. That transition only establishes player
count and local player identity through `MatchStartupOptions`; formal in-match
LAN command submission and recipient-scoped snapshot playback remain separate
multiplayer work.

`LanMatchRuntime` is the cross-scene LAN match holder. It is created from the
lobby start message, survives the transition into `SampleScene`, and records
role, room ID, match ID, player count, local player ID, Host address, and match
port. It now also owns the first in-match command/snapshot channel: Host listens
on the match port, Client sends encoded player commands, Host routes accepted
commands through Lua authority, and Client caches recipient-scoped Host
snapshots. Presentation code may read the runtime through `MatchSceneContext`;
gameplay authority still remains on Host Lua. Client-side match UI should submit
player intent through `LanMatchRuntime.TrySendCommand(...)` and wait for Host
snapshots instead of mutating local Lua state. The current formal UI coverage is
Ready, shop operations, piece placement, and sell/drop. Host also broadcasts
recipient-scoped snapshots at a low fixed rate to already-bound match clients,
which gives the first battle playback path without asking Clients to resimulate
combat. A LAN Client still starts its scene-local Lua runtime for static
configuration and board snapshot access, but `LuaBootstrap` pauses local
simulation ticks on Clients; gameplay phase, pieces, enemies, players, shops,
and match events must come from Host snapshots. Clients automatically send
`MatchJoin` after connecting so Host can bind the connection to a player ID and
return an initial authoritative snapshot before any gameplay click. `MatchJoin`
includes a Host-authored token delivered inside that player's
`MatchStartEnvelope`; Host validates room ID, match ID, player ID, and token
before accepting the join. The expected token table belongs to the active match
session and must survive transport cleanup/rebuild during scene binding. Demo
short reconnect reuses that token only when Host has already observed the old
connection disconnecting and no active connection owns the same player ID.
`RequestSnapshot` remains a no-side-effect player command, but ordinary player
commands no longer establish connection identity. Enemy views may smooth small
position differences between snapshots, but this is presentation-only and must
not create damage, leaks, deaths, or other gameplay results. Future reconnect
support must define saved token storage, process resume, and expiry rules.

`MatchStateSnapshot` may also carry public one-shot `MatchEvent` records.
This is currently used by the LAN Demo to expose joint-defense start, rescued
leaks, final leak resolution, and health damage to Clients without letting
Clients resimulate settlement locally. The same snapshot-carried event path now
also exposes shared Boss creation, board retargeting, damage, endpoint/defeat
outcomes, and final match result logs. The events are presentation data, not the
final reliable combat log: later spectator and reconnect work should add event
IDs, retention, de-duplication, and catch-up keyframes before relying on them
for polished playback.

## Fixed Grid Contract

Deployment commands use a unit instance ID and grid cell ID, never a world
position. The authority validates ownership, phase, occupancy, and capacity.

The current pseudo-3D board prototype lives under `Assets/Game/Board/Prototype`.
Reusable presentation components live under `Assets/Game/Runtime/Board`.
Prototype maps and controls exist only to validate those components.

`DefaultSizeMap_11x7.asset` is the visual editing reference for the current
default personal board. Its runtime gameplay form is the Lua authority config;
the ScriptableObject is not queried as gameplay authority during a match.

The terrain rows in `Config.Board` are written from back to front so their
text layout matches the visible board: the first row is the farthest row and
the final row is the nearest Reserve row. Runtime construction converts this
display-oriented order into ascending authority `grid_y` once at session
startup.

## Shared Boss Contract

Surviving players enter one shared boss battle. A Boss wave may contain regular
minions and exactly one shared Boss. Regular Boss-wave minions are spawned
independently for every alive player; the Boss is created once with one shared
health pool. `target_player_id` represents the board the Boss is currently
visiting. Only that board's pieces can see, block, and attack the Boss during
the current visit.

The Boss no longer loses by reaching a defense endpoint. Boss battle failure is
owned by the Boss defense timer. The current Boss route uses two dedicated route
segments: it walks toward the defense-point doorway, returns toward the spawn
doorway, then transfers to the next alive player's board. If all deployed board
pieces on the current target board are downed at the same time, Session requests
an early transfer.

This replaces the earlier large shared-arena direction. The board-hopping model
keeps the camera readable for up to four players, reuses personal-board
presentation, and still preserves a single authoritative boss result:
defeating the Boss ends in Victory, while the Boss defense timer expiring ends
in Defeat.

## Documentation Discipline

Before changing gameplay, presentation, networking, XLua, or scene workflow,
start with `Docs/CURRENT_STATE.md`, then check the relevant detailed files
under `Docs/` for previous decisions. After a change that alters rules,
architecture boundaries, validation status, or development direction, update
the matching document in the same task. Documentation is part of the
implementation contract, not a separate cleanup phase.

`Docs/GAME_DATA_GUIDE.md` is the designer-facing content configuration manual.
Use it as the first reference when editing pieces, enemies, synergies, shop
probabilities, waves, player numbers, flow timing, board data, and Unity
resource catalogs.

## Platform Constraints

- Windows is the first client and host platform.
- Android initially joins games as a client.
- XLua must be tested early with Android ARM64 and IL2CPP.
- Gameplay scripts must not use desktop-only file paths or APIs.
- Android loads downloaded scripts from persistent data and packaged initial
  scripts from generated Unity Resources inside the APK.

## Build Diagnostics

Windows Player builds include a lightweight runtime log viewer for LAN
validation. `BuildLogViewer` is auto-created only in standalone Windows Player
builds, listens to `Application.logMessageReceived`, and draws an IMGUI overlay
when `F12` is pressed. It shows recent logs, error stacks, and the Unity
`Player.log` folder. This is a development diagnostic surface, not gameplay UI,
and it should not become an authority path or scene dependency.

Windows Player builds also force `Application.runInBackground = true` at runtime
because LAN validation commonly opens Host and Client side by side. Keeping the
unfocused client ticking is required for transport `Pump()` dispatch and match
state synchronization.
