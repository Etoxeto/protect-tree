# M4 LAN Multiplayer

## Goal

The first multiplayer milestone is a two-device LAN host/client match using
data structures and protocol rules that support up to four players.

This milestone separates three concepts:

1. **Authority** decides whether a command is legal and owns gameplay state.
2. **Protocol** defines the commands, snapshots, identity checks, versions, and
   ordering rules understood by both sides.
3. **Transport** moves serialized protocol messages between machines.

A LAN networking package is a transport implementation. It does not replace
the authority or protocol rules.

## Step 1 - Framework-independent Protocol Foundation

Status: Implemented and awaiting owner-run validation.

The first protocol contracts live in `ProtectTree.Core.Network`, which has no
Unity, XLua, socket, or networking-package dependency.

### Client to host

`MatchCommand` exposes only supported player intent:

- Set ready
- Purchase, refresh, or upgrade the shop
- Deploy, bench, sell, or rotate a piece

Commands never contain world positions, Gold totals, damage values, health
values, or other client-authored authority state.

Each `PlayerCommandEnvelope` contains:

- Protocol version
- Match ID
- Claimed player ID
- Per-player increasing sequence number
- One `MatchCommand`

`HostCommandGate` compares the claimed player ID with the player ID assigned
to the actual connection. It rejects unsupported versions, commands for
another match, player impersonation, duplicates, and out-of-order commands.
Passing this gate does not make a gameplay command legal; the Lua authority
still validates phase, ownership, cost, capacity, and all gameplay rules.

### Host to client

`MatchStateSnapshot` groups one authoritative simulation tick's public flow,
enemies, pieces, and players, plus only the receiving player's private shop.
The host creates one recipient-scoped snapshot per connected player so clients
cannot inspect another player's shop offers.

Each `ServerSnapshotEnvelope` contains:

- Protocol version
- Match ID
- Global increasing snapshot sequence number
- One `MatchStateSnapshot`

`ClientSnapshotGate` rejects unsupported versions, another match's data,
duplicate or out-of-order snapshot sequences, and simulation-tick rollback.

### Lobby

`LobbySnapshot` and `LobbyPlayerSnapshot` define a stable, maximum-four-player
lobby representation with unique player IDs and no more than one host.

Join/leave authority, transport serialization, discovery, and actual sockets
are deliberately not implemented yet.

### Why two sequence numbers exist

- Client command sequences are independent per player. Player 2's slow or
  missing packet must not block Player 1's next command.
- Server snapshot sequences are global because every client observes one
  ordered authority-state stream.
- Simulation Tick identifies gameplay time. Snapshot sequence identifies
  transmission order. A later snapshot may repeat the same simulation Tick,
  but it must never move to an older Tick.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. In Test Runner, run `NetworkProtocolTests`.
3. Confirm all five protocol tests pass:
   - Command payload contract
   - Host identity and command-order gate
   - Four-player lobby boundary
   - Recipient-only private shop
   - Client snapshot-order gate
4. Run the complete EditMode suite. The exact total should match the current
   Test Runner discovery count; this document no longer treats the old `48/48`
   count as authoritative.

No network package, socket, scene behavior, or single-player gameplay behavior
was changed in this step.

## Step 2 - Local Two-player Authority Prototype

Status: Implemented; owner-run validation passed through the later local
multiplayer gameplay slices.

This step moves the project back toward the original multiplayer goal without
adding new content or UI polish. It validates that the local Lua authority can
run more than one player before a real LAN transport is connected.

### Authority changes

- `Config.Players` now separates player `definitions` from
  `active_player_ids`. Definitions are provided for players 1 through 4, while
  the default active list remains `{ 1 }` so the formal single-player scene is
  unchanged.
- `Match.PlayerConfig` is the single helper for resolving the active player
  set. `PlayerRoster`, `PieceRoster`, `ShopRoster`, and `LeakResolver` now use
  that helper instead of directly iterating `Config.Players`.
- `Match.Session.start(options)` accepts `player_ids`, allowing local authority
  tests to start `{ 1, 2 }` without changing the default game scene.
- `Bootstrap.Main.start_local_multiplayer(player_count)` rebuilds the local
  Session for players `1..player_count`. `LuaRuntime.StartLocalMultiplayer`
  exposes this as a C# test/debug entry.
- Normal-wave spawn requests are copied once per alive player. Each player gets
  independent enemies with the same wave and spawn index, isolated by
  `target_player_id`.
- Personal-defense leak resolution now works over the active player list. Later
  vertical-slice work added joint-defense transfer/rescue and board-hopping
  shared Boss behavior on top of this active-player foundation.

### Presentation boundary

- `MatchSceneContext` now distinguishes `LocalPlayerId` from
  `ObservedPlayerId`.
- `MatchSceneController` has an `observedPlayerId` field. `0` means "follow the
  local player", preserving current single-player behavior.
- `MatchBoardPresenter` drives the board view from `ObservedPlayerId`, while
  interactions remain blocked when the observed player is not the local player.

### Explicit limitations

- This is not LAN networking yet. No socket, transport, lobby scene, host/client
  UI, or serialization path was added.
- The same local authority now includes joint-defense transfer/rescue and
  board-hopping shared Boss behavior, but all players are still simulated
  inside one local process.

### Owner-run validation

1. Let Unity import scripts and confirm the Console has no compile errors.
2. Run `MatchSession_LocalMultiplayerUsesPrivateShopsAndSpawnsForEachAlivePlayer`.
   It should verify two active players, private shops, all-player ready gating,
   and per-player enemy spawns.
3. Run the complete EditMode suite.
4. Enter Play Mode in `SampleScene` and confirm the default scene still behaves
   as single-player: only player 1 is required to ready, and no player 2 enemy
   lane appears unless a local-multiplayer debug entry explicitly restarts the
   Session.

## Step 3 - Host-side Lua Command Router

Status: Implemented; owner-run validation passed.

This step adds the host-side adapter between framework-independent network
protocol commands and the existing Lua authority. It still does not introduce
sockets, a network package, discovery, or a lobby scene.

Implemented:

- `LuaMatchCommandRouter` lives in `ProtectTree.Runtime.Lua` because it directly
  calls `LuaRuntime`.
- The router first passes `PlayerCommandEnvelope` through `HostCommandGate`.
  Protocol version, match ID, connection-assigned player identity, duplicates,
  and out-of-order command sequences are rejected before Lua is touched.
- Accepted commands are mapped to the existing Lua runtime authority calls:
  ready, shop purchase/refresh/upgrade/lock, deploy, bench, sell, facing, and
  place.
- Gameplay legality remains in Lua. The router does not trust client-authored
  cost, health, damage, positions, ownership, phase, or capacity.
- If a command passes the protocol gate but Lua rejects it for gameplay reasons,
  the router returns `CommandRejectionReason.GameplayRejected` and stores the
  exception for diagnostics. The command sequence remains consumed because the
  host did receive and process that ordered command.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is a host plumbing step.

## Step 4 - Host-side Lua Snapshot Factory

Status: Implemented; owner-run validation passed.

This step adds the host-side snapshot output adapter. It complements Step 3's
command input adapter and still does not introduce sockets, a network package,
discovery, or a lobby scene.

Implemented:

- `LuaMatchSnapshotFactory` lives in `ProtectTree.Runtime.Lua` because it reads
  authoritative snapshots from `LuaRuntime`.
- `CreateSnapshot(recipientPlayerId)` builds a `MatchStateSnapshot` from the
  current Lua authority state and includes only that recipient's private shop.
- `CreateEnvelope(recipientPlayerId)` wraps the snapshot in a
  `ServerSnapshotEnvelope` using the current network protocol version, the
  configured match ID, and a monotonically increasing server snapshot sequence.
- Gameplay simulation time still comes from `LuaRuntime.SimulationTick`; the
  network envelope sequence is only transmission ordering.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is a host plumbing step.

## Step 5 - Local Loopback Match Host Pipeline

Status: Implemented; pending owner-run validation.

This step composes the previous host-side pieces into a minimal transport-free
host pipeline. It is intentionally named "loopback" to make clear that it is
not LAN networking yet.

Implemented:

- `LuaLoopbackMatchHost` owns one `HostCommandGate`, one
  `LuaMatchCommandRouter`, and one `LuaMatchSnapshotFactory` around an existing
  `LuaRuntime`.
- `TrySubmitCommand(assignedPlayerId, envelope, out rejectionReason)` is the
  future host receive path. It uses the connection-assigned player ID, not the
  client's claimed identity.
- `LastCommandGameplayException` exposes the latest Lua authority rejection
  captured by the command router for development diagnostics.
- `Tick(deltaTime)` advances the Lua authoritative simulation.
- `CreateSnapshotEnvelope(recipientPlayerId)` creates the recipient-scoped
  outgoing host snapshot.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is a host plumbing step.

