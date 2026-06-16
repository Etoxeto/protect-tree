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

Status: Implemented; awaiting owner-run validation.

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
- Personal-defense leak resolution now works over the active player list. It
  still performs direct per-player leak damage; joint defense remains a later
  step.

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
- This is not joint defense yet. Normal-wave leaks are recorded and resolved
  per player.
- Multiplayer shared Boss is still future work. The current implementation is
  intended to validate normal personal-defense authority first; shared Boss
  arena ownership and presentation must be designed separately.

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
