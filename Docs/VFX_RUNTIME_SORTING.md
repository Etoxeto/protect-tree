# VFX Runtime Sorting

This note records how board-attached VFX should choose rendering order.

## Rule

Board play should not rely on a fixed low `Sorting Order` for unit or Boss
animation VFX. Front board rows often use sorting orders much higher than
`3000`, so a fixed value can place magic effects behind board tiles.

When a unit prefab contains `AnimationVfxTrigger`, `BoardUnitVisualInstance`
updates that trigger's runtime sorting order from the unit's current board
sorting order. Spawned Boss magic therefore follows the caster's board depth
and renders above the row where the caster is standing.

Manual `Sorting Order` values on `AnimationVfxTrigger` are still useful for
prefab preview. In formal board play, runtime board sorting is the final source
of truth.