## Step 6 - Client-side Snapshot Receiver

Status: Implemented; pending owner-run validation.

This step adds the matching client-side snapshot input cache for the host
snapshot output path. It still does not introduce sockets, a network package,
scene binding, or remote interpolation.

Implemented:

- `MatchSnapshotReceiver` lives in `ProtectTree.Runtime.Network` because it
  does not call Lua. It only validates and caches network snapshots.
- The receiver owns a `ClientSnapshotGate` and exposes the latest accepted
  `MatchStateSnapshot` and `ServerSnapshotEnvelope`.
- The receiver checks `Snapshot.RecipientPlayerId` before accepting a snapshot.
  A snapshot addressed to another player is rejected with
  `SnapshotRejectionReason.WrongRecipient`, so sequence counters are not
  consumed by misdelivered private-shop data.
- Existing protocol, match ID, sequence, and simulation tick checks still live
  in `ClientSnapshotGate`.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is client plumbing.

## Step 7 - Client-side Command Envelope Factory

Status: Implemented; pending owner-run validation.

This step adds the matching client-side command output helper. It still does not
send bytes across a socket; it only wraps a validated `MatchCommand` in a
protocol envelope ready for transport.

Implemented:

- `MatchCommandEnvelopeFactory` lives in `ProtectTree.Runtime.Network`.
- The factory stores the local player ID and match ID, then creates
  `PlayerCommandEnvelope` instances using `NetworkProtocol.CurrentVersion`.
- Each newly created envelope receives the next per-player command sequence.
- If a command must be resent, the existing envelope should be reused instead of
  creating a new envelope and consuming a new sequence number.

Why this matters:

- The local transport-free chain now has the same shape as the future LAN
  command path: client creates `MatchCommand`, client wraps it in an envelope,
  transport sends it, host validates connection identity and sequence, then Lua
  authority validates gameplay legality.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is client plumbing.

## Step 8 - Local Loopback Match Client

Status: Implemented; pending owner-run validation.

This step adds a transport-free client endpoint that can talk to
`LuaLoopbackMatchHost` in the same process. It is a development harness for the
future LAN shape, not a real network client.

Implemented:

- `LoopbackMatchClient` lives in `ProtectTree.Runtime.Network`.
- It owns a `MatchCommandEnvelopeFactory` and a `MatchSnapshotReceiver`.
- `TrySendCommand(command, out reason)` wraps a `MatchCommand` in a
  `PlayerCommandEnvelope` and submits it to the paired `LuaLoopbackMatchHost`.
- `TryReceiveSnapshot(out reason)` pulls this client's recipient-scoped
  snapshot envelope from the host and caches it through `MatchSnapshotReceiver`.
- `TrySendCommandAndReceiveSnapshot(...)` provides a convenient local
  request/refresh path for future harnesses.

Why this matters:

- The no-socket loopback chain is now complete enough for development-time
  validation:
  client command -> envelope -> host gate -> Lua authority -> host snapshot ->
  client receiver.
- Real LAN transport should preserve this shape and replace only the direct
  in-process method calls with serialized messages.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is loopback plumbing.

## Step 9 - Transport Interface Boundary

Status: Implemented; pending owner-run validation.

This step adds the byte-level transport boundary before choosing or wiring a
specific LAN networking package.

Implemented:

- `IMatchHostTransport` defines host-side connection lifecycle and byte payload
  delivery by transport connection ID.
- `IMatchClientTransport` defines client-side connection lifecycle and byte
  payload delivery to the host.
- These interfaces know nothing about `LuaRuntime`, `MatchCommand`,
  `PlayerCommandEnvelope`, player IDs, shops, damage, or snapshots.
- The host still maps connection IDs to assigned player IDs above the transport
  layer before invoking `LuaLoopbackMatchHost` or a future real host pipeline.

Why this matters:

- Unity Transport, Mirror, LiteNetLib, or another LAN solution can be adapted
  behind these interfaces without changing the authority, protocol, or UI
  command-generation code.
- Serialization remains a separate step. Transport sends bytes; protocol
  adapters decide how command and snapshot envelopes become bytes.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is a boundary definition.

## Step 10 - Protocol Codec Boundary

Status: Implemented; pending owner-run validation.

This step adds the boundary between protocol envelopes and transport bytes. It
does not choose JSON, binary, MessagePack, or any concrete format yet.

Implemented:

- `NetworkMessageType` lives in `ProtectTree.Core.Network` and identifies the
  high-level protocol message category:
  `PlayerCommand`, `ServerSnapshot`, and `LobbySnapshot`.
- `IMatchProtocolCodec` lives in `ProtectTree.Runtime.Network` and defines how
  player command envelopes, server snapshot envelopes, and lobby snapshots are
  encoded to or decoded from `byte[]`.
- The codec boundary is separate from `IMatchHostTransport` and
  `IMatchClientTransport`: transport moves bytes, codec understands protocol
  envelope structure.

Why this matters:

- A future Unity Transport adapter can stay byte-only, while a codec
  implementation can evolve separately.
- The first concrete codec can be simple and debuggable. A later binary codec
  can replace it without changing host/client authority code.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is a serialization boundary definition.

## Step 11 - Binary Command, Snapshot, and Lobby Codec

Status: Implemented; owner-run compile validation passed.

This step adds the first concrete codec implementation for the current protocol
envelopes.

Implemented:

- `BinaryMatchProtocolCodec` implements `IMatchProtocolCodec`.
- Every payload starts with a small header: Protect Tree packet magic, protocol
  version, and `NetworkMessageType`.
- `PlayerCommandEnvelope` can be encoded and decoded, including command type,
  player ID, match ID, command sequence, and command-specific optional fields.
- `ServerSnapshotEnvelope` can be encoded and decoded, including recipient ID,
  simulation tick, flow, enemy roster, piece roster, player roster, and the
  recipient-scoped shop snapshot.
- `LobbySnapshot` can be encoded and decoded, including revision, startability,
  player IDs, display names, connection state, ready state, and host flag.
- `GetMessageType(payload)` reads the payload header so transport adapters can
  dispatch bytes before decoding the full message.

Current limitation:

- This codec is a deterministic local binary format, not yet a bandwidth-tuned
  or backward-compatible production protocol. If snapshot fields change, update
  the codec and protocol version together.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is protocol serialization plumbing.

## Step 12 - Encoded Loopback Host/Client Path

Status: Implemented; owner-run compile validation passed.

This step connects the binary codec to the existing local loopback development
path. It is still not real LAN networking, but the command and snapshot flow now
matches the shape a real byte transport will use.

Implemented:

- `LoopbackMatchByteHost` wraps `LuaLoopbackMatchHost` and
  `IMatchProtocolCodec`.
- The byte host accepts `PlayerCommand` payloads, decodes them into
  `PlayerCommandEnvelope`, then forwards them to the existing host authority
  gate and Lua command router.
- The byte host creates recipient-scoped server snapshot envelopes through the
  existing host snapshot factory, then encodes them as `ServerSnapshot` payloads.
- `LoopbackMatchClient` now has a second constructor for the encoded path:
  pass a `LoopbackMatchByteHost` plus an `IMatchProtocolCodec` to make client
  send/receive use `byte[]` payloads.
- The original direct-envelope loopback constructor remains available for
  comparison and debugging.

Why this matters:

- Real LAN transport should not know about Lua, shops, combat, or DTO internals.
  This step proves the future transport can move only bytes while protocol and
  authority stay in their own layers.
- The current loopback setup can now catch codec/schema problems before adding
  socket lifecycle, discovery, or lobby UI.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is encoded loopback plumbing.

## Step 13 - Encoded Loopback Scene Debug Input

Status: Implemented; pending owner-run validation.

This step adds an opt-in scene component for manually validating the encoded
loopback path from Play Mode. It is not a formal network UI and is disabled by
default.

Implemented:

- `EncodedLoopbackDebugInput` lives in `ProtectTree.Runtime.Network`.
- The component requires `LuaBootstrap` on the same GameObject so it can use the
  currently running Lua authority.
- When enabled, it lazily creates:
  - one `BinaryMatchProtocolCodec`;
  - one `LuaLoopbackMatchHost`;
  - one `LoopbackMatchByteHost`;
  - encoded `LoopbackMatchClient` instances for player 1 and player 2.
- Hotkeys:
  - `F7`: rebuild encoded loopback clients.
  - `F8`: send player 1 ready through encoded `byte[]` command payload and
    receive an encoded snapshot back.
  - `F9`: send player 2 ready through encoded `byte[]` command payload and
    receive an encoded snapshot back.
  - `F10`: receive snapshots for both debug clients.
  - `F11`: send ready for both debug clients.
