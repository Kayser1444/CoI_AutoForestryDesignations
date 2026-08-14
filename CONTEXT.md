# Auto Forestry Designations

Language for AFD's player-facing forestry automation domain.

## Language

**Forestry vehicle optimizations**:
A world-wide operating mode that keeps forestry vehicles in the field and proactively positions them where forestry work is expected to become available.
_Avoid_: Smart vehicles, planter trailing, smart harvesters

**Work-ready planter**:
An enabled planter carrying saplings, assigned to an enabled forestry tower, and available for planting or proactive positioning.
_Avoid_: Idle planter, loaded planter

**Future planting claim**:
An exclusive, temporary global claim by a work-ready planter on the tile occupied by a tree currently targeted for harvesting by a harvester from the same forestry tower. The claim becomes a planting opportunity when the tree is removed and expires if the tile cannot be planted.
_Avoid_: Planter escort, future reservation

**Future harvest claim**:
An exclusive, temporary global claim by an idle harvester on an immature tree expected to reach its forestry tower's configured harvest threshold. A future harvest claim is a target from which a future planting claim may arise.
_Avoid_: Harvest reservation, smart-harvester target

**Active harvest target**:
A tree assigned to an actual harvesting job. It takes priority over a future harvest claim when recruiting a planter for future planting.
_Avoid_: Future harvest target, mature tree

**Staging position**:
A collision-aware waiting position at a forestry vehicle's working distance from expected work.
_Avoid_: Target tile, parking spot

**Field wait**:
The stationary fallback for an enabled tower-assigned planter or harvester when no current or future work target exists. The vehicle remains at its current position instead of returning to the forestry tower merely to wait.
_Avoid_: Tower parking, speculative staging
