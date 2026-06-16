# M2 Single-Player Vertical Slice

## Step 1 - Authoritative Match Phases

Status: Complete on 2026-06-10.

The first M2 gameplay task is a Lua-owned match phase state machine:

```text
Preparation -> Battle -> Settlement -> Preparation
                                      -> End
```

The C# runtime now advances Lua gameplay at a fixed simulation step of `0.1`
seconds. Unity frame rate can vary, but each Lua `update(delta_time)` call
receives the same simulation duration. This prepares the gameplay core for a
server-authoritative session later.

The clock allows at most eight simulation steps during one Unity frame. Extra
accumulated time is dropped to prevent an unrecoverable catch-up loop.

### Owner Implementation Task

Create `Assets/Game/Lua/Match/Flow.lua.txt`. Keep all state module-local and do
not reference Unity GameObjects.

Temporary learning values:

- Preparation: 3 seconds
- Battle: 5 seconds
- Settlement: 2 seconds
- Total waves: 3

Required public API:

```lua
start()
update(delta_time)
shutdown()
get_snapshot()
```

`get_snapshot()` should return a plain Lua table containing:

```lua
{
    phase = "Preparation",
    wave = 1,
    remaining_seconds = 3,
    is_finished = false,
}
```

Rules:

1. `start` initializes wave 1 in `Preparation`.
2. `update` reduces the current phase timer.
3. When a timer reaches zero, carry excess time into the next phase.
4. After `Settlement`, start the next wave in `Preparation`.
5. After wave 3 settlement, enter `End`.
6. `End` does not advance further.
7. Print one log only when the phase changes, not every simulation tick.

Do not yet implement enemies, victory conditions, ready buttons, or rewards.
Those enter after the phase flow is stable.

### Acceptance Sequence

With the temporary durations, the Console should eventually show:

```text
wave=1 phase=Preparation
wave=1 phase=Battle
wave=1 phase=Settlement
wave=2 phase=Preparation
...
wave=3 phase=Settlement
wave=3 phase=End
```

After `Match.Flow` works independently, the next step will connect it to
`Bootstrap.Main` and add an automated Lua integration test.

Validation completed:

- `Bootstrap.Main` forwards fixed simulation steps to `Match.Flow`.
- Lua exposes a plain-table match snapshot.
- C# converts the Lua table into a read-only `MatchFlowSnapshot`.
- The EditMode integration test verifies all three waves and the final `End`
  state without depending on Console log text.

## Step 2 - Phase Transition Events

Status: Complete on 2026-06-10.

The next gameplay task is to let other gameplay systems react to phase changes
without reading Console logs or checking the snapshot every simulation tick.

Add a module-local pending-event queue to `Match.Flow`. Every phase entry
should append one plain Lua table:

```lua
{
    type = "PhaseChanged",
    wave = 1,
    phase = "Battle",
}
```

Add this public API:

```lua
drain_events()
```

`drain_events()` returns all pending events and clears the internal queue.
Calling it again before another transition must return an empty table.

Acceptance rules:

1. `start()` produces one `Preparation` phase event.
2. Entering each new phase produces exactly one event.
3. Normal fixed-step updates that do not change phase produce no events.
4. The final `End` phase produces one event.
5. Event tables contain only gameplay data and do not reference Unity objects.

Validation completed:

- `Bootstrap.Main` exposes `drain_match_events()` without consuming events
  during every update.
- C# converts the returned Lua event array into read-only `MatchEvent`
  objects.
- The EditMode integration test verifies empty, single-event, consume-once,
  and accumulated multi-event batches.

## Step 3 - Wave Spawn Planner

Status: Complete on 2026-06-11.

The first battle-facing module will create deterministic enemy spawn requests.
It does not instantiate Unity GameObjects. A later C# presentation system will
turn those requests into visible enemies.

Create `Assets/Game/Lua/Config/Waves.lua.txt`:

```lua
return {
    [1] = {
        enemy_id = "Slime",
        count = 3,
        interval_seconds = 1.0,
    },
    [2] = {
        enemy_id = "Slime",
        count = 4,
        interval_seconds = 0.8,
    },
    [3] = {
        enemy_id = "Slime",
        count = 5,
        interval_seconds = 0.6,
    },
}
```

Create `Assets/Game/Lua/Match/WaveSpawner.lua.txt`. Required public API:

```lua
start()
handle_event(event)
update(delta_time)
shutdown()
get_snapshot()
drain_events()
```

Required internal state:

```lua
current_wave
enemy_id
remaining_count
spawn_index
seconds_until_next_spawn
is_active
pending_events
```

Behavior:

1. `start()` resets all state and clears pending events.
2. `handle_event(event)` ignores events that are not `PhaseChanged`.
3. Entering `Battle` loads the matching wave configuration and activates the
   spawn plan.
4. Entering any non-`Battle` phase stops an active spawn plan.
5. The first enemy is requested on the first update after `Battle` begins.
6. Later enemies are requested according to `interval_seconds`.
7. Spawn timing carries overflow into the next interval.
8. After the final request, the plan becomes inactive.

Each requested enemy appends:

```lua
{
    type = "EnemySpawnRequested",
    wave = 1,
    enemy_id = "Slime",
    spawn_index = 1,
}
```

After the final enemy request, append:

```lua
{
    type = "WaveSpawnCompleted",
    wave = 1,
}
```

`get_snapshot()` returns:

```lua
{
    is_active = true,
    wave = 1,
    enemy_id = "Slime",
    remaining_count = 2,
    next_spawn_index = 2,
    seconds_until_next_spawn = 0.9,
}
```

Validation completed:

- `WaveSpawner` loads the owner-defined `Crab` wave configuration.
- `Battle` activation, fixed-interval requests, overflow timing, completion,
  snapshots, and event draining are implemented.
- `WaveSpawner` remains independent from `Flow` and Unity GameObjects.

## Step 4 - Match Session Coordinator

Status: Complete on 2026-06-11.

`Match.Session` is now the single owner of gameplay update ordering and
internal-event draining:

```text
Flow update
    -> drain and forward phase events
    -> WaveSpawner update
    -> collect public match events
```

It forwards `PhaseChanged` events to `WaveSpawner`, then republishes phase and
spawn events through one public queue. `Bootstrap.Main`, C#, UI, networking,
and tests no longer drain producer queues independently.

Validation completed:

- Entering `Battle` activates the correct wave plan.
- The same fixed simulation tick publishes `PhaseChanged` before the first
  `EnemySpawnRequested`.
- C# reads generic `MatchEvent` objects and `WaveSpawnerSnapshot`.
- The complete EditMode suite passes with the session integration.

## Step 5 - Enemy Gameplay State

Status: Independent module complete on 2026-06-11. Session integration is
complete on 2026-06-11.

The next Lua gameplay module will consume `EnemySpawnRequested` and create
authoritative enemy data records. It will still not instantiate Unity
GameObjects. A later C# view will display those records as 2D characters.

Create `Assets/Game/Lua/Config/Enemies.lua.txt`:

```lua
return {
    Crab = {
        max_health = 10,
    },
}
```

Create `Assets/Game/Lua/Match/EnemyRoster.lua.txt`. Keep it independent from
`WaveSpawner`, `Flow`, `Session`, and Unity GameObjects.

Required public API:

```lua
start()
handle_event(event)
get_snapshot()
drain_events()
shutdown()
```

Required internal state:

```lua
next_instance_id
enemies_by_id
ordered_enemy_ids
pending_events
```

An enemy definition ID identifies a kind of enemy:

```text
enemy_id = "Crab"
```

An instance ID identifies one specific spawned enemy:

```text
enemy_instance_id = 1
enemy_instance_id = 2
enemy_instance_id = 3
```

`handle_event(event)` ignores events other than `EnemySpawnRequested`. For
each spawn request, it must:

1. Validate that `Config.Enemies[event.enemy_id]` exists.
2. Allocate the current `next_instance_id`, then increment it.
3. Create one authoritative record:

```lua
{
    instance_id = 1,
    enemy_id = "Crab",
    wave = 1,
    spawn_index = 1,
    health = 10,
    max_health = 10,
    status = "Alive",
}
```

4. Store the record in `enemies_by_id[instance_id]`.
5. Append the ID to `ordered_enemy_ids`.
6. Append one public event:

```lua
{
    type = "EnemyCreated",
    wave = 1,
    enemy_id = "Crab",
    spawn_index = 1,
    enemy_instance_id = 1,
}
```

`get_snapshot()` returns a new snapshot table:

```lua
{
    alive_count = 1,
    enemies = {
        {
            instance_id = 1,
            enemy_id = "Crab",
            wave = 1,
            spawn_index = 1,
            health = 10,
            max_health = 10,
            status = "Alive",
        },
    },
}
```

Build the snapshot by iterating `ordered_enemy_ids` with `ipairs`. Do not build
it with `pairs(enemies_by_id)`, because Lua does not guarantee `pairs`
iteration order. Stable ordering is required for deterministic tests,
network snapshots, and replays.

The snapshot must contain copied enemy tables. Do not return the internal
records directly, because a caller could then modify authoritative state.

Behavior rules:

1. `start()` sets `next_instance_id` to `1` and clears all records/events.
2. Irrelevant events do nothing.
3. Missing enemy configuration raises a clear error.
4. Each accepted request creates exactly one enemy.
5. Instance IDs increase deterministically and are never reused during a
   match.
6. `get_snapshot()` returns enemies in instance-ID creation order.
7. `drain_events()` returns pending events once, then clears the queue.
8. `shutdown()` clears all records and pending events.

Do not implement movement, damage, death, endpoint arrival, rewards, or
GameObjects yet. Do not connect `EnemyRoster` to `Match.Session` yet. The next
integration step will route spawn events through the roster and add C# enemy
snapshots.

Validation completed:

- Three spawn requests create deterministic instance IDs `1`, `2`, and `3`.
- Enemy definitions are loaded from `Config.Enemies`.
- Snapshots preserve creation order and return copied records.
- Modifying a returned snapshot cannot modify authoritative enemy health.
- `EnemyCreated` events are drained once.
- The complete EditMode suite passes with `12/12` tests.

Session integration completed:

- `Match.Session` routes each `EnemySpawnRequested` through `EnemyRoster`.
- Public event order is deterministic:
  `EnemySpawnRequested` followed by its matching `EnemyCreated`.
- C# reads copied `EnemyRosterSnapshot` and `EnemySnapshot` objects.
- The complete generation chain passes with `13/13` EditMode tests.

## Step 6 - Enemy Path Progress

Status: Completed.

The next step adds deterministic movement as gameplay data. Each alive enemy
will advance along an abstract normalized path:

```text
path_progress = 0.0  -> spawn point
path_progress = 1.0  -> endpoint
```

Unity world positions and sprites remain presentation concerns. Lua owns only
the authoritative progress value and endpoint result.

Modify `Config.Enemies`:

```lua
Crab = {
    max_health = 10,
    path_speed = 0.25,
}
```

`path_speed = 0.25` means the enemy advances through 25% of the complete path
per second and reaches the endpoint after four simulated seconds.

Modify `EnemyRoster`:

1. Add `path_speed` and `path_progress = 0` to each new enemy record.
2. Track a module-local `alive_count` instead of using
   `#ordered_enemy_ids`.
3. Add `update(delta_time)` to the public API.
4. During update, iterate `ordered_enemy_ids` with `ipairs`.
5. Advance only records whose status is `"Alive"`:

```lua
enemy.path_progress =
    enemy.path_progress + enemy.path_speed * delta_time
```

6. When progress reaches or exceeds `1`, clamp it to `1`, change status to
   `"ReachedEndpoint"`, decrement `alive_count`, and append exactly one event:

```lua
{
    type = "EnemyReachedEndpoint",
    wave = enemy.wave,
    enemy_id = enemy.enemy_id,
    spawn_index = enemy.spawn_index,
    enemy_instance_id = enemy.instance_id,
}
```

7. Include `path_speed` and `path_progress` in copied snapshot records.
8. Return the tracked `alive_count` from `get_snapshot()`.

Important rules:

- An enemy that already reached the endpoint must not move or emit another
  endpoint event.
- Multiple enemies may reach the endpoint during the same update.
- Progress is authoritative gameplay data; do not use Unity positions,
  transforms, vectors, or GameObjects.
- Do not connect `EnemyRoster.update()` to `Match.Session` yet. The next
  integration step will define exactly where enemy movement occurs in the
  simulation update order.

Validation completed:

- Alive enemies advance deterministically from `path_speed * delta_time`.
- Exact arrival and overshoot both clamp `path_progress` to `1`.
- Reached enemies remain in stable snapshot order but no longer move.
- `EnemyReachedEndpoint` is emitted exactly once for each reached enemy.
- Multiple enemies may reach the endpoint during the same update.
- `start()` resets records, pending events, instance IDs, and `alive_count`.
- `EnemyRoster.update()` remains independent from `Match.Session`.
- The complete EditMode suite passes with `16/16` tests.