- Logs include whether the client used encoded payloads, accepted snapshot
  sequence, simulation tick, phase, and the player's ready state.

Owner-run validation:

1. Open `Assets/Resources/Scenes/SampleScene.unity`.
2. Select the GameObject that has `LuaBootstrap`.
3. Add component `Protect Tree/Debug/Encoded Loopback Debug Input`.
4. Enable `Enable Debug Input`.
5. Enter the match through the local multiplayer prototype path so players 1
   and 2 both exist.
6. Press `F8`, `F9`, or `F11` in Play Mode.
7. Confirm Console logs include `Encoded=True` and accepted snapshots for the
   corresponding players.

If the scene is entered through single-player mode, player 2 ready commands may
be rejected by Lua authority because player 2 does not exist in that Session.

## Step 14 - TCP LAN Transport Adapter

Status: Implemented; pending owner-run validation.

This step adds the first real process-to-process LAN transport implementation.
It still does not create a lobby screen or room flow, but it gives the project a
concrete byte transport that can later be driven by "Create Room" and "Join
Room" UI.

Implemented:

- `TcpPayloadFraming` defines the transport-level TCP frame format:
  - 4-byte big-endian payload length;
  - raw protocol `byte[]` payload;
  - maximum payload size of 1 MB.
- `TcpMatchHostTransport` implements `IMatchHostTransport` with
  `TcpListener`.
  - `Start(port, maxConnections)` listens on all local interfaces.
  - Each accepted client gets an integer connection ID.
  - Received payloads are reported as `MessageReceived(connectionId, payload)`.
  - Host code can send payloads back with `Send(connectionId, payload)`.
- `TcpMatchClientTransport` implements `IMatchClientTransport` with
  `TcpClient`.
  - `Connect(address, port)` begins an asynchronous direct-IP connection.
  - `Connected`, `Disconnected`, `ConnectionFailed`, and `MessageReceived`
    are queued for main-thread dispatch.
  - `Send(payload)` writes one framed protocol payload to the host.
- Both transport interfaces now expose `Pump()`. Transport receive/connect work
  happens on background tasks, and `Pump()` dispatches queued events on the
  Unity main thread.

Why this matters:

- The project no longer only has protocol contracts and local byte loopback. It
  now has a concrete LAN-capable transport adapter.
- The next step can build a small lobby service and UI on top of this adapter:
  Host listens, Client connects to `IP:Port`, Lobby snapshots travel over the
  same codec, and the Host assigns player IDs.

Current limitations:

- This is direct IP/port LAN connection only. There is no UDP room discovery,
  relay server, NAT traversal, public internet hosting, or invitation service.
- The transport only moves framed bytes. It does not yet know about rooms,
  player IDs, lobby readiness, or match start.
- Android devices must be on the same reachable LAN as the host; emulator
  networking may require using the host machine's LAN IP rather than localhost.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is a transport adapter step.

## Step 15 - LAN Lobby Service Layer

Status: Implemented; pending owner-run validation.

This step adds the first room/lobby service layer above the TCP transport and
binary protocol codec. It still does not create lobby UI or enter the match
scene, but it gives UI code a stable service boundary to call.

Implemented protocol additions:

- `NetworkProtocol.CurrentVersion` is now `3`.
- `NetworkMessageType` now includes:
  - `LobbyCommand`;
  - `LobbyAssignment`.
- `LobbyAssignmentEnvelope` tells a connected client which player ID the Host
  assigned to that connection.
- `LobbyCommandEnvelope` carries lobby-level client intent after assignment.
- `LobbyCommand` currently supports:
  - `SetReady(bool)`;
  - `SetDisplayName(string)`.
- `BinaryMatchProtocolCodec` can now encode/decode lobby assignment and lobby
  command messages in addition to player commands, server snapshots, and lobby
  snapshots.

Implemented services:

- `LobbyHostService`
  - owns one `IMatchHostTransport` and one `IMatchProtocolCodec`;
  - starts Host listening on a TCP port;
  - keeps player 1 as the local Host player;
  - assigns remote players to IDs `2..maxPlayers` by connection;
  - sends `LobbyAssignment` to each remote client;
  - accepts only lobby commands whose player ID matches the assigned
    connection;
  - rejects stale lobby command sequences;
  - updates ready state and display name;
  - broadcasts `LobbySnapshot` to all remote clients;
  - exposes `LobbyChanged`, `PlayerAssigned`, and `ProtocolError` events.
- `LobbyClientService`
  - owns one `IMatchClientTransport` and one `IMatchProtocolCodec`;
  - connects to Host by direct `IP:Port`;
  - receives `LobbyAssignment` and stores the assigned player ID;
  - automatically sends its display name after assignment;
  - can send ready/display-name lobby commands;
  - caches the latest `LobbySnapshot`;
  - exposes connection, assignment, lobby-changed, and protocol-error events.

Why this matters:

- The project now has the core room flow underneath UI:
  Host creates a room, Client joins by IP/port, Host assigns player IDs, and all
  connected clients can see the same lobby snapshot.
- Match start is intentionally not embedded in the transport. The next layer can
  decide when `LobbySnapshot.CanStart` allows the Host to start a match and how
  all clients transition into `SampleScene`.

Current limitations:

- No visual lobby screen is connected yet.
- No match-start message exists yet.
- No reconnect, kick, password, room discovery, or invitation service exists.
- Host and Client services must be pumped by an owning MonoBehaviour so
  transport events dispatch on the Unity main thread.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. No scene setup is required yet; this is a service-layer step.

## Step 16 - LAN Lobby Scene UI Hookup

Status: Implemented; pending owner-run validation.

This step connects the new `LanLobby` scene to the LAN lobby service layer. It
does not yet start a networked match, but it makes the room flow visible and
operable in Unity.

Implemented:

- `LanLobby.unity` is now included in Build Settings between `Menu` and
  `SampleScene`.
- The main menu LAN button now loads `LanLobby` instead of the old local
  two-player prototype shortcut.
- `PlayerProfileOptions` stores the local player name and avatar resource path
  in `PlayerPrefs`.
- `UISettingPanel` can show/hide itself, save the player name, and open the
  avatar picker.
- `UIAvatarSetting` reads avatar buttons from its `Content` children. Clicking
  an avatar stores `UI/Infos/Player/Avatars/{sprite_name}` as the local profile
  avatar path.
- `UIMenuPanel` now exposes UI events for:
  - create room;
  - join room by invite code;
  - return to main menu.
- `UIRoomPanel` now renders `LobbySnapshot` data through the reused
  `UIPlayerInfo` entries, resolves each player's avatar path, and controls
  ready/start/back buttons.
- In the room UI, each player's `personalBackground` uses the same sprite as the
  avatar button/image so the selected portrait is visually consistent.
- `UIPlayerInfo` now supports room rendering through `RenderLobby`, including
  display name, host/local tags, ready text, and ready visual state.
- `UILanLobby` now owns the scene flow:
  - creating a room starts `LobbyHostService` over `TcpMatchHostTransport`;
  - invite code is direct `IP:Port`, default port `7777`;
  - joining a room creates `LobbyClientService` over `TcpMatchClientTransport`;
  - received lobby snapshots update the room panel;
  - lobby snapshots now include each player's selected avatar resource path;
  - ready toggles are sent through the Lobby service layer;
  - leaving the room disposes transport/services and returns to the lobby menu.

When the room enters a match, `UILanLobby` passes the latest lobby avatar paths
to `LanMatchRuntime`. `UIPlayerInfos` then resolves battle HUD avatars by player
ID from that LAN session mapping. This keeps avatar presentation out of Lua
gameplay authority while preserving each player's selected portrait after the
scene transition.

Historical boundary:

- Step 16 stopped at visible create/join/ready room flow. The room `Start`
  button was intentionally left for the next protocol step, which is now covered
  by Step 17.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Open the main menu and click the LAN button. It should load `LanLobby`.
3. In one player, click Create. The Console should log an invite code like
   `192.168.x.x:7777`.
4. In another player instance, enter that invite code and click Join.
5. Confirm both instances show the same player list.
6. Toggle Ready on each instance and confirm ready visuals update on both.

## Step 17 - LAN Lobby Match Start Transition

Status: Implemented; owner-run validation passed.

This step connects the lobby room flow to the match scene. It is intentionally
limited to synchronized scene transition and startup options; it does not yet
turn the battle itself into a network-authoritative host/client simulation.

Implemented protocol additions:

- `NetworkProtocol.CurrentVersion` is now `4`.
- `NetworkMessageType` now includes `MatchStart`.
- `MatchStartEnvelope` carries:
  - protocol version;
  - room ID;
  - generated match ID;
  - starting player count.
- `BinaryMatchProtocolCodec` can encode and decode `MatchStart` payloads.

Implemented service and scene flow:

