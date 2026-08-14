# Forestry vehicle optimizations — in-game regression checklist

Run this checklist in a disposable test world with the master setting enabled unless a case says otherwise. Observe the vehicle's visible route and job panel; routine claim activity should only be present at Debug log level.

## Setting and lifecycle

- [ ] A new world shows one **Forestry vehicle optimizations** toggle, enabled by default, with the Union of Forestry Workers bathroom-break tooltip.
- [ ] Toggle off, save, reload, and confirm it remains off. Toggle on, save, reload, and confirm it remains on.
- [x] Load a state without `forestryVehicleOptimizations` and confirm the setting defaults to on.
- [x] Toggle changes take effect at a simulation update boundary, log once at Info, and produce no thread-related errors.

## Planters

- [ ] With two planters assigned to one enabled tower, keep one loaded and one empty. Confirm the empty planter still takes an available sapling-pickup job; with no pickup or other real job, it stays where it is.
- [ ] Start an actual same-tower harvest. Confirm the closest work-ready loaded planter stages for the future planting tile at working distance, without occupying the tile or stacking on another vehicle.
- [ ] Confirm only one planter stages for a tile. When that planter leaves to collect saplings, confirm its claim is released and another loaded idle planter can replace it.
- [ ] Give the planter real planting, refuelling, resupply, or another vanilla job. Confirm the real job wins and AFD does not cancel it.
- [ ] After the tree is removed, confirm the exact tree tile is planted when valid and uses the tower's configured mix; make the tile invalid and confirm the claim is released.

## Harvesters

- [ ] With no mature trees, confirm an idle harvester stages at working distance for the best same-tower future tree using maturation and travel time. There is no fixed look-ahead horizon.
- [ ] Add a second harvester and confirm one tree has one global claimant. When the tree reaches threshold, confirm its claimant gets first opportunity for the ordinary harvest job.
- [ ] Make a ready tree available and confirm actual harvest work preempts future staging.

## Overlap, waiting, and toggle-off behavior

- [ ] Make two tower areas overlap. Confirm tree/tile claims remain globally exclusive, planter/harvester coordination remains same-tower, and a released claim can be acquired by the other eligible tower.
- [ ] With an enabled, assigned planter or harvester that has no current or future target, confirm it stays at its current field position and does not drive to the tower.
- [ ] Turn the master setting off while staging is active. Confirm AFD staging and claims are released, actual vanilla jobs continue, and vanilla routing resumes.

## Save/removability

- [ ] Save while vehicles are staged, reload, and confirm no AFD claim/job state was serialized and useful behavior is reconstructed from the world.
- [ ] Remove AFD using the workspace save-removability procedure and confirm the save still loads without serialized claim entities or custom jobs.