## Step 7 - Session Enemy Movement Integration

Status: Completed.

This step connects deterministic enemy movement to the match simulation. The
temporary Demo update order for each fixed tick is:

```text
1. Update enemies that already existed at the start of the tick.
2. Publish EnemyRoster events.
3. Update Flow and route phase events.
4. Update WaveSpawner and route spawn events through EnemyRoster.
```

This order gives two useful guarantees:

- A newly created enemy keeps `path_progress = 0` during its creation tick and
  starts moving on the following tick.
- If an endpoint arrival and a phase change happen on the same tick, the
  `EnemyReachedEndpoint` event is published first.

For now, existing alive enemies continue moving across phase changes. This is
an intentional temporary rule because the current five-second `Battle` timer
may finish while enemies are still alive. A later step will replace the fixed
Battle completion rule with a condition based on spawning and surviving
enemies.

Modify only `Match.Session.update(delta_time)`. After the `is_started` guard
and before `flow.update(delta_time)`, add:

```lua
enemy_roster.update(delta_time)
publish(enemy_roster.drain_events())
```

Do not move these calls after `wave_spawner.update(delta_time)`, because that
would cause newly created enemies to move during their creation tick.

Framework work completed:

- C# `EnemySnapshot` now exposes `PathSpeed` and `PathProgress`.
- `LuaRuntime.GetEnemyRosterSnapshot()` copies both path fields from Lua.
- Integration tests define creation-tick movement and public event ordering.

Validation completed:

- The first enemy has progress `0` on its creation tick.
- On the next fixed tick, its progress is `0.025`.
- All three wave-one enemies emit one endpoint event.
- The second endpoint event is published before the same-tick transition to
  `Settlement`.
- The third enemy continues moving and reaches the endpoint during
  `Settlement`.
- The complete EditMode suite passes with `17/17` tests.

## Step 8 - Battle Completion Condition

Status: Completed.

The fixed five-second Battle duration is no longer correct. A Battle now ends
only when both conditions are true:

```text
WaveSpawner emitted WaveSpawnCompleted
AND
EnemyRoster alive_count == 0
```

The responsibilities remain separated:

- `Flow` owns phase state and performs a requested phase transition.
- `WaveSpawner` owns whether all configured spawn requests were emitted.
- `EnemyRoster` owns how many enemies remain alive.
- `Session` owns the cross-module Battle completion condition.

### Flow changes

Remove `Battle` from `phase_durations`. Add a helper that returns `0` for
phases without a timer:

```lua
local function get_phase_duration(phase)
    return phase_durations[phase] or 0
end
```

Use this helper when `enter_next_phase()` assigns `remaining_seconds`.

`Flow.update(delta_time)` must not count down during Battle:

```lua
if current_phase == "Battle" then
    return
end
```

Also add `current_phase ~= "Battle"` to the existing transition loop
condition. This prevents entering Battle and immediately leaving it again
because its remaining time is `0`.

Add a guarded public command:

```lua
function M.complete_battle()
    if not is_started or is_finished or current_phase ~= "Battle" then
        return false
    end

    enter_next_phase()
    return true
end
```

Calling this outside Battle must do nothing and return `false`.

### EnemyRoster change

Add a cheap read-only query:

```lua
function M.get_alive_count()
    return alive_count
end
```

Do not call `get_snapshot()` every fixed tick just to read the count. That
would copy every historical enemy record unnecessarily.

### Session changes

Add module state:

```lua
local is_wave_spawn_completed = false
```

Reset it in `start()` and `shutdown()`. When routing a `PhaseChanged` event,
reset it to `false`. When routing a `WaveSpawnCompleted` event, set it to
`true`.

Add:

```lua
local function try_complete_battle()
    if not is_wave_spawn_completed then
        return
    end

    if enemy_roster.get_alive_count() > 0 then
        return
    end

    if flow.complete_battle() then
        route_flow_events()
    end
end
```

Call `try_complete_battle()` at the end of `Session.update(delta_time)`, after
`route_spawner_events()`.

This preserves the fixed-tick event order:

```text
EnemyReachedEndpoint
PhaseChanged(Settlement)
```

Validation completed:

- Battle reports `remaining_seconds = 0` and does not end from elapsed time.
- Completing all spawn requests is insufficient while enemies remain alive.
- Reaching zero alive enemies is insufficient until spawning is complete.
- Wave one enters Settlement when its third enemy reaches the endpoint.
- Wave-one Battle lasts six simulated seconds instead of five.
- All three waves still reach the final End phase.
- The complete EditMode suite passes with `18/18` tests.

## Step 9 - Independent Player Health Roster

Status: Independent roster state completed. Its direct endpoint-damage input
contract is superseded by Step 10.

This step adds authoritative player health without connecting it to
`Match.Session` yet. The module is a roster rather than a single-player value
so the same state shape can later contain up to four players.

Create `Assets/Game/Lua/Config/Players.lua.txt`:

```lua
return {
    [1] = {
        max_health = 10,
    },
}
```

Create `Assets/Game/Lua/Match/PlayerRoster.lua.txt`.

Required public API:

```lua
start()
handle_event(event)
get_alive_count()
get_snapshot()
drain_events()
shutdown()
```

Required module-local state:

```lua
players_by_id
ordered_player_ids
pending_events
alive_count
```

`start()` clears all old state and creates one player record for every entry
in `Config.Players`, preserving numeric player-ID order:

```lua
{
    player_id = 1,
    health = 10,
    max_health = 10,
    status = "Alive",
}
```

`handle_event(event)` ignores every event except `EnemyReachedEndpoint`. An
endpoint event must contain `target_player_id`, `wave`, and
`enemy_instance_id`. Missing or unknown target player IDs should raise a clear
error.

If the targeted player is still alive:

1. Subtract exactly `1` health and clamp health to a minimum of `0`.
2. Append:

```lua
{
    type = "PlayerDamaged",
    player_id = player.player_id,
    damage = 1,
    health = player.health,
    wave = event.wave,
    enemy_instance_id = event.enemy_instance_id,
}
```

3. If health became `0`, change status to `"Eliminated"`, decrement
   `alive_count`, then append:

```lua
{
    type = "PlayerEliminated",
    player_id = player.player_id,
    health = player.health,
    wave = event.wave,
    enemy_instance_id = event.enemy_instance_id,
}
```

Important rules:

- `PlayerDamaged` is emitted before `PlayerEliminated`.
- Read the damaged player from `event.target_player_id`. Do not use a generic
  `event.player_id` for enemy ownership because that becomes ambiguous once
  joint defense is introduced.
- An eliminated player ignores later endpoint events.
- Health must never become negative.
- `PlayerEliminated` is emitted exactly once.
- `get_alive_count()` returns the tracked count without copying snapshots.
- `get_snapshot()` returns copied player records in stable player-ID order:

```lua
{
    alive_count = 1,
    players = {
        {
            player_id = 1,
            health = 10,
            max_health = 10,
            status = "Alive",
        },
    },
}
```

- `drain_events()` returns pending events once and clears the queue.
- `shutdown()` clears records and pending events.
- Do not require or connect `PlayerRoster` from `Match.Session` yet. The next
  step will add player ownership to endpoint events and define routing order.

Expected validation after the owner edit:

- Player configuration creates deterministic player records.
- Returned snapshots cannot mutate authoritative health.
- Ten endpoint events reduce player one from `10` health to `0`.
- The tenth endpoint emits `PlayerDamaged` followed by `PlayerEliminated`.
- An eleventh endpoint event does nothing.
- Calling `start()` after elimination restores configured health and status.
- The complete EditMode suite passes with `20/20` tests.

## Step 10 - Settlement-Based Player Damage

Status: Owner implementation completed and passed static review. Owner-run
test result not recorded in this document.

`EnemyReachedEndpoint` means an enemy escaped one player's personal defense
area. It does not necessarily mean the target player loses health, because a
joint-defense player may still defeat that enemy.

`PlayerRoster` must therefore ignore `EnemyReachedEndpoint` and consume only a
resolved settlement result:

```lua
{
    type = "PlayerLeakResolved",
    player_id = 1,
    wave = 1,
    initial_leak_count = 5,
    rescued_count = 2,
    final_leak_count = 3,
}
```

Field meanings:

- `player_id`: the player whose round result is being settled.
- `initial_leak_count`: enemies that escaped that player's personal defense.
- `rescued_count`: those escaped enemies later defeated during joint defense.
- `final_leak_count`: enemies still unresolved after joint defense.

The resolver that creates this event will be implemented separately. In
single-player mode it will use `rescued_count = 0`, but the player-health
module follows exactly the same contract.

Modify `PlayerRoster.handle_event(event)`:

1. Ignore every event except `PlayerLeakResolved`.
2. Require an existing `event.player_id`.
3. Validate all three leak-count fields as non-negative integers.
4. Validate:

```lua
event.final_leak_count ==
    event.initial_leak_count - event.rescued_count
```

5. Ignore the event if `final_leak_count == 0`.
6. Ignore it if the player is already eliminated.
7. Calculate actual applied damage:

```lua
local damage = math.min(event.final_leak_count, player.health)
```

8. Subtract `damage` and append:

```lua
{
    type = "PlayerDamaged",
    player_id = player.player_id,
    damage = damage,
    leak_count = event.final_leak_count,
    health = player.health,
    wave = event.wave,
}
```

`leak_count` preserves the settlement result, while `damage` reports the
actual health removed. For example, ten final leaks against seven remaining
health produce `leak_count = 10`, `damage = 7`, and `health = 0`.

9. If health became `0`, append `PlayerEliminated` after `PlayerDamaged`.

Important rules:

- Do not read `event.target_player_id` in `PlayerRoster`. That field belongs to
  an enemy before leak resolution. `PlayerLeakResolved` is already a
  player-specific settlement result, so it uses `event.player_id`.
- Do not include `enemy_instance_id` in `PlayerDamaged` or
  `PlayerEliminated`; one settlement event may represent many enemies.
- `PlayerDamaged` is emitted once per non-zero settlement result, not once per
  enemy.

Expected validation after the owner edit:

- A raw `EnemyReachedEndpoint` event does not change player health.
- `5` initial leaks with `5` rescues cause no damage.
- `5` initial leaks with `2` rescues deduct `3` health.
- `10` final leaks against `7` remaining health deduct exactly `7`.
- The lethal settlement emits `PlayerDamaged` followed by
  `PlayerEliminated`.
- Later settlement results do nothing after elimination.
- Run both `PlayerRoster` EditMode tests, then run the complete EditMode suite.

The following step will add an independent `LeakResolver` that records escaped
enemy instance IDs, records joint-defense rescues, and emits one
`PlayerLeakResolved` event per player at settlement.

## Step 11 - Independent Leak Resolver

Status: Owner implementation completed. Owner-run EditMode suite passed.

Create `Assets/Game/Lua/Match/LeakResolver.lua.txt`. Keep it independent from
`Match.Session`, `PlayerRoster`, and scene objects.

Required public API:

```lua
start()
begin_wave(wave)
handle_event(event)
resolve_wave()
get_snapshot()
drain_events()
shutdown()
```

Required module-local state:

```lua
ordered_player_ids
current_wave
is_resolved
leaked_enemy_ids_by_player
leak_owner_by_enemy_id
rescued_enemy_ids
pending_events
```

Require `Config.Players` and build `ordered_player_ids` by collecting numeric
player IDs with `pairs`, then sorting them. Create one empty leak-ID array for
every configured player.

### Wave lifecycle

`start()` clears all module state and builds the stable player-ID list.

`begin_wave(wave)`:

1. Require a positive integer wave.
2. Reject starting a new wave if the current wave exists but has not been
   resolved.
3. Reject wave IDs that are not greater than the current wave.
4. Set `current_wave = wave` and `is_resolved = false`.
5. Clear `leak_owner_by_enemy_id` and `rescued_enemy_ids`.
6. Replace every player's leak-ID array with an empty array.
7. Do not clear `pending_events`; undrained results must never be silently
   discarded.

### Recording personal-defense leaks

Handle:

```lua
{
    type = "EnemyReachedEndpoint",
    target_player_id = 1,
    wave = 1,
    enemy_instance_id = 10,
}
```

Validate:

- A wave has begun and is not resolved.
- `event.wave == current_wave`.
- `target_player_id` identifies a configured player.
- `enemy_instance_id` is a positive integer.
- That enemy instance has not already been recorded as leaked.

Then:

```lua
table.insert(
    leaked_enemy_ids_by_player[event.target_player_id],
    event.enemy_instance_id
)

leak_owner_by_enemy_id[event.enemy_instance_id] =
    event.target_player_id
```

### Recording joint-defense rescues

Handle:

```lua
{
    type = "LeakedEnemyRescued",
    wave = 1,
    enemy_instance_id = 10,
}
```

Validate:

- The event belongs to the current unresolved wave.
- The enemy was previously recorded in `leak_owner_by_enemy_id`.
- The enemy has not already been rescued.

Then:

```lua
rescued_enemy_ids[event.enemy_instance_id] = true
```