- `LobbyHostService.TryStartMatch(...)` checks `LobbySnapshot.CanStart`, creates
  one match ID, broadcasts `MatchStart` to connected clients, and exposes the
  start envelope for the Host.
- `LobbyClientService` accepts `MatchStart` only after assignment, checks the
  room ID and protocol version, and raises `MatchStarted`.
- `UILanLobby` now uses the room `Start` button to:
  - broadcast `MatchStart` from the Host;
  - set `MatchStartupOptions.UseLocalMultiplayer(playerCount, localPlayerId)`;
  - load `SampleScene` on Host and Clients.
- The Host waits briefly before unloading the lobby so the TCP transport has a
  chance to flush the final start payload to clients.

Current limitations:

- After entering `SampleScene`, each client currently starts the same local
  multiplayer authority shape with its assigned local player ID. Formal
  in-match command sending, host authority ticking, recipient snapshots, and
  remote presentation are still later M4 work.
- There is no reconnect, late join, countdown, loading progress, or failed-start
  recovery yet.

Owner-run validation passed:

1. Let Unity compile and confirm the Console has no script errors.
2. Start two player instances.
3. Host creates a room; Client joins by invite code.
4. Toggle Ready on both.
5. On Host, click Start.
6. Confirm both instances load `SampleScene`.
7. Confirm each instance observes its assigned player board first. For example,
   Host should start as player 1; the first joined Client should start as player
   2.

## Step 18 - LAN Match Runtime Context

Status: Implemented; owner-run validation passed.

This step preserves the network match identity across the lobby-to-match scene
transition. It is a small foundation step before connecting in-match commands
and snapshots.

Implemented:

- `LanMatchRole` identifies whether this client entered the match as Host or
  Client.
- `LanMatchRuntime` is a `DontDestroyOnLoad` runtime object created when the
  lobby enters `SampleScene`.
- The runtime currently stores:
  - role;
  - room ID;
  - match ID;
  - starting player count;
  - local player ID.
- `UILanLobby` creates the runtime before loading `SampleScene`.
- `UIMainMenu` clears any previous LAN runtime when returning to the menu,
  entering single-player mode, or entering a new LAN lobby.
- `MatchSceneContext` exposes the current `LanMatchRuntime` for future match
  scene features.
- `MatchSceneController` logs the LAN match identity on entry so the owner can
  verify that Host and Client carried the expected IDs into the match scene.

Historical boundary:

- Step 18 only preserved LAN match identity. The first live in-match transport
  path is covered by Step 19.

Owner-run validation passed:

1. Let Unity compile and confirm the Console has no script errors.
2. Start two player instances and enter a LAN room.
3. Ready both players and start the match from Host.
4. Confirm both instances load `SampleScene`.
5. Confirm the Console prints a LAN match log like:
   `Entered match scene with Host Match=... Player=1/2` on Host and
   `Entered match scene with Client Match=... Player=2/2` on the first Client.

## Step 19 - Minimal In-match Ready Sync

Status: Implemented; owner-run validation passed through later LAN validation.

This step creates the first real in-match LAN command/snapshot loop. It is
deliberately limited to the Ready command so the transport, protocol, Host
authority, and Client snapshot receiver can be validated before extending the
same path to shop and deployment commands.

Implemented:

- `LanMatchRuntime` now owns the first in-match transport path:
  - Host listens on `lobby port + 1`, for example `7778` when the lobby uses
    `7777`;
  - Client connects to that match port after entering `SampleScene`;
  - Client retries briefly if it enters the match scene before Host has started
    listening.
- `MatchSceneController` binds the active `LuaRuntime` into `LanMatchRuntime`
  once the match scene runtime is available.
- Host creates a `LuaLoopbackMatchHost` around its Lua authority and accepts
  byte-encoded `PlayerCommand` payloads over TCP.
- Client creates `MatchCommandEnvelopeFactory` and `MatchSnapshotReceiver` for
  the active match ID and local player ID.
- `RoundInfo` routes Client Ready clicks through
  `LanMatchRuntime.TrySendCommand(MatchCommand.SetReady(true))`. Host and
  single-player still use the local Lua authority call directly.
- Host binds a match connection to the `playerId` in the first valid command,
  routes the command through the existing Host command gate and Lua router, then
  sends a recipient-scoped `ServerSnapshot` back to that Client.
- Client accepts the Host snapshot through `MatchSnapshotReceiver`.
- `MatchSceneContext` uses the latest Host snapshot on Client when available,
  while still using local Lua board data for the static map layout.

Current limitations:

- The first connection/player binding trusts the `playerId` in the first valid
  command envelope. This is acceptable for the early LAN Demo, but a later step
  should add a dedicated `MatchJoin` or resume token handshake.
- Ready uses the LAN command path. Step 20 extends the same path to the basic
  shop operations; deployment, selling, facing, and battle input still need to
  be routed through the command channel.
- Host currently sends snapshots in response to received commands. Periodic
  snapshot broadcast for battle playback is still future work.
- Client presentation uses Host snapshots only after the first snapshot arrives.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Start two player instances and enter a LAN room.
3. Ready both players in the lobby and start the match from Host.
4. In `SampleScene`, wait until Client logs:
   `Connected to match host ...`.
5. Click the Ready button on Client.
6. Confirm Client logs `Sent SetReady command`.
7. Confirm Host logs:
   - `Bound connection ... to player 2`;
   - `Host accepted SetReady from player 2`;
   - `Sent snapshot ... to player 2`.
8. Confirm Client logs `Accepted snapshot ...`.
9. Confirm Client UI follows the Host snapshot after receiving it.

## Step 20 - Minimal In-match Shop Command Sync

Status: Implemented; owner-run validation passed.

This step extends the already validated in-match command/snapshot loop from
Ready to the basic shop operations. It deliberately stays within existing
gameplay rules: the Client sends only player intent, while Host Lua still owns
gold, shop offers, bench capacity, merge results, and rejection rules.

Implemented:

- `UIShop` now routes Client purchase, refresh, upgrade, and lock clicks through
  `LanMatchRuntime.TrySendCommand(...)`.
- Host and single-player still call the local `LuaRuntime` methods directly, so
  the normal local workflow is unchanged.
- A LAN Client never mutates its local Lua shop state after clicking a shop
  button. It waits for the Host's recipient-scoped `ServerSnapshot`, then
  `MatchSceneContext` presents the authoritative shop, player, and piece state.
- `MatchKeyboardInput` now applies the same Client command split for the
  preparation/debug keys related to Ready and shop actions. The component
  remains disabled by default and is not formal UI.

Current limitations:

- Host still sends snapshots only in response to received commands. Periodic
  snapshots for battle playback remain future work.
- Step 21 converts formal drag placement, sell/drop, and facing confirmation to
  the same Client-command path.
- There is still no dedicated in-match join/resume handshake; the first valid
  command binds the connection to the claimed player ID for this early LAN Demo.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Build or run two Windows clients, enter a LAN room, ready both players, and
   start the match.
3. In `SampleScene`, wait until Client logs:
   `Connected to match host ...`.
4. On Client, click shop refresh, lock/unlock, upgrade if affordable, and buy a
   visible offer if affordable.
5. Confirm Client logs commands like:
   - `Sent RefreshShop command`;
   - `Sent ToggleShopLock command`;
   - `Sent UpgradeShop command`;
   - `Sent PurchaseShopOffer command`.
6. Confirm Host logs matching accepted commands from player 2 and sends a
   snapshot after each accepted or rejected command.
7. Confirm Client logs `Accepted snapshot ...`.
8. Confirm Client shop, gold, and purchased bench piece update only after the
   Host snapshot arrives.

Owner-run validation passed:

- Client shop refresh, invite-code joining, and ready synchronization behaved as
  expected in packaged Windows LAN clients.

## Step 21 - Minimal In-match Piece Command Sync

Status: Implemented; pending owner-run validation.

This step extends the in-match command/snapshot loop to the formal piece
interaction path. The goal is not to add new gameplay; it keeps the existing
drag, reserve placement, facing confirmation, and sell/drop workflow, but moves
LAN Client authority changes to Host commands.

Implemented:

- `MatchBoardInteraction` now routes Client `PlacePiece` submissions through
  `LanMatchRuntime.TrySendCommand(MatchCommand.PlacePiece(...))`.
- The same formal path covers:
  - dragging from reserve to a battle cell and confirming facing;
  - moving a deployed piece to another battle cell;
  - dragging a piece back to reserve cells.
- Dragging a piece into the shop sell/drop zone now routes Client sale through
  `MatchCommand.SellPiece(...)`.
- Host and single-player still use direct local `LuaRuntime` calls.
- `MatchKeyboardInput` now applies the same Client command split for the
  remaining debug piece keys: bench, sell, deploy, and facing. The component
  remains disabled by default and is not formal UI.

