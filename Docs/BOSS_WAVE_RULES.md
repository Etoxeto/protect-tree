# Boss Wave Rules

This document records the current Boss-wave authority rules.

## Current Flow

- Normal combat has a defense timer from `Config.MatchFlow.battle_defense_seconds`.
- Joint defense uses `Config.MatchFlow.joint_defense_seconds`.
- Boss battle uses `Config.MatchFlow.boss_defense_seconds`.
- Normal and joint-defense timeout rules treat surviving enemies as leaks.
- Boss timeout ends the match in Defeat.

## Boss_Goat

`Config.Waves.presets.Boss_Goat` may spawn both regular minions and the shared
Boss:

- Sheep and Goat are regular Boss-wave minions.
- `Mysterious_Goat` is the single shared Boss.
- The Boss group uses route `3`, the outbound Boss route.
- Route `4` is the return Boss route.

Regular Boss-wave minions are copied once per alive player. The Boss is not
copied; it is created once and retargets between players' boards.

## Minion Enrage

When the Boss is created, `Session` sends `BossMinionsAwakened` to
`EnemyRoster`.

Current tuning:

- movement speed multiplier: `2`
- attack speed multiplier: `2`
- presentation Animator speed: `2`
- prefab `Eyes` renderers: shown while enraged

Minions spawned after the Boss appears also enter the enraged state.

## Boss Transfer

Boss transfer can happen in two ways:

- The Boss finishes the return route segment and emits `BossTransferRequested`.
- All deployed board pieces on the currently targeted player board are downed.

Transfer retargets the same Boss instance to the next alive player's board and
resets it to the outbound Boss route. In single-player, the Boss retargets back
to the same player so the route can loop.

`BossTransferRequested` is published first so presentation can play
`transfer_in`. Session then waits
`Config.MatchFlow.boss_transfer_intro_seconds` before sending the actual
retarget request, which produces `BossRetargeted` and lets presentation play
`transfer_out` on arrival.

## Boss-Wave Leaks

Boss-wave minions do not damage player health when they reach the endpoint.
Instead, Session immediately respawns the same enemy type for the same player
and route. This keeps pressure on the board without using the normal leak-health
system during Boss battle.