The future joint-defense combat system will create `LeakedEnemyRescued`.
`LeakResolver` does not decide who attacks or how the enemy is defeated.

### Snapshot

`get_snapshot()` returns copied summary records in stable player-ID order:

```lua
{
    wave = 1,
    is_resolved = false,
    players = {
        {
            player_id = 1,
            initial_leak_count = 5,
            rescued_count = 2,
            final_leak_count = 3,
        },
    },
}
```

Calculate a player's rescued count by iterating that player's leak-ID array
and checking `rescued_enemy_ids[enemy_instance_id]`. This guarantees that a
rescue can only reduce the original target player's final leak count.

### Resolving the wave

`resolve_wave()`:

1. Require an active wave.
2. If `is_resolved` is already true, return `false` without emitting events.
3. For every configured player in stable order, calculate the three counts
   and append one:

```lua
{
    type = "PlayerLeakResolved",
    player_id = player_id,
    wave = current_wave,
    initial_leak_count = initial_leak_count,
    rescued_count = rescued_count,
    final_leak_count = final_leak_count,
}
```

4. Emit a result even when all three counts are `0`. This gives Session one
   deterministic settlement result per configured player.
5. Set `is_resolved = true` and return `true`.

`drain_events()` follows the existing consume-once queue pattern.
`shutdown()` clears all state.

Do not connect `LeakResolver` to `Session` yet. The next integration step will
propagate `target_player_id` through enemy creation, route endpoint events into
the resolver, request single-player leak resolution, and feed the resulting
events into `PlayerRoster`.

Expected owner-run validation:

- Five endpoint events for player one create five initial leaks.
- Rescuing enemy instances `2` and `5` produces two rescues and three final
  leaks.
- Mutating a returned snapshot cannot alter authoritative counts.
- `resolve_wave()` emits one `PlayerLeakResolved` event with counts `5/2/3`.
- A second `resolve_wave()` emits nothing.
- Beginning wave two clears wave-one counts.
- Run `LeakResolver_RecordsRescuesAndEmitsResolvedResultsOnce`, then run the
  complete EditMode suite.

## Step 12 - Single-Player Leak Settlement Integration

Status: Owner implementation completed and owner-run EditMode suite passed.
Static review completed. The C# enemy-snapshot bridge now also preserves
`target_player_id`.

This step connects the independent enemy, leak-resolution, player-health, and
phase modules into one authoritative single-player event chain:

```text
WaveSpawner
    -> EnemySpawnRequested
Session assigns target_player_id
    -> EnemyRoster
    -> EnemyReachedEndpoint
    -> LeakResolver
    -> PlayerLeakResolved
    -> PlayerRoster
    -> PlayerDamaged
    -> Flow enters Settlement
```

There is no joint-defense combat in this step. Single-player resolution simply
uses `rescued_count = 0`. The resolver remains in the chain so multiplayer
joint defense can later insert rescue events without changing `PlayerRoster`.

### Ownership of `target_player_id`

Do not put `target_player_id` in `Config.Waves` or make `WaveSpawner` choose a
player. `WaveSpawner` owns only what enemy appears and when it appears.
`Session` owns which player's defense area receives that generic spawn
request.

For the current single-player slice, add this explicit temporary assignment in
`Match.Session`:

```lua
local single_player_id = 1
```

When routing an `EnemySpawnRequested` event, set:

```lua
event.target_player_id = single_player_id
```

Set it before sending the event to `EnemyRoster` and before publishing it.
Future multiplayer work can replace this one assignment rule with per-player
lane routing while leaving `WaveSpawner` and `EnemyRoster` contracts intact.

### EnemyRoster changes

In `Match.EnemyRoster`, an `EnemySpawnRequested` event now requires a positive
integer `target_player_id`.

Store it in the authoritative enemy record:

```lua
local enemy = {
    -- existing fields
    target_player_id = event.target_player_id,
}
```

Propagate the stored value into:

1. `EnemyCreated`
2. copied enemy snapshots
3. `EnemyReachedEndpoint`

Always emit `enemy.target_player_id` from the stored record when the enemy
reaches the endpoint. Do not try to recover the player ID from the spawner,
current phase, or a global variable at that later time.

### Session module ownership

Require the two newly integrated modules:

```lua
local leak_resolver = require("Match.LeakResolver")
local player_roster = require("Match.PlayerRoster")
```

`Session.start()` must start both modules before match events can reach them.
One valid initialization order is:

```text
PlayerRoster -> LeakResolver -> EnemyRoster -> WaveSpawner -> Flow
```

When `Session` receives `PhaseChanged` with `phase == "Battle"`, call:

```lua
leak_resolver.begin_wave(event.wave)
```

Call it exactly once for each battle phase. Do not begin a leak-resolution
wave during `Preparation` or `Settlement`.

### Route EnemyRoster events

Replace direct calls that only publish `enemy_roster.drain_events()` with a
dedicated routing function:

```lua
local function route_enemy_events()
    local events = enemy_roster.drain_events()

    for _, event in ipairs(events) do
        leak_resolver.handle_event(event)
    end

    publish(events)
end
```

`LeakResolver.handle_event` ignores events other than the two leak-related
types, so routing `EnemyCreated` through it is valid. This keeps Session from
duplicating the resolver's event-filtering rules.

Use `route_enemy_events()`:

1. after `enemy_roster.update(delta_time)`;
2. after each event sent from `WaveSpawner` into `EnemyRoster`.

This preserves the existing order where an `EnemySpawnRequested` event is
published immediately before its resulting `EnemyCreated` event.

### Resolve before entering Settlement

Add one helper that resolves leaks and routes the result:

```lua
local function resolve_single_player_leaks()
    if not leak_resolver.resolve_wave() then
        return
    end

    local events = leak_resolver.drain_events()

    for _, event in ipairs(events) do
        player_roster.handle_event(event)
    end

    publish(events)
    publish(player_roster.drain_events())
end
```

In `try_complete_battle()`, retain the existing checks:

```text
wave spawning is complete
and alive enemy count is zero
```

After both conditions pass, use this order:

```lua
resolve_single_player_leaks()

if flow.complete_battle() then
    route_flow_events()
end
```

The order is important. `PlayerLeakResolved` and `PlayerDamaged` describe the
result of the battle that just ended, so they must be produced before the
externally visible `PhaseChanged/Settlement` event.

### Shutdown

`Session.shutdown()` must also shut down `LeakResolver` and `PlayerRoster`.
Keep clearing Session's own flags and event queue as before.

### Expected first-wave result

The temporary first wave contains three enemies and there are no towers yet,
so all three enemies reach player one's endpoint:

```text
initial leaks = 3
rescued       = 0
final leaks   = 3
health        = 10 - 3 = 7
```

After the initial `Preparation` event has already been drained, the first-wave
Session event batch contains 14 events. Its final six events must be:

```text
9  EnemyReachedEndpoint, enemy 1, target player 1
10 EnemyReachedEndpoint, enemy 2, target player 1
11 EnemyReachedEndpoint, enemy 3, target player 1
12 PlayerLeakResolved, counts 3/0/3
13 PlayerDamaged, damage 3, health 7
14 PhaseChanged, Settlement
```

## Step 48 - EditMode Test Alignment for the 11x7 Authority Board

Status: In progress.

After the authority board migrated to the current 11x7 route layout and the
wave pacing returned to the latest enemy config, a group of older EditMode
tests no longer matched runtime reality. The main drift points are:

- several tests still assumed the older `Crab.path_speed` and therefore used
  too-short battle windows before expecting endpoint or settlement results;
- some combat tests still deployed validation pieces onto cells that were valid
  in the earlier prototype, but no longer sit on or near the current route;
- defeat and settlement tests used fixed step budgets that were too small for
  the present round pacing.

The current repair direction is to keep the coverage, but realign those tests
to the current authority board instead of deleting them:

- use larger settlement budgets for session tests that intentionally wait for a
  full leak resolution;
- move attack/block validation pieces onto cells that are still meaningful on
  the current routes;
- update path-speed and progress assertions to match the live config.

### Owner-run validation

Run these EditMode tests first:

1. `EnemyRoster_CreatesDeterministicRecordsAndReturnsCopies`
2. `EnemyRoster_ReachingEndpointClampsAndEmitsSingleShotEvents`
3. `MatchSession_ResolvesSinglePlayerLeaksBeforeSettlement`
4. `MatchEvents_AreDrainedOnceAndMayBeReadInBatches`

Then run the complete EditMode suite. This step adds one test, so the expected
complete result after implementation is `22/22 Passed`.

## Step 13 - Independent Enemy Damage and Defeat

Status: Owner implementation completed, passed static review, and passed the
owner-run EditMode suite.

This step gives `Match.EnemyRoster` the authoritative rule for damaging and
defeating an enemy. It remains independent from `Match.Session` and from the
future chess-piece module.

The future combat system will decide:

```text
which piece attacks
which enemy it targets
when the attack occurs
how much configured damage it requests
```

`EnemyRoster` decides:

```text
whether the target enemy exists and is still alive
how much damage can actually be applied
whether the enemy becomes defeated
which authoritative result events are emitted
```

Keeping these responsibilities separate prevents a piece from directly
editing an enemy table. It also gives local single player and the future
authoritative server one shared damage-validation path.

### New input event

Extend `EnemyRoster.handle_event(event)` to handle:

```lua
{
    type = "EnemyDamageRequested",
    wave = 1,
    enemy_instance_id = 1,
    source_piece_instance_id = 7,
    damage = 4,
}
```

Field meanings:

- `wave`: wave in which the attack occurred.
- `enemy_instance_id`: authoritative target enemy.
- `source_piece_instance_id`: authoritative attacking piece. The piece module
  does not exist yet, so the independent test supplies a synthetic ID.
- `damage`: requested damage before remaining-health clamping.

For events other than `EnemySpawnRequested` and `EnemyDamageRequested`,
`EnemyRoster.handle_event` must continue to return without doing anything.

### Validation

Create or reuse a local helper that requires a positive integer. For an
`EnemyDamageRequested` event, validate:

1. `enemy_instance_id` is a positive integer.
2. `source_piece_instance_id` is a positive integer.
3. `damage` is a positive integer.
4. The target enemy exists.
5. `event.wave == enemy.wave`.

Validation failures should raise an error because they indicate an invalid
authoritative command or an internal routing bug.

After validation, if `enemy.status ~= "Alive"`, return without emitting
anything. Multiple pieces may select the same target during one simulation
step; after the first attack defeats it, later already-created requests are
stale rather than fatal.

### Applying damage

Never allow health below zero. Calculate:

```lua
local actual_damage = math.min(event.damage, enemy.health)
enemy.health = enemy.health - actual_damage
```

Then append:

```lua
{
    type = "EnemyDamaged",
    wave = enemy.wave,
    enemy_id = enemy.enemy_id,
    enemy_instance_id = enemy.instance_id,
    target_player_id = enemy.target_player_id,
    source_piece_instance_id = event.source_piece_instance_id,
    damage = actual_damage,
    health = enemy.health,
}
```

The distinction between requested and actual damage matters. If an enemy has
six health and receives a request for ten damage, the emitted `damage` is six.

### Defeating an enemy

If health becomes zero:

1. Set `enemy.status = "Defeated"`.
2. Reduce `alive_count` exactly once.
3. Append `EnemyDefeated` after `EnemyDamaged`:

```lua
{
    type = "EnemyDefeated",
    wave = enemy.wave,
    enemy_id = enemy.enemy_id,
    enemy_instance_id = enemy.instance_id,
    target_player_id = enemy.target_player_id,
    source_piece_instance_id = event.source_piece_instance_id,
    health = enemy.health,
}
```

Do not remove the enemy record. Snapshots and presentation code still need its
final state to play a defeat animation and support later match inspection.

`EnemyRoster.update(delta_time)` already moves only enemies whose status is
`"Alive"`. Therefore, a defeated enemy remains at its last path progress and
can never emit `EnemyReachedEndpoint`.

### Important event ordering

A lethal request must emit:

```text
EnemyDamaged
EnemyDefeated
```

`EnemyDamaged` comes first because it describes the health transition to zero;
`EnemyDefeated` then describes the resulting state transition.

### Do not integrate with Session yet

Do not make `Session` generate fake attacks and do not add automatic damage.
The next step will create an independent fixed-grid piece/combat module that
generates real `EnemyDamageRequested` events.

This step changes only:

```text
Assets/Game/Lua/Match/EnemyRoster.lua.txt
```

### Expected owner-run validation

The contract test performs this sequence:

1. Create one Crab with ten health.
2. Request four damage from synthetic piece instance `7`.
3. Verify six health remains and one `EnemyDamaged` event reports damage four.
4. Request ten damage from the same piece.
5. Verify actual damage is clamped to six.
6. Verify status becomes `Defeated` and `alive_count` becomes zero.
7. Verify `EnemyDamaged` is emitted before `EnemyDefeated`.
8. Send another damage request after defeat and update movement by ten
   seconds.