Current limitations:

- Host still sends snapshots only in response to received commands. A Client
  sees its own piece operation after the Host responds, but other remote
  spectators still need periodic or event-driven snapshot broadcast.
- Battle playback, enemy movement snapshots during combat, and stronger
  match-join identity checks remain future M4 work.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Build or run two Windows clients, enter a LAN room, ready both players, and
   start the match.
3. On Client, buy a piece if needed, then drag it from reserve to a valid battle
   cell and confirm facing.
4. Confirm Client logs `Sent PlacePiece command`.
5. Confirm Host logs `Host accepted PlacePiece from player 2` and sends a
   snapshot.
6. Confirm Client logs `Accepted snapshot ...` and the piece remains at the
   Host-confirmed cell/facing.
7. Drag that piece to another valid battle cell or back to reserve and confirm
   another `PlacePiece` round trip.
8. Drag the piece into the shop sell/drop zone.
9. Confirm Client logs `Sent SellPiece command`, Host accepts it, and Client
   gold/piece state updates after the Host snapshot arrives.

## Step 22 - Periodic Host Snapshot Broadcast

Status: Implemented; owner-run validation passed.

This step makes the in-match LAN path useful after players stop clicking
buttons. Before this step, Host sent a snapshot only as a direct response to a
Client command, which meant battle movement could freeze on Client as soon as
there were no more player inputs. Host now pushes recipient-scoped snapshots at
a low fixed rate to every bound match client.

Implemented:

- `LanMatchRuntime` Host broadcasts snapshots every `0.1` seconds to each
  connection that has already been bound to a player ID.
- The broadcast still creates one `ServerSnapshotEnvelope` per recipient, so
  private shop data remains recipient-scoped.
- Command-response snapshots remain unchanged and are still sent immediately
  after Host accepts or rejects a command.
- Host periodic logs are throttled to roughly once per second to avoid flooding
  the Windows build log overlay.

Current limitations:

- Step 23 adds an automatic `RequestSnapshot` command on Client connection, so
  normal clients no longer need manual input before receiving periodic match
  snapshots.
- Snapshots are full-state snapshots, not delta-compressed or interest-managed.
  This is acceptable for the two-player LAN Demo but should be profiled before
  scaling content.
- Step 24 adds lightweight enemy presentation smoothing for the 10Hz snapshot
  stream.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Build or run two Windows clients, enter a LAN room, ready both players, and
   start the match.
3. Ensure the Client has sent at least one in-match command such as Ready,
   shop refresh, or piece placement so Host can bind the connection to player 2.
4. Start a battle round.
5. Confirm Host logs a throttled line like:
   `Broadcasted periodic snapshots to 1 client(s), last seq ...`.
6. Confirm Client continues receiving `Accepted snapshot ...` logs while no
   further buttons are clicked.
7. Confirm enemy movement, health, leaks, and phase changes on Client continue
   to follow Host during the battle instead of freezing at the last command
   response.

## Step 23 - Initial Match Snapshot Request

Status: Implemented; pending owner-run validation.

This step removes the awkward requirement that a Client must click a gameplay
button before Host can bind the in-match connection and begin periodic snapshot
broadcast. The Client now sends one explicit no-op command immediately after
connecting to the match transport.

Implemented:

- `NetworkProtocol.CurrentVersion` is now `5`.
- `MatchCommandType.RequestSnapshot` and `MatchCommand.RequestSnapshot()` define
  a no-side-effect in-match command.
- `BinaryMatchProtocolCodec` can decode the new command.
- `LuaMatchCommandRouter` accepts `RequestSnapshot` without changing Lua
  gameplay state. Host command gating still validates protocol version, match
  ID, player identity, and command sequence.
- `LanMatchRuntime` Client automatically sends `RequestSnapshot` after
  connecting to the match Host.
- Host responds with the existing recipient-scoped snapshot and then includes
  that Client in periodic snapshot broadcasts.

Current limitations:

- This is still an early Demo identity path. The first valid command, now
  usually `RequestSnapshot`, binds the connection to the claimed player ID.
  A formal `MatchJoin` or reconnect token should replace this later.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Build or run two Windows clients, enter a LAN room, ready both players, and
   start the match.
3. On Client, do not click any in-match gameplay button after entering
   `SampleScene`.
4. Confirm Client logs:
   - `Connected to match host ...`;
   - `Sent RequestSnapshot command`;
   - `Requested initial authoritative snapshot`;
   - `Accepted snapshot ...`.
5. Confirm Host logs:
   - `Bound connection ... to player 2`;
   - `Host accepted RequestSnapshot from player 2`;
   - `Sent snapshot ... to player 2`;
   - later `Broadcasted periodic snapshots ...`.
6. Confirm Client receives periodic snapshots without needing an initial Ready,
   shop, or placement click.

## Step 24 - Snapshot Presentation Smoothing

Status: Implemented; owner-run validation passed.

This step improves the visual feel of the periodic snapshot path without
changing networking authority. Host still owns all combat state, and Client
still renders Host snapshots; the Client now eases enemy visuals toward each
snapshot position instead of snapping every time a 10Hz snapshot arrives.

Implemented:

- `BoardEnemyView` keeps a presentation-only position per visible enemy.
- New enemies and large corrections snap immediately to the authoritative
  position, preventing enemies from sliding across the board after observation
  switches or major state corrections.
- Normal small movement updates ease toward the latest authoritative route
  sample using frame-time smoothing.
- Client accepted-snapshot logs are throttled to roughly once per second. The
  snapshot cache still accepts every valid snapshot; only diagnostic logging is
  reduced so the Windows log overlay remains readable.

Current limitations:

- This is presentation smoothing, not client-side prediction. If a snapshot is
  delayed or lost, Client does not invent combat results.
- Piece and effect playback still use current snapshot/event presentation and
  may need their own smoothing or event buffering later.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Build or run two Windows clients, enter a LAN room, and start a battle.
3. Confirm Client enemies keep following Host state but no longer visibly jump
   every 0.1 seconds during normal movement.
4. Confirm changing observed board or receiving a large correction does not
   make enemies glide through unrelated cells.
5. Confirm Client still shows `Accepted snapshot ...` periodically, but the log
   no longer appears at 10 lines per second.

## Step 25 - Explicit In-match Join Handshake

Status: Implemented; owner-run validation passed.

This step separates in-match identity binding from gameplay commands. Before
this step, the first valid player command, usually `RequestSnapshot`, also
bound the TCP connection to a player ID. That was useful for a quick LAN Demo
but made ordinary command flow carry connection-authentication meaning. Client
now sends a dedicated `MatchJoin` message immediately after connecting to the
match Host.

Implemented:

- `NetworkProtocol.CurrentVersion` is now `6`.
- `NetworkMessageType.MatchJoin` defines a separate in-match join payload.
- `MatchJoinEnvelope` carries protocol version, room ID, match ID, and player
  ID.
- `BinaryMatchProtocolCodec` can encode and decode `MatchJoin`.
- `LanMatchRuntime` Client sends `MatchJoin` after connecting to the match
  transport.
- `LanMatchRuntime` Host binds a connection to a player ID only through
  `MatchJoin`, then immediately sends that player's recipient-scoped snapshot.
- `PlayerCommand` messages arriving before `MatchJoin` are rejected instead of
  silently becoming identity-binding messages.
- `RequestSnapshot` remains a no-side-effect player command, but no longer
  performs connection identity binding.

Current limitations:

- Step 26 adds a Host-authored join token to `MatchJoin`; full reconnect still
  needs token reuse rules and timeout/lifetime handling later.
- The legacy helper method used by the earlier first-command binding still
  remains in `LanMatchRuntime` only as unreachable code until the file is next
  cleaned up safely.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Rebuild both Windows clients because the protocol version changed to `6`.
3. Enter a LAN room, ready both players, and start the match.
4. Confirm Client logs:
   - `Connected to match host ...`;
   - `Sent MatchJoin for initial authoritative snapshot`.
5. Confirm Host logs:
   - `Bound connection ... to player 2`;
   - `Host accepted MatchJoin from player 2`;
   - `Sent snapshot ... to player 2`.
6. Confirm Client receives snapshots and can still use Ready, shop, placement,
   sale, and battle playback normally.

Validation notes:

- Owner-provided Host and Client logs confirmed Client sent `MatchJoin`, Host
  bound connection 1 to player 2, Host accepted the `MatchJoin`, and Client
  received the first authoritative snapshot plus ongoing periodic snapshots.

## Step 26 - Match Join Token Validation

Status: Implemented; owner-run validation passed for the normal positive path.

This step makes the explicit `MatchJoin` handshake less trusting. Host now
generates one join token per active player during match start, sends only that
player's token in the recipient-specific `MatchStartEnvelope`, and validates
the token when the player connects to the match transport.

