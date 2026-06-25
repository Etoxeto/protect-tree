# Boss Skills

This document records the current Boss skill authority and presentation contract.

## Current Boss

The current Boss config entry is:

```text
Assets/Game/Lua/Config/Enemies.lua.txt
Mysterious_Goat
```

Boss skill authority is implemented in:

```text
Assets/Game/Lua/Match/EnemyAttackPlanner.lua.txt
Assets/Game/Lua/Match/EnemyRoster.lua.txt
```

## Skill Rules

## Boss Wave Rules

- Boss waves may contain regular minions and exactly one Boss.
- Regular Boss-wave minions spawn independently on every alive player's board.
- The Boss spawns once and keeps one shared health pool.
- Boss-wave minions do not damage player health when they leak. They are
  respawned on the same player's board instead.
- When the Boss appears, Boss-wave minions enter an enraged state. The first
  tuning value is `x2` movement speed and `x2` attack speed; their bound `Eyes`
  sprites are shown and Animator speed is doubled for presentation.
- Boss battle failure is timer-based. The Boss does not enter the player
  endpoint to cause defeat.
- The Boss uses dedicated route segments defined in `Config.Board`:
  `boss_routes.outbound_route_id` and `boss_routes.return_route_id`.
- Transfer happens after the return segment completes, or early if all deployed
  board pieces owned by the current target player are downed.

`magic_shooting`

- Damages player pieces.
- Uses the Boss horizontal facing.
- The Boss only uses `Left` and `Right` for this rule.
- Facing is derived from the Boss route direction.
- It damages active board pieces owned by the Boss target player if they are
  on the same grid row and in front of the Boss.

`magic_explosion`

- Damages player pieces.
- Chooses one random active board piece owned by the Boss target player.
- The random selection is driven by a deterministic Lua-side RNG in
  `EnemyAttackPlanner`.

## Events

Boss skills emit two kinds of events:

```text
BossSkillCast
PieceDamageRequested
```

`BossSkillCast` is for presentation. It does not change health by itself.
It also carries `cast_lock_seconds`; `Session` routes that event back into
`EnemyRoster`, and the Boss stops advancing along the route while the lock is
active. Current skill configs set this to the cast animation length plus a
short idle hold, so the Boss remains still briefly after magic finishes.

`PieceDamageRequested` is the authority damage command consumed by
`PieceRoster`.

Presentation distinguishes the skill through `projectile_id`:

| `projectile_id` | Animator trigger |
| --- | --- |
| `BossMagicShooting` | `magic_02` |
| `BossMagicExplosion` | `magic_01` |

`AnimationVfxTrigger` exposes `Invert Shooting Magic Facing`. It is enabled by
default because the current `magic_shooting` art faces opposite to the Boss
horizontal facing. If the prefab art direction changes later, turn this option
off on the Boss visual prefab instead of changing Lua skill logic.

## Current Limits

- `magic_explosion` visual is not yet spawned at the selected target's foot
  position. It currently relies on the animation event fallback point.
- Boss transfer presentation still needs a dedicated sequence if we want
  `transfer_in -> move visual -> transfer_out`.
- Boss-wave minion respawn currently reuses the same route and enemy type
  immediately. Later tuning can add respawn delay, effects, or caps.
- Skill values are intentionally simple demo values and should be tuned later.

## Presentation Notes

- Boss skill visuals use `AnimationVfxTrigger.Sorting Layer Name` and
  `Sorting Order`; `MagicShooting` and `MagicExplosion` apply those values to
  all child `SpriteRenderer` components when spawned.
- `BoardUnitVisualInstance` updates `AnimationVfxTrigger` sorting from the
  unit's current board sorting order, so spawned Boss magic appears above the
  board row where the Boss is standing.
- During Boss skill lock, both authority movement and visible movement pause.
  The visual lock remains as presentation smoothing, but it no longer hides a
  continuing authority movement.
- Board pieces, enemies, and Boss prefab visuals use the shared
  `BoardUnitVisualInstance` tint path for hit feedback. When their snapshot
  health drops, the body sprites briefly flash red, then restore their original
  colors.