9. Verify no new events appear, health stays zero, and path progress does not
   change.

Run:

1. `EnemyRoster_DamageDefeatsAndPreventsEndpoint`
2. all `EnemyRoster` tests
3. the complete EditMode suite

This step adds one test, so the expected complete result after implementation
is `23/23 Passed`.

## Step 14 - Independent Fixed-Grid Piece Roster

Status: Owner implementation completed and passed static review. The contract
test's expected exception type was corrected from `System.Exception` to
`XLua.LuaException`; owner rerun pending.

Before pieces can attack, the authority needs stable answers to these
questions:

```text
Which piece instances exist?
Which player owns each piece?
Is a piece on the bench or on the board?
Which fixed deployment cell does it occupy?
Is the requested destination cell already occupied?
```

This step creates those rules without targeting enemies or producing damage.
The following step will build an attack planner that reads piece and enemy
snapshots and produces `EnemyDamageRequested`.

### Piece configuration

Create `Assets/Game/Lua/Config/Pieces.lua.txt`:

```lua
return {
    Sprout = {
        damage = 4,
        attack_interval_seconds = 1.0,
    },
}
```

These are temporary learning values. `PieceRoster` copies them into a piece
instance when that piece is granted, matching the existing rule where
`EnemyRoster` copies enemy configuration into each spawned enemy. A later hot
update therefore cannot silently change an already-running match.

### PieceRoster module

Create `Assets/Game/Lua/Match/PieceRoster.lua.txt`.

Required public API:

```lua
start()
grant_piece(player_id, piece_id)
handle_event(event)
get_snapshot()
drain_events()
shutdown()
```

Required module-local state:

```lua
next_instance_id
pieces_by_id
ordered_piece_ids
occupied_cells_by_player
pending_events
```

Require both:

```lua
local piece_configs = require("Config.Pieces")
local player_configs = require("Config.Players")
```

`start()` resets all state and creates one empty occupancy table for every
configured player:

```lua
occupied_cells_by_player[player_id] = {}
```

Occupancy is separated by player because every player owns an independent
defense board. Player one and player two may both occupy cell `101` on their
own boards.

### Granting an authoritative piece

`grant_piece(player_id, piece_id)` is a trusted authority API, not a client
command. During M2 the test calls it directly. Later, the economy/shop module
will call it only after validating and charging a purchase.

Validate:

1. `player_id` is a positive integer and identifies a configured player.
2. `piece_id` identifies a configured piece.
3. Configured `damage` is a positive integer.
4. Configured `attack_interval_seconds` is a positive number.

Create the piece on the bench:

```lua
local piece = {
    instance_id = next_instance_id,
    piece_id = piece_id,
    owner_player_id = player_id,
    level = 1,
    location = "Bench",
    cell_id = nil,
    damage = config.damage,
    attack_interval_seconds = config.attack_interval_seconds,
}
```

Store it in `pieces_by_id`, append its ID to `ordered_piece_ids`, increment
`next_instance_id`, and append:

```lua
{
    type = "PieceGranted",
    player_id = piece.owner_player_id,
    piece_id = piece.piece_id,
    piece_instance_id = piece.instance_id,
}
```

Return the new `piece.instance_id`. Piece instance IDs are unique across the
whole match, not merely within one player's board.

### Fixed-grid deployment command

`handle_event(event)` ignores every event except:

```lua
{
    type = "PieceDeployRequested",
    player_id = 1,
    piece_instance_id = 1,
    cell_id = 101,
}
```

The command uses a fixed `cell_id`, never a Unity world position.

Validate these rules before changing any state:

1. All three IDs are positive integers.
2. `player_id` identifies a configured player.
3. `piece_instance_id` identifies an existing piece.
4. The requesting player owns that piece.
5. The requested cell is not occupied by another piece belonging to that
   player.

If the piece is already in the requested cell, return without emitting an
event.

Important: check the destination cell before clearing the piece's current
cell. If a move request is invalid, the piece must remain in its original
location.

For a valid deployment or board-to-board move:

```lua
local previous_cell_id = piece.cell_id

if previous_cell_id ~= nil then
    occupied_cells_by_player[player_id][previous_cell_id] = nil
end

occupied_cells_by_player[player_id][event.cell_id] = piece.instance_id
piece.location = "Board"
piece.cell_id = event.cell_id
```

Then append:

```lua
{
    type = "PieceDeployed",
    player_id = piece.owner_player_id,
    piece_id = piece.piece_id,
    piece_instance_id = piece.instance_id,
    cell_id = piece.cell_id,
    previous_cell_id = previous_cell_id,
}
```

For a bench-to-board deployment, `previous_cell_id` is `nil`, so Lua naturally
omits that key from the event table.

Do not add world coordinates, drag state, GameObjects, attack timers, target
selection, selling, merging, or returning a piece to the bench yet.

### Snapshot

`get_snapshot()` returns copied piece records in stable instance-ID order:

```lua
{
    pieces = {
        {
            instance_id = 1,
            piece_id = "Sprout",
            owner_player_id = 1,
            level = 1,
            location = "Board",
            cell_id = 101,
            damage = 4,
            attack_interval_seconds = 1.0,
        },
    },
}
```

Do not expose `pieces_by_id` or `occupied_cells_by_player` directly. Mutating
a returned snapshot must not change authoritative state.

`drain_events()` uses the existing consume-once queue pattern. `shutdown()`
resets all state.

Do not connect `PieceRoster` to `Session` yet.

### Expected owner-run validation

The contract test:

1. Grants two `Sprout` pieces to player one and verifies IDs `1` and `2`.
2. Verifies both pieces begin on the bench and emit `PieceGranted`.
3. Deploys piece one to cell `101`.
4. Attempts to deploy piece two into occupied cell `101` and expects an error.
5. Verifies the failed command changed no state.
6. Deploys piece two to `102`.
7. Moves piece one from `101` to `103`, freeing its old cell.
8. Verifies the three successful `PieceDeployed` events and their
   `previous_cell_id` values.
9. Verifies mutating a returned snapshot cannot change authoritative cells.

Run:

1. `PieceRoster_GrantsAndDeploysToFixedCells`
2. the complete EditMode suite

This step adds one test, so the expected complete result after implementation
is `24/24 Passed`.

## Step 15 - Independent Deterministic Piece Attack Planner

Status: Next owner implementation task. Contract test is ready.

This step creates the rule that decides when a deployed piece attacks and
which enemy it targets. The planner does not own piece state, enemy health, or
enemy movement:

```text
PieceRoster snapshot + EnemyRoster snapshot
                    |
                    v
           PieceAttackPlanner
                    |
                    v
          EnemyDamageRequested events
```

The planner reads copied snapshots and produces commands. It must never modify
or retain those snapshot tables. The later Session integration step will drain
the commands, send them into `EnemyRoster`, and publish the resulting damage
and defeat events.

### Temporary M2 targeting rule

There is no attack range or board geometry yet. During this first combat
slice, every deployed piece may attack every alive enemy in its owner's
defense area.

Choose one target using this deterministic priority:

1. Enemy must have `status == "Alive"`.
2. Enemy must belong to the active wave.
3. `enemy.target_player_id == piece.owner_player_id`.
4. Prefer the enemy with the greatest `path_progress`.
5. If progress is equal, prefer the smaller `enemy.instance_id`.

The target closest to the endpoint is therefore attacked first. The explicit
tie-break rule ensures every authority simulation makes the same choice.

Bench pieces never attack.

### PieceAttackPlanner module

Create `Assets/Game/Lua/Match/PieceAttackPlanner.lua.txt`.

Required public API:

```lua
start()
begin_battle(wave)
end_battle()
update(delta_time, piece_snapshot, enemy_snapshot)
drain_events()
shutdown()
```

Required module-local state:

```lua
current_wave
is_active
seconds_until_attack_by_piece_id
pending_events
```

`start()` and `shutdown()` reset all state.

### Battle lifecycle

`begin_battle(wave)`:

1. Require `wave` to be a positive integer.
2. Reject beginning another battle while already active.
3. Reject a wave that is not greater than `current_wave`.
4. Set `current_wave = wave` and `is_active = true`.
5. Clear `seconds_until_attack_by_piece_id`.

Clearing timers makes every already-deployed piece ready to attack when the
new battle begins.

`end_battle()` sets `is_active = false` and clears all attack timers. It does
not reset `current_wave`.

### Update input

`update(delta_time, piece_snapshot, enemy_snapshot)` receives the plain copied
tables returned by:

```lua
piece_roster.get_snapshot()
enemy_roster.get_snapshot()
```

If the planner is inactive, return immediately.

Validate that `delta_time` is a non-negative number. The current authority
will normally pass the fixed simulation step of `0.1`, but the independent
test uses larger values to inspect cooldown behavior.

Iterate `piece_snapshot.pieces` with `ipairs`, preserving the stable piece
instance order supplied by `PieceRoster`.

### Cooldown update

For a piece whose `location == "Board"`:

```lua
local seconds_until_attack =
    seconds_until_attack_by_piece_id[piece.instance_id] or 0

seconds_until_attack = seconds_until_attack - delta_time
```

If the result is greater than zero, store it and continue.

If it is zero or below, search for a target using the deterministic rule
above.

When a target exists, append:

```lua
{
    type = "EnemyDamageRequested",
    wave = current_wave,
    enemy_instance_id = target.instance_id,
    source_piece_instance_id = piece.instance_id,
    damage = piece.damage,
}
```

Then carry excess time into the next cooldown:

```lua
seconds_until_attack =
    seconds_until_attack + piece.attack_interval_seconds
```

This follows the same timer-overflow principle already used by the phase flow
and wave spawner. With a one-second interval, an attack emitted after a
`0.1`-second update leaves `0.9` seconds until the next attack.

Emit at most one attack per piece during one planner update. Multiple pieces
may choose the same target from the same snapshot. If an earlier request
defeats that enemy, `EnemyRoster` already ignores the later stale request.

When no valid target exists, store a cooldown of `0`. The piece remains ready
and attacks on the first update after a target appears.

For a piece not on the board, remove its stored cooldown:

```lua
seconds_until_attack_by_piece_id[piece.instance_id] = nil
```

### Target helper

A local target-selection helper may look conceptually like:

```lua
local function find_target(piece, enemies)
    local selected = nil

    for _, enemy in ipairs(enemies) do
        if enemy.status == "Alive"
            and enemy.wave == current_wave
            and enemy.target_player_id == piece.owner_player_id then
            -- Select greater path_progress, then smaller instance_id.
        end
    end

    return selected
end
```

Do not use `pairs` when selecting or updating entities whose order can affect
events. Do not use random targeting.

### Event queue

`drain_events()` follows the existing consume-once queue pattern.

Do not connect the planner to `Session` yet. Do not directly call
`EnemyRoster.handle_event` from the planner. The planner should know only the
snapshot and event contracts.

### Expected owner-run validation

The contract test:

1. Creates two Sprout pieces but deploys only piece `1`.
2. Creates three enemies for player one.
3. Advances enemy `1` farther along the path while enemies `2` and `3` remain
   tied at zero progress.
4. Begins battle wave one.
5. Verifies the first update emits exactly one request from deployed piece
   `1`, targeting the most advanced enemy `1`.
6. Verifies the bench piece emits nothing.
7. Advances only half the remaining cooldown and verifies no attack.
8. Defeats enemy `1` through `EnemyRoster`.
9. Completes the cooldown and verifies the planner retargets enemy `2`,
   selecting the lower instance ID from the tied enemies `2` and `3`.
10. Ends the battle and verifies updates no longer emit attacks.

Run:

1. `PieceAttackPlanner_UsesBoardPiecesCooldownsAndDeterministicTargets`
2. `PieceRoster_GrantsAndDeploysToFixedCells`
3. the complete EditMode suite

This step adds one test, so the expected complete result after implementation
is `25/25 Passed`.

## Step 16 - Session Piece Combat Integration

Status: Implemented and passed owner-run validation.

`Match.Session` now owns `PieceRoster` and `PieceAttackPlanner` lifecycle,
routes piece events, and sends attack-planner commands through `EnemyRoster`.
The fixed-tick order is:

```text
route pending piece events
move existing enemies and route their events
update phase flow
spawn and create new enemies
plan piece attacks from the latest piece/enemy snapshots
apply damage commands and route damage/defeat events
try to complete battle
```

Important consequences:

- A newly spawned enemy may be attacked during its creation Tick.
- Enemy movement happens before target selection for that Tick.
- The planner cannot directly mutate enemy health.
- Session remains the only drain owner for internal gameplay event queues.

The Lua entry module and C# `LuaRuntime` now expose:

```text
GrantPiece(playerId, pieceId)
DeployPiece(playerId, pieceInstanceId, cellId)
GetPieceRosterSnapshot()
```

This provides the stable command and snapshot bridge required by the first
Unity presentation layer.

Run:

1. `PieceAttackPlanner_UsesBoardPiecesCooldownsAndDeterministicTargets`
2. `MatchSession_DeployedPieceAttacksSpawnedEnemy`
3. the complete EditMode suite