Implemented:

- `NetworkProtocol.CurrentVersion` is now `7`.
- `MatchStartEnvelope` now includes the recipient player's join token.
- `MatchJoinEnvelope` now includes the token returned by the Client.
- `BinaryMatchProtocolCodec` writes and reads the token fields for both
  `MatchStart` and `MatchJoin`.
- `LobbyHostService.TryStartMatch(...)` generates a token for each starting
  player. Host receives player 1's token locally; each remote player receives
  only its own token.
- `LanMatchRuntime.BeginHostSession(...)` receives the Host's token table and
  stores the expected token for each player.
- `LanMatchRuntime.BeginClientSession(...)` receives the Client's own token from
  its `MatchStartEnvelope`.
- `LanMatchRuntime` Host rejects `MatchJoin` if room ID, match ID, player ID, or
  join token does not match the expected values.

Current limitations:

- Tokens are single-match credentials but are not yet reconnect credentials.
  Later reconnect support should define token reuse, expiry, and whether a
  disconnected player may reclaim the same player ID.
- The token is delivered over the same direct TCP lobby connection. This is
  acceptable for the LAN Demo; internet play will need transport security or a
  trusted relay/session service.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Rebuild both Windows clients because the protocol version changed to `7`.
3. Enter a LAN room, ready both players, and start the match.
4. Confirm Client loads `SampleScene`, sends `MatchJoin`, receives the initial
   snapshot, and then continues periodic snapshot playback.
5. Confirm Host logs `Host accepted MatchJoin from player 2`.
6. Confirm Ready, shop, placement, sale, and battle playback still work after
   the token validation change.

Validation notes:

- Owner-provided logs confirmed the normal token-bearing `MatchJoin` path works
  after lobby start and match-scene transition.
- Invalid-token rejection remains a code-path expectation rather than a manual
  owner-run scenario.

## Step 27 - Preserve Match Join Tokens During Scene Bind

Status: Implemented; owner-run validation passed.

This step fixes a reliability issue introduced by the token validation flow.
`LanMatchRuntime.BeginHostSession(...)` stores the expected join token table
before loading the match scene, but `BindRuntime(...)` rebuilds the match TCP
transport after the scene Lua runtime exists. The transport rebuild path must
not erase the session's join token table, or Host will reject the Client's
otherwise valid `MatchJoin`.

Implemented:

- `DisposeMatchTransport()` no longer clears the match join token table.
- `DisposeHostTransport()` no longer clears the match join token table.
- Session reset still clears join tokens through `ResetState()`.
- Starting a new session still replaces join tokens through
  `ConfigureExpectedMatchJoinTokens(...)`.

Current limitations:

- This is still a single-match token. It is preserved across transport rebuilds
  inside the same active session, but reconnect expiry and long-term token
  lifetime rules remain later M4 work.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Rebuild both Windows clients if needed.
3. Create a LAN room, join from Client, ready both players, and start the
   match.
4. Confirm Client enters `SampleScene` and Host logs
   `Host accepted MatchJoin from player 2`.
5. Confirm Client receives snapshots and can use Ready/shop/placement normally.

Validation notes:

- Owner-provided Host log confirmed `BindRuntime(...)` rebuilt the Host match
  transport, then accepted player 2's `MatchJoin`. This means the expected join
  token table survived scene binding.
- Owner-provided logs also confirmed player 2's purchase and placement commands
  were accepted after the initial snapshot path.

## Step 28 - Demo Short Reconnect MatchJoin

Status: Implemented; owner-run validation deferred.

This step turns the existing retry behavior into an explicit LAN Demo reconnect
rule. A Client that briefly loses the match TCP connection may reconnect to the
same Host, send `MatchJoin` again with the same player ID and match token, and
resume receiving authoritative snapshots, as long as Host has already observed
the old connection disconnecting.

Implemented:

- Host records player IDs whose bound match connection disconnected.
- Host accepts a later `MatchJoin` for that player when room ID, match ID,
  player ID, and token still match and no active connection currently owns that
  player ID.
- Host logs reconnect acceptance separately:
  `Host accepted reconnecting MatchJoin from player ...`.
- Invalid `MatchJoin` attempts are disconnected immediately instead of leaving
  a connected client with no snapshots.
- Client sends the same `MatchJoin` after every reconnect attempt.
- Client only resets its retry-attempt counter after receiving an accepted
  authoritative snapshot, not merely after opening a TCP socket.

Current limitations:

- This is short reconnect for the same running client process. If the client
  executable is closed and reopened, it does not yet have a saved match token or
  command sequence to resume with.
- If Host has not yet detected the old TCP connection as disconnected, a second
  connection claiming the same player ID is rejected. This avoids letting a
  duplicate client steal an active player slot.
- Reconnect has no UI prompt yet. It is logged and handled by the runtime.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Start Host and Client, enter a LAN room, ready both players, and start the
   match.
3. Confirm Client receives snapshots normally.
4. Temporarily interrupt only the Client match connection if possible, such as
   by pausing network/firewall rules or briefly stopping the Client process
   without testing full process resume yet. If this is inconvenient, leave this
   validation pending until a controlled disconnect button exists.
5. Confirm Client logs a new connection attempt and sends `MatchJoin` again.
6. Confirm Host logs `Host accepted reconnecting MatchJoin from player 2`.
7. Confirm Client receives snapshots again and can continue Ready/shop/placement
   commands after reconnect.

Validation notes:

- Owner reported that this test condition is temporarily inconvenient to
  simulate, so reconnect validation is intentionally left pending until a
  controlled disconnect path or suitable network interruption test is available.

## Step 29 - Minimal Observed Board Switching

Status: Implemented; owner-run validation passed.

This step exposes the existing `ObservedPlayerId` path through formal match UI
instead of relying on hidden debug controls. Multiplayer clients can now switch
which active player's personal board is rendered while keeping gameplay
authority unchanged.

Implemented:

- `UIPlayerInfos` keeps using `MatchSceneContext.RequestObservePlayer(...)` for
  player-card clicks.
- `UIPlayerInfos` also supports `F1` to `F4` as a small keyboard fallback for
  observing active players 1 to 4.
- `UIPlayerInfo` now brightens the observed player's avatar/background and
  dims non-observed players, giving a simple visible confirmation.
- `MatchSceneController.SetObservedPlayer(...)` remains the single owner of the
  observed-player state. Switching observation clears piece selection and
  inspection blocking.
- Board static terrain is still reused; only dynamic pieces/enemies/effects are
  refreshed for the observed player.
- Interactions remain local-player-only through the existing board interaction
  guard, so observing another player does not let this client operate that
  player's pieces.

Current limitations:

- This is a minimal entry, not final spectator UX. There is no dedicated
  spectate panel, transition animation, or reconnect/status prompt yet.
- Combat event catch-up is still snapshot-driven. Formal timestamped playback
  records and keyframe restoration remain later observation work.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Start Host and Client, enter a two-player LAN room, and enter `SampleScene`.
3. Click the player info entries, or press `F1` and `F2`.
4. Confirm the highlighted player info changes.
5. Confirm the board shows the selected player's pieces/enemies.
6. While observing another player, confirm local piece dragging/placement is not
   allowed on that remote board.
7. Switch back to the local player and confirm local shop/placement operations
   still work.

Validation notes:

- Owner-run validation passed: observing another active player's board works,
  and the minimal player-info/F-key observation entry is usable.

## Step 30 - LAN Joint-defense Event Snapshots

Status: Implemented; owner-run validation passed.

This step makes LAN joint-defense settlement easier to validate from packaged
clients. Before this step, Client presentation could see authoritative phase
and roster snapshots, but one-shot match events were consumed on the Host side
and were not included in `ServerSnapshotEnvelope`. That made it hard to confirm
which player leaked, how many enemies were rescued, and what health was finally
deducted.

Implemented:

- `NetworkProtocol.CurrentVersion` is now `8`.
- `MatchStateSnapshot` can carry a public list of `MatchEvent` records.
- `MatchEvent` now includes optional public fields used by settlement events,
  such as player IDs, defender IDs, leak owner IDs, initial/rescued/final leak
  counts, damage, remaining health, leaking-player count, and transferred enemy
  count.
- `LuaRuntime.DrainMatchEvents()` reads those optional fields from Lua events.
- `LuaMatchSnapshotFactory` and `LuaLoopbackMatchHost` can attach drained
  events to recipient-scoped snapshots.
- `LanMatchRuntime` drains Host Lua events once per periodic snapshot pass and
  includes the same public event list in snapshots sent to all bound Clients.
- Direct command-response snapshots return the latest authoritative state but
  do not drain one-shot events, so public events are not consumed by only the
  player who happened to click a command.
