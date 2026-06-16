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
  route progress, Spawn starts, and Endpoint finishes before a session starts.
- Player intent always submits a cell ID. Picking a visible cell does not prove
  that deployment is legal.

Normal defense uses the same static terrain layout for every player. The client
therefore keeps one visible personal-board view and changes only its dynamic
piece, enemy, and effect content when `observedPlayerId` changes. Static board
meshes are not rebuilt when switching between players. The shared Boss arena is
a separate board view because it has different ownership and presentation
requirements.

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

Surviving players enter one shared boss arena. Units retain their owner ID,
while the authority runs one boss battle and one shared boss state.

The current single-player vertical slice already validates the Boss authority
foundation locally: five normal waves advance into `BossPreparation` and
`BossBattle`, defeating the Boss ends in Victory, and the Boss reaching the
endpoint ends in Defeat. Multiplayer shared-arena presentation remains future
work.

## Documentation Discipline

Before changing gameplay, presentation, networking, XLua, or scene workflow,
check the relevant files under `Docs/` for current project state and previous
decisions. After a change that alters rules, architecture boundaries, validation
status, or development direction, update the matching document in the same
task. Documentation is part of the implementation contract, not a separate
cleanup phase.

## Platform Constraints

- Windows is the first client and host platform.
- Android initially joins games as a client.
- XLua must be tested early with Android ARM64 and IL2CPP.
- Gameplay scripts must not use desktop-only file paths or APIs.
- Android loads downloaded scripts from persistent data and packaged initial
  scripts from generated Unity Resources inside the APK.