Step 15 and Step 16 add two tests after Step 14, so the expected complete
result is `26/26 Passed`.

## Step 17 - First Unity Presentation Layer

Status: Implemented and passed owner-run validation.

At this historical step, `MatchDemoPresentation` was automatically attached
by `LuaBootstrap` during the Demo stage. Step 29 replaces that temporary
composition with explicit scene components. The placeholder graphics still
allow the combat chain to be observed before final art and prefab work begins.

Implemented presentation:

- Gray enemy path and green endpoint tree
- Three blue fixed deployment cells
- One temporary green `Sprout`, automatically granted until the shop exists
- Number keys `1`, `2`, and `3` submit deployment commands for cells
  `101`, `102`, and `103`
- Red enemy views move from authoritative enemy snapshots
- Enemy health bars and yellow hit flashes
- Wave, phase, and player-health display
- Runtime restart detection so Stop/Start Lua does not leave stale views

The important authority boundary remains unchanged:

- Unity input submits only a player ID, piece instance ID, and fixed cell ID.
- Lua `Session` and `PieceRoster` validate and mutate deployment state.
- The presentation reads snapshots and never mutates enemy health, movement,
  or piece ownership.
- Hit flashes compare consecutive snapshots instead of draining gameplay
  events, leaving the event stream available to future systems.

This step also exposes the existing Lua `PlayerRoster` snapshot through
`Bootstrap.Main` and `LuaRuntime.GetPlayerRosterSnapshot()`.

### Owner-run validation

1. Wait for Unity to finish importing and confirm the Console has no compile
   errors.
2. In Test Runner, run:
   - `PieceRoster_GrantsAndDeploysToFixedCells`
   - `PieceAttackPlanner_UsesBoardPiecesCooldownsAndDeterministicTargets`
   - `MatchSession_DeployedPieceAttacksSpawnedEnemy`
   - the complete EditMode suite, expected `26/26 Passed`
3. Open `Assets/Scenes/SampleScene.unity` and enter Play Mode.
4. During preparation, confirm the green piece appears on the first blue cell.
5. Press `2` and `3`; confirm the piece moves between fixed cells.
6. During battle, confirm red enemies move along the path, lose health, flash
   yellow when hit, and disappear when defeated.
7. Allow a later enemy to leak if one survives; after settlement, confirm the
   displayed player HP decreases.
8. While playing, use `Stop Lua` and then `Start Lua` from the `LuaBootstrap`
   component context menu; confirm stale enemies disappear and a fresh match
   starts.

## Step 18 - Terrain, Blocking, and Piece Survival

Status: Implemented and passed owner-run validation.

### Implemented authority model

- `Config.Board` defines route IDs and fixed cells.
- Cells `101` and `102` are ground cells on route one.
- Cell `103` is high ground.
- Cell `104` is an obstacle and rejects deployment.
- Wave spawn plans carry a route ID, allowing later waves to select between
  multiple configured routes.
- `BlockResolver` deterministically assigns enemies to active ground pieces.
- A piece cannot block more enemies than `max_block_count`.
- An enemy that passes a full blocker is not pulled backward later.
- Blocked enemies remain alive but stop advancing.
- Current melee enemies attack only the piece blocking them.
- High-ground pieces cannot block and therefore cannot be attacked by current
  melee enemies.

`PieceRoster` now owns piece health and survival state:

- `Active`: may attack and block.
- `Downed`: remains deployed and occupies its cell, but cannot attack or block.
- A downed piece recovers after its configured timer.
- A downed piece also recovers when the complete combat stage ends.

The Session treats both `Battle` and the future `JointDefense` phase as one
continuous combat lifecycle. Entering joint defense therefore will not recover
pieces that fell during personal defense. This explicitly preserves the
required joint-defense state behavior.

Important current simplifications:

- The Demo has one visual route, though route IDs support additional routes.
- `Sprout` may deploy on ground or high ground and currently uses the existing
  global deterministic target selection. Attack range and melee/ranged piece
  categories remain future work.
- Enemy `attack_type` already distinguishes `Melee` and reserves extension
  space for future ranged enemies; only melee behavior is implemented.
- Recovery cooldown continues when entering future joint defense. Resetting
  that cooldown is intentionally unspecified.

### Presentation

- Brown cells on the path are ground blocker cells.
- The blue cell is high ground.
- The gray cell is an obstacle.
- Blocked enemies turn orange.
- Pieces now display health bars, flash when damaged, and turn gray while
  downed.
- The HUD displays piece terrain, current/max block count, and recovery timer.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Run the complete EditMode suite, expected `28/28 Passed`.
3. Run `BlockResolver_GroundPieceBlocksToCapacityAndEnemyAttacksIt`.
4. Run `PieceRoster_DownedPieceRecoversByTimerAndBattleEnd`.
5. Enter Play Mode with the piece on ground cell `101`.
6. Confirm the first enemy stops and turns orange at the piece while another
   enemy may pass when the piece's block count is full.
7. Confirm both the enemy and piece health bars decrease during combat.
8. Press `3` to move the piece to high ground; confirm later enemies no longer
   stop at that piece and current melee enemies do not damage it.

## Step 19 - Facing, Grid Attack Range, and Starter Shop

Status: Implemented and passed owner-run validation.

### Facing and attack range

- Every piece stores one of `Up`, `Right`, `Down`, or `Left`.
- Piece attack ranges are configured as relative grid offsets using
  `forward` and `right` coordinates.
- `Sprout` currently attacks its own cell and the first two cells in front.
- `BoardQueries` rotates the configured attack template according to facing
  and maps enemy route progress to deterministic logical route samples.
- Unity world positions and future art resources do not participate in target
  validation.
- Deployment position and facing commands are rejected outside
  `Preparation`.

The yellow marker on the placeholder piece shows its facing. During
Preparation, number keys change deployment cells and arrow keys change facing.

### Starter economy and shop

- Each player starts with configured gold.
- Buying a shop offer spends gold and grants the piece to the bench.
- Refreshing the shop spends its configured refresh cost.
- Surviving players receive configured gold when a round enters Settlement.
- Shop operations are rejected outside Preparation.
- The current Demo shop has three slots and uses deterministic pool rotation.
  This avoids unsynchronized random behavior and leaves a clear replacement
  point for a future server-owned seeded random pool.

The current shop intentionally does not yet implement:

- Shop level and rarity probabilities
- Upgrade cost
- Shared piece-pool depletion
- Selling and three-copy merging

### Presentation controls

- `Q`, `W`, `E`: buy the corresponding shop slot and select the new bench
  piece.
- `R`: refresh the shop.
- `1`, `2`, `3`: deploy the selected piece.
- Arrow keys: change selected-piece facing.

The HUD displays player gold and current shop offers.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Run the complete EditMode suite, expected `31/31 Passed`.
3. Run:
   - `PieceAttackPlanner_RespectsFacingAndConfiguredGridRange`
   - `MatchSession_DeploymentAndFacingCanOnlyChangeDuringPreparation`
   - `MatchSession_ShopPurchaseRefreshAndRoundRewardUseGold`
4. In Play Mode, rotate the piece away from the route and confirm it stops
   attacking enemies outside its configured front-facing range.
5. Confirm deployment and arrow-key facing controls stop responding in Battle.
6. During Preparation, buy and refresh shop offers and confirm displayed gold
   decreases.
7. Confirm round Settlement grants the displayed round reward.

## Step 20 - Bench Capacity and Deployment Population

Status: Implemented and passed owner-run validation.

This step begins the M3 auto-chess core loop without changing the temporary
Unity presentation layer.

### Implemented authority rules

- Each player has a configured bench capacity and starting deployment limit.
- Newly granted or purchased pieces enter the bench and consume one bench slot.
- A piece moving from the bench to the board consumes one deployment slot.
- Moving an already-deployed piece between board cells does not change counts.
- A piece may be returned from the board to the bench during Preparation.
- Returning a piece frees one deployment slot and consumes one bench slot.
- Purchase checks bench capacity before spending gold or marking an offer sold.
- Capacity state is included in copied Lua and C# piece-roster snapshots.

Current temporary values:

- Bench capacity: `8`
- Starting deployment limit: `2`

The next shop-level step can increase `deployment_limit` without changing the
piece ownership model introduced here.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Run:
   - `PieceRoster_EnforcesBenchAndDeploymentCapacity`
   - `MatchSession_FullBenchRejectsPurchaseWithoutSpendingGold`
   - `MatchSession_DeploymentAndFacingCanOnlyChangeDuringPreparation`
3. Run the complete EditMode suite, expected `33/33 Passed`.

No new temporary presentation controls were added. `BenchPiece` is currently
available through the C# runtime bridge for the future formal deployment UI.

## Step 21 - Shop Level and Deployment Limit Growth

Status: Implemented and passed owner-run validation.

The shop now has three deterministic levels:

| Level | Upgrade cost | Deployment limit |
| --- | ---: | ---: |
| 1 | 4 | 2 |
| 2 | 6 | 3 |
| 3 | Maximum | 4 |

Upgrade rules:

- Upgrades are accepted only during Preparation.
- The authority checks that another level exists before spending gold.
- A successful upgrade updates the shop level and the player's deployment
  limit in the same command.
- Upgrading never removes already-deployed pieces because limits only increase.
- Attempting to upgrade a maximum-level shop changes no gold or state.
- The copied shop snapshot exposes level, maximum level, upgrade availability,
  and current upgrade cost.

At this step the content still had only one piece definition. Multiple piece
rarities and deterministic rarity selection were added later in Step 24.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Run:
   - `MatchSession_ShopUpgradeIncreasesDeploymentLimit`
   - `MatchSession_DeploymentAndFacingCanOnlyChangeDuringPreparation`
   - `PieceRoster_EnforcesBenchAndDeploymentCapacity`
3. Run the complete EditMode suite, expected `34/34 Passed`.

No temporary presentation control was added for upgrading the shop.

## Step 22 - Sell Pieces

Status: Implemented and passed owner-run validation.

Selling is now an authoritative Preparation command:

- A player may sell only a piece they own.
- Bench and board pieces may both be sold.
- Selling a bench piece frees one bench slot.
- Selling a board piece frees its occupied cell and one deployment slot.
- Sold pieces are removed from snapshots and combat planning.
- Instance IDs are never reused after sale.
- `Sprout` currently sells for its configured value of `3`.
- A successful sale publishes `PieceSold`, followed by a separate
  `GoldGranted` event with reason `PieceSale`.
- Selling outside Preparation or selling a missing piece changes no gold.

`PieceSnapshot.SellValue` exposes the current sale value for the future formal
deployment UI.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Run:
   - `MatchSession_SellsBenchAndBoardPiecesForGold`
   - `MatchSession_DeploymentAndFacingCanOnlyChangeDuringPreparation`
   - `MatchSession_ShopPurchaseRefreshAndRoundRewardUseGold`
3. Run the complete EditMode suite, expected `35/35 Passed`.

No temporary presentation control was added for selling.

## Step 23 - Three-Copy Piece Merge

Status: Implemented and passed owner-run validation.

Pieces now merge automatically after a piece is granted or purchased:

- Three pieces with the same owner, piece ID, and level merge into one piece
  of the next level.
- The earliest instance ID survives, preserving stable deployment and network
  references.
- The other two instances are permanently removed and their IDs are never
  reused.
- Merges cascade when the upgraded piece completes another set of three.
- A surviving board piece keeps its cell; consumed board or bench pieces
  release their corresponding capacity.
- The granted or purchased command returns the final surviving instance ID.
- Maximum-level pieces do not merge further.

`Sprout` currently has explicit data-driven level values:

| Level | Health | Damage | Max block | Sell value |
| --- | ---: | ---: | ---: | ---: |
| 1 | 12 | 4 | 1 | 3 |
| 2 | 24 | 8 | 2 | 9 |
| 3 | 48 | 16 | 3 | 27 |

Each successful merge publishes one `PiecesMerged` event containing the
surviving instance ID, consumed instance IDs, previous level, and new level.

Current full-bench rules remain strict: a full bench rejects another grant or
purchase even if the incoming piece might have completed a merge.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Run:
   - `PieceRoster_MergesThreeMatchingPiecesAndCascades`
   - `PieceRoster_EnforcesBenchAndDeploymentCapacity`
   - `MatchSession_FullBenchRejectsPurchaseWithoutSpendingGold`
3. Run the complete EditMode suite, expected `36/36 Passed`.

No temporary presentation behavior was added for merge effects.

## Step 24 - Shop Rarity Probabilities

Status: Implemented and passed owner-run validation.

The shop now uses data-driven rarity weights at each shop level:

| Shop level | Rarity 1 | Rarity 2 | Rarity 3 |
| --- | ---: | ---: | ---: |
| 1 | 100% | 0% | 0% |
| 2 | 75% | 25% | 0% |
| 3 | 60% | 30% | 10% |

Rules and current placeholder content:

- `Sprout`, `Bramble`, and `Bloom` currently represent rarities 1, 2, and 3.
- Upgrading changes the weights used by future refreshes; it does not replace
  the offers already shown to the player.
- The Demo uses deterministic rolls derived from refresh and slot indexes.
  This preserves reproducible authority state and leaves a clear replacement
  point for future server-seeded randomness.
- Startup validates weights, rarity pools, piece references, and pool rarity
  consistency.
- Shop snapshots expose both the current rarity weights and each offer's
  rarity for the future formal shop UI.

`Bramble` and `Bloom` are gameplay-validation placeholders rather than final
content definitions.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Run:
   - `ShopRoster_ShopLevelChangesRarityWeightsOnLaterRefreshes`
   - `MatchSession_ShopUpgradeIncreasesDeploymentLimit`
   - `MatchSession_ShopPurchaseRefreshAndRoundRewardUseGold`
3. Run the complete EditMode suite, expected `37/37 Passed`.

No temporary presentation behavior was added for rarity probabilities.

## Step 25 - Board Synergies

Status: Implemented and passed owner-run validation.

The three placeholder pieces now have two data-driven synergies:

| Synergy | Required unique board pieces | Current effect |
| --- | ---: | ---: |
| `Nature` | 2 | `+2` damage to deployed Nature pieces |
| `Arcane` | 2 | `+3` damage to deployed Arcane pieces |

Current membership:

- `Sprout`: Nature and Arcane
- `Bramble`: Nature
- `Bloom`: Arcane

Authority rules:

- Only pieces currently deployed on the board contribute to synergies.
- A synergy counts unique piece IDs, so multiple copies of the same piece do
  not fill multiple required positions.
- Active bonuses affect only deployed pieces belonging to that synergy.
- Deploying, returning to bench, selling, granting, and merging all recompute
  the player's active synergies and final piece damage.
- `PieceSnapshot.BaseDamage` keeps the configured level damage separate from
  the final `Damage` value used by combat.
- `PieceRosterSnapshot.ActiveSynergies` exposes the active level, unique count,
  threshold, and damage bonus for the future formal roster UI.
- Activation changes publish `SynergiesChanged`.

The current synergy names and values are gameplay-validation placeholders.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Run:
   - `MatchSession_BoardCompositionActivatesAndRemovesSynergies`
   - `PieceRoster_MergesThreeMatchingPiecesAndCascades`
   - `PieceAttackPlanner_UsesBoardPiecesCooldownsAndDeterministicTargets`
3. Run the complete EditMode suite, expected `38/38 Passed`.

No temporary presentation behavior was added for synergies.

## Step 26 - Three Enemy Archetypes

Status: Implemented and passed owner-run validation.

The three normal waves now use distinct placeholder enemies:

| Wave | Enemy | Health | Path speed | Attack | Attack interval |
| --- | --- | ---: | ---: | ---: | ---: |
| 1 | `Crab` | 10 | 0.25 | 3 | 1.0s |
| 2 | `Skitter` | 6 | 0.40 | 2 | 0.7s |
| 3 | `Shellback` | 24 | 0.15 | 5 | 1.5s |

`Skitter` validates fast, fragile pressure while `Shellback` validates slow,
durable pressure. All three remain melee enemies so this content step does not
silently introduce unfinished ranged-enemy behavior.

`WaveSpawner` now validates its configured enemy reference when Battle begins.
`EnemyRoster` continues copying all combat values into each created enemy, so
later configuration hot updates cannot mutate enemies already in the match.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Run:
   - `WaveAndEnemyConfigs_CreateThreeDistinctEnemyArchetypes`
   - `MatchFlow_AdvancesThroughConfiguredPhases`
   - `MatchSession_BattlePhaseDrivesWaveSpawner`
3. Run the complete EditMode suite, expected `39/39 Passed`.

No temporary presentation behavior was added for the new enemy archetypes.

## Step 27 - Five Normal Waves and Boss Finish

Status: Implemented and passed owner-run validation.

The authoritative single-player flow is now:

```text
Preparation -> Battle -> Settlement
    (repeat for normal waves 1-5)
-> BossPreparation -> BossBattle -> End
```

Boss and result rules:

- Wave 6 spawns one placeholder boss, `AncientGuardian`.
- `BossPreparation` allows the same deployment, facing, shop, and sell commands
  as normal Preparation.
- The Boss remains a shared-state enemy marked by `EnemySnapshot.IsBoss`.
- Defeating the Boss ends the match with `MatchFlowSnapshot.Result = Victory`.
- If the Boss reaches the endpoint, the match ends with `Defeat`; it does not
  enter normal leak settlement.
- If every player is eliminated during a normal-wave settlement, the match
  immediately ends with `Defeat`.
- Normal-wave and Boss counts, plus phase durations, now live in
  `Config.MatchFlow`.

This is the single-player shared-Boss authority foundation. Multiplayer arena
switching, combining surviving players on one board, and synchronized Boss
presentation remain later networking work.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Run:
   - `MatchFlow_AdvancesThroughConfiguredPhases`
   - `MatchSession_AllPlayersEliminatedEndsMatchInDefeat`
   - `MatchSession_StrongBoardDefeatsBossAndEndsInVictory`
   - `EnemyRoster_BossResultDistinguishesVictoryAndDefeat`
3. Run the complete EditMode suite, expected `42/42 Passed`.

No formal Boss presentation or art was added.

## Step 28 - Player Ready and Early Preparation Completion

Status: Implemented and passed owner-run validation.

Players now have an authoritative ready state:

- `PlayerReadyRequested` is accepted only during `Preparation` or
  `BossPreparation`.
- When every surviving player is ready, the authority immediately advances to
  `Battle` or `BossBattle` without waiting for the remaining timer.
- Ready state resets when the next normal or Boss preparation begins.
- Eliminated players do not participate in the all-ready check.
- `PlayerSnapshot.IsReady` exposes the state for the future formal preparation
  UI and multiplayer synchronization.
- `PlayerReadyChanged` publishes each actual state change.

The current single-player session has one surviving player, so setting that
player ready immediately starts combat. The same command and all-alive check
can later be transported by the LAN authority without changing phase rules.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Run:
   - `MatchSession_AllAlivePlayersReadyEndsEachPreparationEarly`
   - `MatchFlow_AdvancesThroughConfiguredPhases`
   - `MatchSession_DeploymentAndFacingCanOnlyChangeDuringPreparation`
3. Run the complete EditMode suite, expected `43/43 Passed`.

The temporary keyboard adapter now maps `Space` to the ready command as part
of Step 29.

## Step 29 - Explicit Scene Presentation Boundary

Status: Implemented and passed owner-run validation.

The temporary presentation has been separated from Lua startup and explicitly
composed in `SampleScene`:

```text
LuaBootstrap                    Lua startup, fixed ticking, and disposal
├── Demo Setup                  temporary starting-piece grant and deployment
├── Keyboard Input              temporary keyboard-to-command adapter
└── Placeholder View            replaceable no-art diagnostic rendering
```

`MatchSceneController` is attached beside `LuaBootstrap`. Once per rendered
frame it reads the authoritative flow, enemy, piece, player, and shop
snapshots, then distributes the same snapshot set to the child scene features.

Responsibilities are now intentionally separate:

- `LuaBootstrap` no longer creates any presentation behavior.
- `MatchDemoSetup` contains the temporary starting `Sprout` setup and can be
  removed when formal match entry exists.
- `MatchKeyboardInput` owns temporary keyboard controls, including `Space` for
  ready. It submits commands only during normal or Boss preparation. The most
  recently purchased piece becomes selected.
- `MatchPlaceholderView` owns only the existing placeholder arena, entities,
  bench, selected-piece highlight, facing marker, health feedback, and
  diagnostic HUD.
- Future board, unit, UI, and effects GameObjects can replace the placeholder
  view one component at a time without changing Lua authority.

No art resources, formal UI, or prefab architecture were introduced in this
step.

Current note: this section records the earlier presentation-boundary step.
The `Demo Setup` child and `MatchDemoSetup` script described here were retired
in Step 47 after formal shop-driven match entry became available.

### Owner-run validation

1. Wait for Unity to import the new scripts and confirm the Console has no
   compile errors.
2. Open `Assets/Scenes/SampleScene.unity`. Confirm `LuaBootstrap` has a
   `Match Scene Controller` component and the hierarchy contains:
   `Demo Setup`, `Keyboard Input`, and `Placeholder View`.
3. Enter Play Mode. Confirm the placeholder arena appears and the initial
   `Sprout` is deployed.
4. Click the Game window so it owns keyboard focus. During `Preparation` and
   `BossPreparation`, buy a piece with `Q/W/E`. Confirm it appears on the
   bottom bench with a cyan selected-piece highlight.
5. Press an arrow key. Confirm the selected piece's yellow facing marker moves
   and the HUD `Facing` value changes.
6. Press `1`, `2`, and `3`. Confirm the selected piece moves respectively to
   fixed cells `101`, `102`, and `103`, as shown by the HUD `Cell` value.
7. Confirm `R` refreshes the shop and `Space` immediately begins battle.
8. During battle, confirm keyboard deployment is locked and snapshot-driven
   enemy movement, health bars, hit feedback, and piece state still update.
9. While playing, select the root `LuaBootstrap` GameObject. Open the
   three-dot menu at the upper-right of the `Lua Bootstrap (Script)` component
   header and choose `Stop Lua`. Open the same menu and choose `Start Lua`.
   Confirm dynamic views clear and a fresh match reconnects without adding
   duplicate scene components.
10. Run the complete EditMode suite, expected `43/43 Passed`.

## Step 30 - Complete Single-player Diagnostic Controls

Status: Implemented and passed owner-run validation.

The temporary keyboard adapter and placeholder view now expose the remaining
already-implemented single-player authority features. This makes the complete
loop operable before formal UI and art are introduced.

Additional preparation controls:

- `Tab` selects the next locally owned piece.
- `B` returns the selected board piece to the bench when space is available.
- `X` sells the selected piece and automatically selects another owned piece
  on the next frame.
- `U` upgrades the shop when the player can afford the next level.

Additional diagnostic presentation:

- Board population and deployment limit
- Bench population and capacity
- Shop level, upgrade cost, and maximum-level state
- Selected piece damage and sale value
- Active synergy levels and damage bonuses
- A larger purple placeholder for the Boss
- Remaining phase time and final Victory/Defeat result

The keyboard adapter still submits player-intent commands only. Lua remains
responsible for preparation-phase restrictions, costs, capacity, ownership,
selling, upgrades, synergies, battle results, and all other state changes.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Enter Play Mode and click the Game window for keyboard focus.
3. Buy several pieces. Confirm `Tab` cycles the cyan selection highlight and
   selected-piece HUD between locally owned pieces.
4. Deploy a selected piece, press `B`, and confirm it returns to the bottom
   bench while the board/bench counters update.
5. Press `X` and confirm the selected piece disappears, its sale value is
   added to Gold, and another piece becomes selected.
6. Accumulate enough Gold, press `U`, and confirm shop level and deployment
   limit increase.
7. Deploy different piece types and confirm active synergy information appears
   when its configured threshold is reached.
8. Continue through all normal waves. Confirm the Boss is displayed as a
   larger purple enemy and the end phase displays `MATCH Victory` or
   `MATCH Defeat`.
9. Run the complete EditMode suite, expected `43/43 Passed`.

## Step 31 - Connect the Formal Pseudo-3D Match Board

Status: Complete; owner-run validation passed.

The match scene now builds one formal pseudo-3D personal board from the
authority-owned `BoardSnapshot`:

- `BoardVisualLayoutConverter` translates authority cell IDs, presentation
  coordinates, terrain, heights, and visual keys into a read-only visual
  layout.
- `MatchBoardPresenter` builds static terrain once for each Lua runtime and
  provides collider-free cell picking and selection highlights.
- `BoardsRoot` contains the reusable `ObservedBoardView` structure:
  `StaticBoard`, `Highlights`, `Pieces`, `Enemies`, and `Effects`.
- Picking a cell only changes presentation and logs its authority cell ID.
  Deployment legality remains owned by Lua.
- The placeholder combat view remains active in parallel until formal piece
  and enemy presenters replace it.

The authority board configuration currently contains four sparse temporary
cells. The formal match scene deliberately renders only those cells instead of
copying the larger visual-prototype map into gameplay authority.

### Owner-run validation

1. Wait for Unity to import the new scripts and confirm the Console has no
   compile errors.
2. Open `Assets/Scenes/SampleScene.unity`. Confirm `BoardsRoot` has
   `Observed Board View` and `Match Board Presenter`, and contains
   `StaticBoard`, `Highlights`, `Pieces`, `Enemies`, and `Effects`.
3. Enter Play Mode. Confirm a sparse four-cell pseudo-3D board appears near the
   lower center while the existing placeholder arena and controls still work.
4. Click each visible pseudo-3D cell. Confirm the selected cell receives a cyan
   highlight and the Console logs its authority cell ID.