- Client snapshots now expose the received event list through
  `MatchSceneContext.Events`.
- Windows Host and Client logs now print important joint-defense events:
  `JointDefenseStarted`, `LeakedEnemyRescued`, `PlayerLeakResolved`, and
  `PlayerDamaged`.

Current limitations:

- This is still a small LAN-Demo event path, not the final reliable combat
  event stream. Later multiplayer work should add event IDs, de-duplication,
  retention windows, and proper catch-up for spectators or reconnecting
  clients.
- Event delivery is currently coupled to periodic full snapshots. If no
  periodic snapshot is delivered after an event, the Client will not see that
  event as a separate reliable packet.
- In LAN Host mode, network snapshot creation now owns Lua event draining so
  Clients can receive those events. Host local presentation remains state-based
  and may need a shared event distributor later if Host-side event animations
  must play from the same event source.

Owner-run validation:

1. Rebuild both Windows clients, because protocol version `8` is incompatible
   with earlier packaged clients.
2. Start Host and Client, enter a two-player LAN room, and enter `SampleScene`.
3. Create the known validation situation: one player defends fully and another
   player leaks enemies.
4. Confirm both clients advance through the personal battle and joint-defense
   phase normally.
5. Open the in-player log viewer with `F12` or inspect `Player.log`.
6. Confirm logs include `Event JointDefenseStarted`.
7. Confirm settlement logs include `Event PlayerLeakResolved` with
   `initial`, `rescued`, and `final` values.
8. Confirm `Event PlayerDamaged` uses the final leak count after rescue, not the
   original personal-round leak count.

Validation notes:

- Owner-run validation passed: the known one-full-defense/one-leaking-player
  LAN situation advances through joint defense correctly, and final health
  loss matches the rescued leak result.

## Step 31 - LAN Shared Boss Event Visibility

Status: Implemented; owner-run validation passed.

This step keeps the current board-hopping shared Boss authority model and makes
its important facts visible to LAN packaged clients. The goal is not a final
Boss UI; it is to verify that Host authority owns one shared Boss, all clients
receive the same health and target-board state, and the final match result is
consistent.

Implemented:

- `NetworkProtocol.CurrentVersion` is now `9`.
- Public `MatchEvent` data now includes Boss-facing fields:
  `previousTargetPlayerId`, `maxHealth`, and `isBoss`.
- Lua enemy events now include `health` and `max_health` for created, damaged,
  defeated, and endpoint-reaching enemies.
- `BossRetargeted` events are serialized through authoritative snapshots.
- Host and Client logs now print Boss-focused events:
  `BossPhaseChanged`, `BossCreated`, `BossRetargeted`, `BossDamaged`,
  `BossDefeated`, `BossReachedEndpoint`, and `MatchEnded`.

Current limitations:

- Boss presentation still reuses the personal board view and existing enemy
  visuals. A dedicated Canvas `UIBattleCanvas` health bar was later added in
  Step 35, but target-board transition UI is still pending.
- Boss events are still delivered through periodic full snapshots, not through
  a final reliable event stream.

Owner-run validation:

1. Rebuild both Windows clients, because protocol version `9` is incompatible
   with earlier packaged clients.
2. Start Host and Client, enter a two-player LAN room, and enter `SampleScene`.
3. Reach Boss normally, or use the current debug Boss-entry shortcut if that is
   faster for validation: enable `MatchKeyboardInput.enableDebugInput`, then
   press `F6` during `Preparation` to jump to `BossPreparation`.
4. Confirm both clients enter `BossPreparation` and `BossBattle`.
5. Confirm logs include `Event BossCreated`.
6. If both players are alive, wait long enough to confirm logs include
   `Event BossRetargeted` and that the visible board target changes with the
   target player.
7. Damage the Boss and confirm both clients show the same Boss health trend;
   logs should include `Event BossDamaged`.
8. Defeat the Boss or let it reach the endpoint, then confirm logs include
   either `Event BossDefeated` or `Event BossReachedEndpoint`, followed by
   `Event MatchEnded result=Victory` or `result=Defeat`.

Validation notes:

- Owner-provided Host and Client logs confirmed both sides receive the same
  Boss event chain: Boss preparation, Boss battle, Boss creation, damage,
  retargeting between player boards, endpoint defeat, and final match result.
- The validation run ended with `Event BossReachedEndpoint` and
  `Event MatchEnded result=Defeat`, which is a valid Boss failure path.
- The Client log still contains local non-authoritative `LUA: wave=...`
  diagnostic phase lines from its local Lua runtime. These lines do not block
  the current snapshot-driven LAN validation, but they are confusing. Step 32
  addresses this by pausing LAN Client local Lua simulation ticks and removing
  the bare Lua phase print.

## Step 32 - Pause LAN Client Local Lua Simulation

Status: Implemented; owner-run validation passed.

This step removes a confusing LAN Client diagnostic behavior found during Step
31 validation. The packaged Client was correctly driven by Host snapshots, but
its scene-local Lua runtime still advanced independently and printed
`LUA: wave=... phase=...` lines. Those lines were non-authoritative and could
appear out of sync with the real Host match, making logs hard to read.

Implemented:

- `LuaBootstrap.Update()` now pauses local Lua simulation ticks when an active
  `LanMatchRuntime` exists and this process is a LAN Client.
- The Client still starts Lua so static board/config access remains available,
  but gameplay phase, enemies, pieces, players, shop, and events continue to
  come from authoritative Host snapshots through `MatchSceneContext`.
- Host and single-player modes still tick Lua normally.
- `Match.Flow` no longer prints bare phase changes. Phase visibility for LAN
  validation now comes from authority-owned events such as `BossPhaseChanged`,
  `MatchEnded`, and the other network snapshot event logs.

Current limitations:

- The local Client Lua runtime still exists as a config/static-board provider.
  A later cleanup may split static board/config loading from match authority
  startup more cleanly.

Owner-run validation:

1. Let Unity compile and confirm the Console has no script errors.
2. Rebuild both Windows clients if testing packaged LAN logs.
3. Start Host and Client, enter a LAN room, and enter `SampleScene`.
4. Confirm the Client log prints
   `Local Lua simulation tick paused on Client`.
5. Play through at least one normal wave or jump to Boss validation.
6. Confirm the Client log no longer contains local non-authoritative
   `LUA: wave=... phase=...` lines.
7. Confirm Host snapshots, commands, observation switching, joint defense, and
   Boss event logs still appear normally.

Validation notes:

- Owner-provided Client log contains
  `Local Lua simulation tick paused on Client`, then continues accepting
  authoritative Host snapshots.
- The provided Host and Client logs contain no `LUA: wave=...` lines, no
  `Exception`, no `Error`, and no `Warning` matches in the validation filter.
- Both logs still contain joint-defense events, Boss events, and `MatchEnded`,
  so pausing the Client's local Lua tick did not break snapshot-driven LAN
  playback.

## Step 33 - Controlled Short Reconnect Validation

Status: Implemented; owner-run validation passed.

This is the second task in the current LAN stabilization stage. It validates
the existing short reconnect rule without pretending that menu return or
process restart are already supported.

Definitions:

- Returning to the main menu is an intentional session exit. It clears the
  active LAN match runtime and should be treated as "leave match", not
  reconnect.
- Closing the player process is also not supported by the current reconnect
  rule. The current match token, player ID, room ID, match ID, and command
  sequence live in memory only.
- Current reconnect means the same running Client process loses only the match
  TCP connection, keeps its in-memory match identity, reconnects to the Host,
  sends `MatchJoin` again, and resumes from Host snapshots.

Same-player identity for this demo reconnect is:

- same `roomId`;
- same `matchId`;
- same assigned `playerId`;
- same Host-issued match join token;
- Host has already observed the old match connection for that player
  disconnecting, so a second active connection cannot steal the slot.

Implemented:

- `LanMatchRuntime.DebugDisconnectClientTransportForReconnectTest()` closes only
  the Client match transport and keeps the active LAN session identity alive.
- `MatchKeyboardInput` exposes this as a debug-only shortcut:
  `Ctrl+F5` by default when `enableDebugInput` is enabled.
- Host disconnect logs now include the player ID and explicitly mark that player
  reconnectable.
- Client logs when it receives the first authoritative snapshot after
  reconnect, making the validation point easier to find in packaged logs.
- Lobby/menu cleanup paths are unchanged. Returning to menu still clears the
  session and is not a reconnect test.

Full process resume is a later feature. It would require saving at least the
room address, room ID, match ID, player ID, join token, and command sequence or
resend policy, plus a Host-side retention/expiry rule for disconnected players.

Owner-run validation:

1. Rebuild both Windows clients.
2. Start Host and Client, enter a LAN room, ready both players, and enter
   `SampleScene`.
3. Confirm Client has already received normal snapshots.
4. In the Client window, press `Ctrl+F5`.
5. Confirm Client log includes
   `Debug disconnecting client match transport for reconnect test`.
6. Confirm Host log includes
   `player 2 marked reconnectable`.
7. Wait for Client retry, then confirm Client logs:
   - `Connecting to match host ...`;
   - `Sent MatchJoin to resume authoritative snapshots`;
   - `Received authoritative snapshot after reconnect`.
8. Confirm Host log includes
   `Host accepted reconnecting MatchJoin from player 2`.
9. After reconnect, confirm observation, shop, placement, or ready commands can
   still be used normally.

Validation notes:

- Owner-provided logs confirmed the Client-only `Ctrl+F5` path:
  `Debug disconnecting client match transport for reconnect test`.
- Host logged `player 2 marked reconnectable`, then accepted
  `Host accepted reconnecting MatchJoin from player 2`.
- Client logged `Sent MatchJoin to resume authoritative snapshots` and
  `Received authoritative snapshot after reconnect`.
- After reconnect, snapshots continued, countdown/round UI stayed in sync, and
  later map interactions plus player 2 commands were still accepted by Host.
- The Client log includes an earlier lobby `SocketException` from attempting to
  create a room while the lobby port was already in use. It happened before the
  match-scene reconnect test and did not affect the validated match reconnect
  path.

## Step 34 - LAN Lifecycle Status Feedback

Status: Implemented; owner-run validation passed.

This step makes common LAN lifecycle outcomes easier to understand without
adding final UI polish.

Implemented:

- `LanMatchRuntime` now exposes read-only lifecycle state for presentation and
  diagnostics: client connecting, reconnecting, retry exhausted, accepted
  authoritative snapshot, and Host reconnectable-player count.
- `RoundInfo` originally appended a short LAN state to the existing round-state
  text. This was later removed from the player-facing round label during Step
  35, while the underlying lifecycle diagnostics and failure popup remain.
- If a LAN Client exhausts match reconnect attempts, `RoundInfo` shows an
  `UIMessageBox` explaining that the match connection was lost and offers
  returning to the main menu.
- Returning to the main menu from the match result or disconnect popup now
  explicitly clears the active LAN match session before scene load.
- `UIRoomPanel` shows `连接中` / `等待` while a Client is joining a lobby and no
  lobby snapshot has arrived yet.
- `UILanLobby` now catches room creation failures such as an occupied lobby
  port, returns to the lobby menu, and shows a message box instead of leaving
  the error only in logs.
- Lobby Client connection failure, invalid invite code, and room-host
  disconnect now show message boxes and return to the lobby menu.
- Leaving a LAN room or returning from LAN lobby to main menu now logs the
  intentional cleanup path.

Current limitations:

- The lifecycle UI is intentionally minimal and text-based. It reuses existing
  room and round UI rather than adding final connection-status panels.
- Match reconnect remains same-process short reconnect only. Returning to menu
  and process restart still intentionally clear or lose reconnect credentials.

Owner-run validation:

1. Let Unity compile and confirm no Console errors.
2. Rebuild Windows clients for packaged validation.
3. In `LanLobby`, start a Host normally and confirm the room still appears.
4. While one Host already owns the lobby port, try creating another room from a
   second process and confirm a `创建房间失败` popup appears.
5. Try an invalid invite code and confirm a `加入房间失败` popup appears.
6. Try joining an unreachable host and confirm the room panel briefly shows
   `连接中` / `等待`, then returns to the lobby menu with a failure popup.
7. Join a real room, enter `SampleScene`, and confirm round-state text includes
   `LAN 主机` on Host and `LAN 已连接` on Client after snapshots arrive.
8. Press `Ctrl+F5` on Client and confirm the visible LAN state changes through
   reconnect/restoring sync before returning to connected. This may be brief.
9. In a match, close or kill the Host process. After retries exhaust, confirm
   the Client round-state text shows `LAN 连接失败` and a disconnect popup can
   return to the main menu.

Validation notes:

- Owner-run validation passed: lobby popups, connecting labels, in-match LAN
  state labels, reconnect feedback, and match disconnect popup all behaved as
  expected.
- This closes the functional lifecycle-feedback slice. Later dedicated UI art
  can replace the temporary text placement without changing the lifecycle
  authority or transport rules.
- This is not the player-facing multiplayer flow UI. It is a supporting
  diagnostics and fallback slice so connection failures are not silent.

## Step 35 - Player-facing Multiplayer Flow UI

Status: Partially implemented; awaiting owner-run validation.

This is the intended player-facing UI slice for the current LAN stage. It
should help players understand what is happening in the multiplayer match,
without exposing low-level transport states such as "restoring sync" or
"connecting to match host" unless the player must take action.

Planned UI responsibilities:

- Lobby and pre-battle readiness: show each player's ready state and clearly
  explain when the match is waiting for other players.
- Board observation: show whose board is currently being watched and make it
  clear when the player is observing another player's defense area.
- Personal defense phase: show that each player is defending their own board.
- Joint-defense phase: show which player is defending for others, whose leaks
  were transferred, how many were rescued, and each leaking player's final leak
  count.
- Shared Boss phase: show the Boss health and which player's board the shared
  Boss is currently targeting.
- Match result: show victory/defeat and the player-readable reason, such as
  Boss defeated, Boss reached endpoint, or all players eliminated.

Implementation guidance:

- The UI should bind to authoritative `MatchSceneContext` snapshots and public
  `MatchEvent` records, not to local guesses.
- The existing lifecycle labels and debug logs may stay for development, but
  final player-facing UI should not foreground technical connection state.
- The first implementation can reuse current UI containers if convenient, but
  should keep separate scripts/components from transport diagnostics so the
  presentation remains maintainable.

Implemented in the first UI wiring pass:

- `UIFocusPlayer` is now a scene feature. It shows the currently observed board
  during remote observation, joint defense, and shared Boss targeting, while
  staying hidden when the player is simply watching their own normal board.
- During `JointDefense`, `MatchSceneController` forces observation to the
  defending player's board. Manual observation can resume after joint defense
  ends.
- `UILoadingStatus` now animates by rotating the colors already configured on
  its arrow images. `UITipsPanel` can auto-find the main loading status if the
  field is not manually bound.
- `UITipsPanel` shows short, held battle-result prompts from authoritative
  events: all-clear, HP loss, and entering joint defense. It also uses phase
  changes and state snapshots as a fallback so the Host-side local view can
  still show prompts even though LAN Host event delivery is currently owned by
  network snapshot creation.
- `UIPlayerInfo` now supports the prepared/personal-finished marker. In
  preparation phases it reflects player readiness; during normal battle it
  appears after that player's current-wave enemies have all left the active
  state.
- `RoundInfo` now tracks the observed board's current-wave defeated, leaked,
  and known enemy counts. During normal battle the phase label changes from a
  generic battle label to defeated/total, and the leak counter appears when
  leaks exist.
- During joint defense, `RoundInfo` also shows defeated/total instead of a
  generic joint-defense label. The defeated/total value follows the defending
  player's joint-defense enemies, while the leak counter is owner-scoped: the
  defending player's local view hides it, and a leaking player's local view only
  counts that player's own transferred enemies that still reach the endpoint.
- `UIBattleCanvas` now drives the Canvas Boss health Slider from the
  authoritative Boss enemy snapshot during `BossBattle`.

Remaining work for this player-facing UI slice:

- Add explicit text/detail for joint-defense results: defender, leaking players,
  rescued count, and each leaking player's final leak count.
- Add clearer shared Boss target-board presentation and transition feedback.
- Validate the new UI in Host, Client, and single-player paths, especially the
  timing of result prompts and the Host-side event fallback.

## Windows LAN Build Defaults

Status: Implemented; pending owner-run validation.

Windows player builds are configured for local multi-client testing:

- default resolution: `1280x720`;
- default fullscreen mode: windowed;
- resizable window: enabled;
- fullscreen switch: enabled;
- visible in background: enabled.
- run in background: enabled, so unfocused Windows clients continue ticking and
  dispatching LAN transport events.
- in-player log viewer: press `F12` in a Windows build to inspect recent logs
  and find the `Player.log` folder.

`Protect Tree > Build Windows Player` applies these settings before building so
two Windows clients can be opened and positioned side by side for LAN lobby
testing. The standard output path remains `Builds/Windows/ProtectTree.exe`.
`visibleInBackground` only keeps the window visible while unfocused;
`runInBackground` is the setting that keeps `Update()` and network `Pump()`
running.

## Protocol 10 - Piece Attack Range Snapshot

`PieceSnapshot` now includes configured attack range offsets for board
presentation. Host and Client builds must both use protocol version `10`; older
protocol-9 clients should be rejected by the existing version gate instead of
trying to decode the changed piece snapshot payload.