5. Click outside the pseudo-3D board. Confirm the highlight disappears.
6. Use the existing keyboard controls and begin battle. Confirm the placeholder
   combat presentation continues updating.
7. Stop and restart Lua from the `Lua Bootstrap (Script)` component menu.
   Confirm the formal static board is rebuilt without duplicate terrain or
   highlight objects.
8. Run the complete EditMode test suite.

## Step 32 - Establish the Complete 11x7 Authority Board

Status: Implemented; awaiting owner-run validation.

The temporary four-cell authority layout has been replaced by the full default
11x7 personal-defense board based on
`Assets/Game/Board/Prototype/DefaultSizeMap_11x7.asset`.

Authority contracts now include:

- Separate `terrain` and `zone` values for each cell.
- `Battlefield`, `Reserve`, `TemporaryReserve`, `Spawn`, and `Endpoint` zones.
- Eleven normal reserve cells at visual `y = 0`.
- Eleven temporary reserve cells at visual `y = 1`.
- Temporary-reserve capability flags for overflow placement and automatic sale
  when battle begins. The behavior itself is intentionally not active yet.
- Spawn cells at `(10, 3)` and `(10, 5)`, and the shared endpoint at `(0, 3)`.
- Route 1 directly along `y = 3`.
- Route 2 from `(10, 5)` to `(4, 5)`, down through `(4, 4)` to `(4, 3)`,
  then along the shared route section to `(0, 3)`.
- Route-specific progress on shared cells, allowing either route's enemy to be
  blocked correctly.
- Session-start board validation for map coverage, capacities, route
  traversability, adjacent route samples, Spawn starts, and Endpoint finishes.

Normal reserve capacity is now read from the board config and equals eleven.
Waves 2 and 4 currently use route 2 so both routes are exercised by the
existing single-route-per-wave spawner. Simultaneous multi-route spawn groups
remain a later wave-content upgrade.

### Owner-run validation

1. Wait for Unity to finish importing and confirm the Console has no compile
   errors.
2. Open `SampleScene` and enter Play Mode. Confirm the formal pseudo-3D board
   now contains the complete 11x7 layout rather than four sparse cells.
3. Confirm the nearest row contains eleven height-1 high-ground reserve cells,
   and the next row contains eleven height-1 obstacle temporary-reserve cells.
4. Click cells across the full board and confirm selection highlights still
   follow the projected cell tops without covering higher terrain incorrectly.
5. Confirm existing preparation controls still deploy to legacy diagnostic
   cells `101`, `102`, and `103`, while obstacle and reserve cells reject
   battle deployment.
6. Run the complete EditMode suite. The new shared-route blocking test and
   expanded board snapshot assertions should pass.

Current intentional limitations:

- Temporary-reserve overflow placement and battle-start automatic sale are
  recorded in the authority contract but not implemented.
- Formal enemy views do not yet render movement along the two route samples.
- The old placeholder combat presentation remains active.

## Step 33 - Expose Board Zones and Authority Routes

Status: Implemented; awaiting owner-run validation.

The formal board now makes authority regions and paths directly visible:

- Reserve cell tops are blue.
- TemporaryReserve cell tops are purple.
- Spawn cell tops are orange.
- The Endpoint cell top is green.
- Route 1 is cyan and Route 2 is pink.
- The two route lines are offset on their shared section so both remain
  visible.
- Clicking a cell logs its authority cell ID, terrain, visual coordinate, and
  zone.

The route view lives under the dedicated `BoardsRoot/Routes` scene node and is
rebuilt from `BoardSnapshot.Routes` together with the static terrain. It does
not perform pathfinding or gameplay validation.

The `MatchSession_DeployedPieceAttacksSpawnedEnemy` test was also updated for
the complete board's deterministic timing. During its observed battle window,
the deployed Sprout now attacks twice, leaving the first enemy at 2 health.

### Owner-run validation

1. Confirm Unity compiles without errors.
2. Enter Play Mode in `SampleScene`.
3. Confirm the nearest row is blue and the next row is purple.
4. Confirm two orange Spawn cells exist at `(10, 3)` and `(10, 5)`, and one
   green Endpoint cell exists at `(0, 3)`.
5. Confirm cyan and pink routes lead from their Spawn cells to the shared
   Endpoint, and both lines remain visible after joining.
6. Click cells from each zone and confirm the Console logs the expected `zone`.
7. Confirm cell highlighting still obeys higher-terrain occlusion.
8. Run the complete EditMode suite and confirm
   `MatchSession_DeployedPieceAttacksSpawnedEnemy` passes.

## Step 34 - Map Reserve Pieces onto the Formal Board

Status: Implemented; awaiting owner-run validation.

Normal reserve pieces now occupy authority board cells instead of relying on a
presentation-only list index:

- `Board.reserve_cell_ids` defines a stable left-to-right placement order.
- Granting a piece assigns the first free Reserve cell and includes that cell
  ID in the piece snapshot and `PieceGranted` event.
- Deploying, benching, selling, and merging all release or claim cells through
  the same per-player occupancy map.
- Returning a deployed piece to the bench assigns the first available Reserve
  cell.
- Board validation checks both ordered Reserve and TemporaryReserve cell lists.

The formal board now owns piece presentation through `BoardPieceView`. It reads
only authority snapshots, projects each piece onto its assigned cell, follows
facing and downed state, and displays only the currently observed player's
pieces. The temporary diagnostic overlay no longer creates a second set of
piece visuals.

This is still a fallback shape-based piece view. A later art-integration step
can replace its generated visual objects with prefabs without changing reserve
placement or gameplay authority.

### Owner-run validation

1. Wait for Unity to import `BoardPieceView.cs` and confirm the Console has no
   compile errors.
2. Run the complete EditMode suite.
3. Enter Play Mode in `SampleScene`. Confirm the starting piece appears on
   authority cell `101` and no duplicate piece appears in the old placeholder
   arena.
4. During Preparation, purchase pieces with `Q`, `W`, or `E`. Confirm
   non-merged purchases appear from left to right on the nearest blue Reserve
   row.
5. Select a Reserve piece with `Tab`, then deploy it using `1`, `2`, or `3`.
   Confirm it moves from its Reserve cell onto the selected battlefield cell.
6. Press `B` for a deployed selected piece. Confirm it returns to the first
   free blue Reserve cell.
7. Change facing with the arrow keys and confirm the yellow facing marker
   rotates on the formal board piece.

## Step 35 - Drag Pieces and Confirm Battlefield Facing

Status: Implemented; awaiting owner-run validation.

The formal board now supports preparation-phase mouse placement:

- Clicking a visible local piece selects it and starts dragging.
- While dragging, only legal, unoccupied target cells receive the placement
  highlight and piece preview.
- Local legality uses authority snapshot flags, deployment capacity, occupied
  cells, and each piece's configured deployable terrain list.
- Dropping onto a normal Reserve cell commits immediately to that exact cell.
- Dropping onto a Battlefield cell enters facing confirmation without changing
  authority state.
- Clicking the preview piece again, dragging toward one of four directions,
  and releasing commits the target cell and facing in one authority command.
- Right-click or `Escape` cancels an unfinished drag or facing confirmation.
- Mouse placement is disabled outside `Preparation` and `BossPreparation`.
- Existing keyboard controls remain available for diagnostics.

`PiecePlaceRequested` is the new atomic placement intent. Lua validates the
target and facing before moving the piece. Reserve placement preserves the
piece's existing facing. The piece snapshot now exposes its deployable terrain
list so the client can avoid presenting targets that authority will reject.

### Owner-run validation

1. Wait for Unity to import the scripts and confirm the Console has no compile
   errors.
2. Run the complete EditMode suite, including
   `MatchSession_PlacePieceCommitsTargetAndFacingAtomically`.
3. Enter Play Mode in `SampleScene` and purchase at least one non-merged piece
   so it appears on the blue Reserve row.
4. Click and drag that piece across the board. Confirm legal empty cells show
   the cyan highlight and preview, while obstacles, Spawn, Endpoint,
   TemporaryReserve, occupied cells, and unsupported terrain do not.
5. Release over a legal Battlefield cell. Confirm the piece remains there as a
   preview but has not completed placement yet.
6. Click the preview piece, drag clearly up, down, left, or right, then release.
   Confirm placement completes and the yellow facing marker points in that
   direction.
7. Drag a deployed piece onto a chosen empty blue Reserve cell. Confirm it
   moves to that exact cell immediately without entering facing confirmation.
8. Start battle and confirm mouse dragging no longer moves pieces.

## Step 36 - Complete Temporary Reserve Rules

Status: Implemented; awaiting owner-run validation.

The purple TemporaryReserve row is now an authority-owned overflow area:

- Normal Reserve and TemporaryReserve occupancy use separate counts and
  capacities.
- `GrantOverflowPiece` is the explicit authority entry point for gifts,
  synergy-created pieces, special-round rewards, and similar extra sources.
- Overflow grants still use an available normal Reserve cell first. They enter
  TemporaryReserve only when the normal Reserve is full.
- Shop purchases continue to require normal Reserve space and never place
  purchased pieces into TemporaryReserve.
- TemporaryReserve pieces may be dragged directly onto a legal Battlefield
  cell, or moved into a normal Reserve cell after the player frees space.
- Players cannot deliberately move normal pieces into TemporaryReserve.
- When `Battle` or `BossBattle` begins, all pieces still in TemporaryReserve
  are automatically sold. Each sale emits `PieceSold` and grants its sell
  value with reason `TemporaryReserveAutoSell`.

The piece-capacity snapshot now exposes `TemporaryBenchCount` and
`TemporaryBenchCapacity`. The temporary placeholder HUD displays both normal
and temporary reserve occupancy.

### Owner-run validation

1. Wait for Unity to import and confirm the Console has no compile errors.
2. Run the complete EditMode suite, including:
   - `MatchSession_OverflowGrantUsesTemporaryReserveAndCanBeResolved`
   - `MatchSession_BattleStartAutoSellsTemporaryReservePieces`
3. Enter Play Mode in `SampleScene`. Existing shop purchases should continue
   to use only the blue normal Reserve row.
4. TemporaryReserve creation currently uses the authority/test-facing
   `GrantOverflowPiece` API because gifts and special-reward UI do not exist
   yet. Confirm the tests show its first overflow piece at cell `1012`.
5. Confirm a temporary piece can be dragged into a freed blue Reserve cell or
   deployed onto compatible Battlefield terrain.
6. Confirm beginning battle automatically removes any unresolved temporary
   piece and grants its sell value.

## Step 37 - Present Enemies On The Formal Dual-Route Board

Status: Implemented; awaiting owner-run validation.

Enemy presentation has moved from the old placeholder lane onto the formal
authority board:

- `BoardEnemyView` reads enemy snapshots without changing authority state.
- An alive enemy appears at the center of the first sample of its configured
  route, then interpolates between route-cell centers using `PathProgress`.
- Route 1 begins at `(10,3)` and travels directly to `(0,3)`.
- Route 2 begins at `(10,5)`, turns from `(4,5)` through `(4,4)` to `(4,3)`,
  then joins the shared route and reaches `(0,3)`.
- Enemy sorting changes with the current route cell so foreground terrain can
  correctly cover enemies behind it.
- Blocked enemies stop moving because the view follows the unchanged
  authority `PathProgress`; defeated and leaked enemies are hidden.
- Only enemies targeting the currently observed player are shown.
- The old placeholder view no longer creates a second enemy representation.

Each current wave configuration still selects one route. This demonstrates
both routes across configured waves while keeping the existing deterministic
wave-spawner contract. A later wave-format extension can schedule multiple
spawn groups on both routes during the same wave without changing
`BoardEnemyView`.

### Owner-run validation

1. Wait for Unity to import scripts and confirm the Console has no compile
   errors.
2. Run the complete EditMode suite, including
   `BoardSnapshot_ExposesAuthorityLayoutAndRoutes`.
3. Enter Play Mode in `SampleScene`, finish Preparation, and confirm wave 1
   enemies appear at the center of `(10,3)` and move along route 1.
4. Continue to wave 2 and confirm enemies appear at `(10,5)`, travel left to
   `(4,5)`, turn downward through `(4,4)` and `(4,3)`, then follow the shared
   route to `(0,3)`.
5. Confirm blocked enemies stop visually at the blocking piece, damaged
   enemies update their health bar, and defeated or leaked enemies disappear.
6. Confirm no second enemy appears on the old straight placeholder lane.

## Step 38 - Remove The Old Placeholder Arena

Status: Implemented; awaiting owner-run validation.

The formal board now owns all current board, route, piece, and enemy
presentation. The old straight placeholder arena has therefore been removed:

- `MatchPlaceholderView` has been replaced by `MatchDebugHud`.
- `MatchDebugHud` displays authority snapshot values only.
- It no longer creates the old straight path, goal, bench strip, four legacy
  cell blocks, generated square sprite, or runtime presentation root.
- It no longer modifies the scene camera.
- `SampleScene` keeps the same serialized script reference through the
  preserved `.meta` GUID, and its hierarchy object is now named `Debug HUD`.
- Temporary keyboard controls remain in `MatchKeyboardInput`; the starting
  demonstration piece remains isolated in `MatchDemoSetup`.

Current note: Step 47 later removed `MatchDemoSetup` completely and made both
keyboard diagnostics and this text HUD opt-in.

### Owner-run validation

1. Wait for Unity to import scripts and confirm the Console has no missing
   script or compile errors.
2. Open `SampleScene` and confirm the hierarchy contains `Debug HUD` instead
   of `Placeholder View`.
3. Enter Play Mode and confirm no `Placeholder View Runtime`, `EnemyPath`,
   `TreeGoal`, legacy `Bench`, or `Cell_101` through `Cell_104` objects are
   created.
4. Confirm the formal board, pieces, enemies, routes, shop UI, diagnostic
   text, mouse placement, and keyboard diagnostics still work.
5. Run the complete EditMode suite.

## Step 39 - Direct Per-Cell Board Visual Editing

Status: Implemented; awaiting owner-run validation.

`DefaultBoardVisual.asset` is now the single formal-board material editing
entry. Its custom Inspector provides a clickable 11x7 coordinate grid:

- Global terrain materials still apply to every Ground, HighGround, or
  Obstacle cell without a more specific override.
- Visual-key material entries still support shared appearances such as
  Reserve, TemporaryReserve, Spawn, and Endpoint.
- A per-cell override has the highest presentation priority.
- Top, front, left, and right surfaces can be overridden independently.
- Per-cell overrides live only in the Unity visual asset. They do not read or
  modify Lua terrain, zone, visual height, routes, deployment, or combat
  rules.
- Green grid buttons indicate cells with an existing visual override.

### Change the top material at `(1,4)`

1. Select `Assets/Game/Runtime/Board/DefaultBoardVisual.asset`.
2. In `Per-Cell Visual Overrides`, click the `1,4` grid button.
3. Click `Create Top Material Override`.
4. Drag the desired material into `Top Material`.
5. Leave all side override toggles disabled unless that cell also needs unique
   side materials.
6. Re-enter Play Mode so the static formal board is rebuilt.

Use `Remove Cell Override` to return the selected cell to shared material
resolution.

## Step 40 - Hide Route Debug Lines In Formal Play

Status: Implemented; awaiting owner-run validation.

The two colored route lines are diagnostic presentation and are now hidden by
default in `SampleScene`. `MatchBoardPresenter > Routes > Show Route Debug
Lines` may be enabled temporarily when checking route configuration.

Disabling the visual lines does not change Lua route definitions, enemy route
movement, blocking, attacks, spawning, or endpoint resolution.

## Step 41 - Make Authority Terrain Rows Match The Visible Board

Status: Implemented; awaiting owner-run validation.

`Config.Board` terrain rows are now written from back to front:

- The first text row represents the farthest visible board row (`y = 6`).
- The final text row represents the nearest Reserve row (`y = 0`).
- Runtime construction reverses the row index once while creating the 77
  authority cells, so the resulting terrain, routes, cell IDs, and gameplay
  behavior remain unchanged.

This one-time index conversion has negligible performance cost and makes manual
terrain editing match the board's visible orientation.

## Step 42 - Extend The Formal Board Front Edge

Status: Implemented; awaiting owner-run validation.

The formal board can now visually extend the front faces of the `y = 0` row
below their normal base. `MatchBoardPresenter > Projection > Front Edge Extra
Depth` controls the extra depth in height-step units and defaults to `1.5` in
`SampleScene`.

This is presentation-only. It does not move the first-row top surfaces, pieces,
highlights, click areas, Lua terrain heights, deployment, or combat logic.

## Step 43 - Connect Unit Prefabs To Formal Board Views

Status: Implemented and owner-validated.

Formal piece and enemy presentation can now instantiate unit prefabs through
`BoardUnitVisualCatalog`:

- Piece and enemy prefab entries are resolved by authority unit ID.
- Prefabs may provide `GroundAnchor`, `HealthBarAnchor`, `SelectionAnchor`,
  `HitArea`, `Animator`, and `SortingGroup` data.
- `BoardUnitVisualInstance` aligns prefab feet to the board cell unit anchor,
  forwards facing, movement, attack, die, and reborn animation parameters, and
  computes click/bounds data for interaction and camera framing.
- Missing prefab entries still fall back to generated square placeholders, so
  gameplay authority does not depend on art readiness.

The first art workflow was validated with a `golden_soldier` piece prefab and a
`crab` enemy prefab. Remaining enemy IDs and future pieces can be added to the
catalog without changing Lua gameplay rules.

## Step 44 - Formal UI Snapshot Views

Status: Implemented and owner-validated.

The current formal UI under `Assets/Game/UI` observes `MatchSceneContext`
instead of draining gameplay events directly:

- `UIShop` renders offers, gold, refresh, lock, upgrade, purchase state, and
  works as the drag-to-sell drop zone during Preparation/BossPreparation.
- `RoundInfo` renders wave, phase, timer, local player health, and the ready
  button. The ready button calls the authoritative `SetPlayerReady` command.
- `UIPlayerInfos` renders player health slots and hides unused player entries.
- `UISynergiesBar` renders local-player synergy progress, including
  owned-but-not-active progress.
- `UIPieceInspectPanel` renders the selected local piece only when inspection
  is appropriate.

The current UI does not yet include final feedback polish such as audio,
floating damage, gold spend/gain effects, or explicit settlement callouts.
Those are intentionally deferred until the core loop is stable.

## Step 45 - Mouse Placement, Inspection, And Camera Polish

Status: Implemented and owner-validated.

The preparation interaction model is now:

- Clicking a piece selects it only when the mouse is released before crossing
  the drag threshold.
- Dragging starts only after the pointer moves beyond the threshold. This avoids
  flashing the piece inspection panel when the player's intent is to drag.
- While dragging, all legal target cells are highlighted; releasing on an
  invalid point restores the piece to its authority snapshot position.
- The inspection panel is hidden while a piece is pressed, dragged, or waiting
  for battlefield-facing confirmation.
- Completing placement and facing automatically clears selection.
- Clicking non-piece space clears selection.
- During combat, piece inspection remains available for click selection, but
  placement and shop actions are disabled.

Camera framing is presentation-only:

- During Preparation/BossPreparation, the camera avoids the left inspection
  panel only when that panel should actually be visible.
- Pressing, dragging, or facing-confirmation states do not trigger inspection
  avoidance, preventing camera wobble during deployment.
- During Battle/BossBattle, the camera transitions to the configured battle
  framing and the shop UI is hidden.
- When the temporary reason for camera movement ends, the camera returns to its
  initial scene state.

## Step 46 - End-State Presentation Boundary

Status: Complete; owner-run validation passed.

The Boss and match-result authority already existed in Step 27. This step
stabilizes what the Unity presentation does after `MatchFlowSnapshot.IsFinished`
becomes true:

- Board interaction restores any drag preview, clears highlights, and clears
  selected pieces.
- Keyboard diagnostics stop writing stale selection back into
  `MatchSceneContext`.
- `UIPieceInspectPanel` hides during the final End phase.
- `UIShop` hides after match end, just as it hides during Battle/BossBattle.
- `MatchBoardPresenter` clears selection before syncing piece visuals so a
  final-frame selected outline does not linger.

### Owner-run validation

1. Build or enter Play Mode with no compile errors.
2. Play a strong-board path until the Boss is defeated and the match reaches
   `End` with `Victory`.
3. Play or configure a weak-board path until the Boss reaches the endpoint and
   the match reaches `End` with `Defeat`.
4. In both endings, confirm the shop is hidden, the piece inspection panel is
   hidden, selected-piece outlines are cleared, and pieces can no longer be
   clicked or dragged.

## Step 47 - Formal Flow Cleanup and Result Message Box

Status: Complete; owner-run validation passed.

The formal single-player scene now starts from the normal shop and board
interaction flow rather than from a scene-side demonstration grant:

- `MatchDemoSetup` has been removed from `SampleScene` and from the project.
  The scene no longer grants or deploys an automatic starting `Sprout`.
- `MatchKeyboardInput` remains available as a debug command adapter, but its
  `Enable Debug Input` flag defaults to disabled.
- `MatchDebugHud` remains available as a diagnostic text overlay, but its
  `Show Hud` flag defaults to disabled.
- `UIMessageBox` is now a reusable callback-driven message-box control. The
  prefab root scale has been restored to `1,1,1` so instantiated popups are
  visible.
- `RoundInfo` observes `MatchFlowSnapshot.IsFinished` and opens the message box
  on Victory or Defeat. The temporary validation buttons are:
  `重新开始` to reload the active match scene, `返回主菜单` to load `Menu`, and
  `关闭` to dismiss the popup.

### Owner-run validation

1. Let Unity import scripts and confirm there are no compile errors or missing
   script warnings.
2. Open `SampleScene` and confirm `LuaBootstrap` no longer has a `Demo Setup`
   child.
3. Enter Play Mode. Confirm no piece is deployed automatically at match start;
   the first pieces should come from shop purchases.
4. Confirm keyboard shortcuts and the old pure-text HUD do nothing by default.
   They should only work if their serialized debug flags are manually enabled.
5. Reach Victory or Defeat and confirm `UIMessageBox` appears. Test `关闭`,
   then repeat and test `重新开始` and `返回主菜单`.
## Step 48 - EditMode Test Realignment for the 11x7 Authority Board

Status: Complete; batch verification passed on 2026-06-16.

The `ProtectTree.Runtime.Tests` suite has been updated to match the current
authority board, route timing, and stronger single-player verification setup:

- Board snapshot assertions now validate the current 11x7 layout, including
  reserve row `y=0`, temporary reserve row `y=1`, spawn cells `(10,2)` and
  `(10,5)`, endpoint `(2,2)`, and the current route sample counts.
- Piece snapshot terrain assertions now match the migrated legacy cells:
  `101` and `103` are treated as `HighGround`, while `102` remains `Ground`.
- Enemy endpoint tests now use the current `Crab` movement speed, so
  `ReachedEndpoint` assertions are based on the real 20-second route completion
  timing instead of earlier faster prototype values.
- Block-release assertions were updated to the current unblock-then-advance
  progress result on the migrated map.
- The single-player "strong board" setup used by the ready-flow and
  boss-victory tests now upgrades the shop once and deploys a legal three-piece
  max-level board that can complete the full wave path under current rules.

### Verification

Run the current runtime EditMode suite:

```powershell
& 'E:\Unity\Editor\2022.3.57f1c2\Editor\Unity.exe' `
  -batchmode `
  -runTests `
  -projectPath 'E:\UnityProject\Projects\Protect Tree' `
  -testPlatform editmode `
  -assemblyNames 'ProtectTree.Runtime.Tests' `
  -testResults 'E:\UnityProject\Projects\Protect Tree\Temp\full_runtime_results.xml' `
  -logFile 'E:\UnityProject\Projects\Protect Tree\Temp\full_runtime.log'
```

Expected result after this alignment:

- `ProtectTree.Runtime.Tests`: `54/54` passed
- `failed=0` in `Temp/full_runtime_results.xml`

## Step 49 - Local Two-Player Prototype Entry and Observation Switch

Status: Implemented; owner-run validation pending.

This step moves the project back toward the original multiplayer goal without
introducing real networking yet. The scene can now be started as a local
two-player prototype while keeping player 1 as the local operator.

Code-level changes:

- `MatchStartupOptions` records the next match startup mode. `UseSinglePlayer`
  starts the normal one-player session; `UseLocalMultiplayer(2)` starts a local
  two-player authority session.
- `UIMainMenu` now clears startup options for the single-player button and uses
  the LAN button as the temporary local two-player prototype entry.
- `LuaBootstrap` applies `MatchStartupOptions` after loading the Lua entry
  module. Local multiplayer calls the existing `StartLocalMultiplayer(2)` path.
- `MatchSceneController` owns the current observed player and exposes a safe
  observation switch callback through `MatchSceneContext`.
- `UIPlayerInfos` renders all active players and lets each `UIPlayerInfo` card
  request observation switching. The local player remains player 1; only the
  observed board content changes.
- `MatchSceneContext` now rebuilds static board data when the runtime changes
  and drains authority events once per frame for all presentation features.

### Owner-run validation

1. Open Unity and let scripts recompile. Confirm there are no console compile
   errors.
2. Enter from the main menu with the single-player button. Confirm only player 1
   appears in the player info UI and the match behaves as before.
3. Return to the menu and enter with the LAN/local multiplayer button. Confirm
   player 1 and player 2 appear in the player info UI.
4. Click player 2's info card. The board should switch to player 2's dynamic
   content while the static terrain stays in place.
5. Click player 1's info card. The board should switch back to player 1.
6. In the two-player prototype, shop purchase, deployment, ready button, and
   gold display should still operate only for local player 1.
7. During battle, both players should receive their own spawned enemies; switching
   observation should show the selected player's enemies and pieces only.
