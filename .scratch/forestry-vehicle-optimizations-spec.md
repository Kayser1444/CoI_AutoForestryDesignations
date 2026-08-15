## Problem Statement

Forestry planters and harvesters return to their tower or remain idle when no job is immediately available, even when the tower's current work makes the next useful location predictable. Loaded planters waste time returning from the field while harvesters are already creating future planting opportunities. Harvesters likewise park instead of moving toward trees that are expected to reach the tower's harvest threshold. The result is unnecessary travel, avoidable vehicle idle time, and forestry fronts that advance less smoothly than they could.

## Solution

Add one world-wide, save-persisted, default-on **Forestry vehicle optimizations** setting. When enabled, AFD coordinates tower-assigned planters and harvesters through exclusive, temporary work claims rather than permanent vehicle pairings.

A work-ready planter may claim the future planting tile beneath a tree targeted by a same-tower harvester and immediately stage at a collision-aware working distance. An idle harvester may claim an immature tree in its tower area and stage where it can begin work when the tree reaches the configured harvest threshold. Actual work always takes priority over speculative staging, claims prevent flocking, and repeated same-tower claims produce escort-like behavior only when that behavior is useful.

When an enabled tower-assigned planter or harvester has neither current nor future work, it stays at its current field position. AFD does not send it back to the tower merely to wait.

The player-facing tooltip is:

> Suppresses the mandatory bathroom breaks enforced by the Union of Forestry Workers. Idle planters and harvesters are forced to remain in the field and proactively move to places where work is expected to materialize.

## User Stories

1. As a player, I want one Forestry vehicle optimizations toggle, so that I can control planter and harvester routing behavior together.
2. As a player, I want the optimization enabled by default, so that normal play benefits without extra configuration.
3. As a player, I want the setting to apply to the whole world, so that its behavior is consistent across forestry towers.
4. As a player, I want the setting saved with my world, so that my choice survives save and reload.
5. As a player loading an older save, I want the new setting to use its default-on value when absent, so that I receive the improved behavior automatically.
6. As a player, I want the previous narrower keep-loaded-planters setting replaced, so that overlapping settings do not create confusing combinations.
7. As a player, I want a humorous but descriptive tooltip, so that the setting's effect is memorable and understandable.
8. As a player, I want a loaded planter to remain near credible future work, so that it does not make an unnecessary round trip to its tower.
9. As a player, I want an empty planter to retain vanilla sapling collection behavior, so that optimization never prevents resupply.
10. As a player, I want disabled vehicles and vehicles belonging to disabled towers to retain vanilla behavior, so that administrative controls remain authoritative.
11. As a player, I want planters to stage only for work inside their assigned tower's allowed area, so that they do not follow harvesters somewhere they cannot plant.
12. As a player, I want an active harvester target to create a future planting opportunity, so that a planter can arrive before the stump becomes available.
13. As a player, I want the closest eligible planter recruited for a new active harvest target, so that unnecessary planter travel is minimized.
14. As a player, I want planter proximity measured consistently, so that recruitment is predictable.
15. As a player, I want deterministic tie-breaking between equally close planters, so that identical situations do not produce unstable behavior.
16. As a player, I want at most one planter preparing for a harvested tree tile, so that planters do not flock to the same future planting opportunity.
17. As a player, I want each planter to prepare for at most one future tile, so that its intent remains unambiguous.
18. As a player, I want a replacement work-ready planter to claim a target when the current planter leaves to collect saplings, so that the harvesting front remains supported.
19. As a player, I want active planting work to interrupt speculative staging, so that plantable tiles are not left waiting.
20. As a player, I want refuelling, resupply, and other real vehicle jobs to outrank speculative staging, so that optimization cannot starve essential work.
21. As a player, I want a future planting claim released when its vehicle, tower, target, route, or job becomes invalid, so that stale claims do not block useful work.
22. As a player, I want the planter to validate the exact harvested-tree tile after tree removal, so that it plants there only when the tile is genuinely valid.
23. As a player, I want an invalid future planting tile released immediately, so that the planter can resume ordinary target selection.
24. As a player, I want planting species chosen from the tower's configured vanilla mix, so that optimization does not alter forestry composition.
25. As a player, I want idle harvesters to move toward trees expected to become harvestable, so that they can begin cutting promptly.
26. As a player, I want a future harvest claim to be globally exclusive, so that multiple harvesters do not stage for the same tree.
27. As a player, I want each idle harvester to hold at most one future harvest claim, so that its speculative target is clear.
28. As a player, I want harvester target choice to account for both maturation and travel, so that a vehicle stages where work is expected to become actionable soonest.
29. As a player, I want tree readiness calculated against the tower's configured harvest percentage, so that optimization respects my harvesting policy.
30. As a player, I want the tree's actual growth duration used, so that different species and growth conditions are scored correctly.
31. As a player, I want no arbitrary look-ahead horizon, so that a credible future tree can keep a vehicle in the field even when maturation is distant.
32. As a player, I want a future-harvest claimant to receive first right to its tree when it matures, so that staging produces useful work rather than a race.
33. As a player, I want newly available actual harvest work to outrank speculative harvest staging, so that ready trees are processed first.
34. As a player, I want an active harvest target to outrank a future harvest target when recruiting a planter, so that confirmed planting opportunities receive support first.
35. As a player, I want equal-priority claims to remain stable, so that vehicles do not continually change targets for marginal distance gains.
36. As a player, I want future harvest claims to generate same-tower future planting claims, so that planter and harvester movement coordinates naturally without permanent pairing.
37. As a player with overlapping forestry areas, I want claims to be exclusive across the world, so that vehicles from different towers cannot claim the same tree or tile simultaneously.
38. As a player with overlapping forestry areas, I want a released target to become available to either eligible tower, so that overlap does not permanently assign ownership.
39. As a player, I want speculative vehicles to wait at their normal working distance, so that they are ready without occupying the target tile.
40. As a player, I want staging positions to account for nearby vehicles, so that optimized vehicles do not stack on top of one another.
41. As a player, I want an idle planter or harvester with no current or future target to stay at its current position, so that it does not waste time and fuel returning to the tower merely to wait.
42. As a player, I want disabling the setting to cancel only AFD speculative staging, so that real vehicle jobs remain untouched.
43. As a player, I want disabling the setting to restore vanilla routing promptly, so that the toggle has an immediate, understandable effect.
44. As a player, I want saving to release transient staging control, so that no mod-specific runtime job state is embedded in the save.
45. As a player, I want claims reconstructed from the loaded world, so that optimized behavior resumes safely after loading.
46. As a player, I want to remove AFD from a save without leaving serialized claim entities or custom jobs behind, so that the mod remains save-removable.
47. As a player, I want unreachable speculative targets handled like vanilla unreachable work, so that vehicles recover instead of retrying path failures continuously.
48. As a maintainer, I want ordinary claim and staging activity logged only at Debug level, so that normal logs remain quiet.
49. As a maintainer, I want genuine failures reported at Warning or Error level, so that actionable runtime problems remain visible.
50. As a maintainer, I want setting changes logged at Info level, so that behavioral changes can be correlated with player actions.
51. As a maintainer, I want all claim and vehicle mutation orchestration on the simulation thread, so that game state is not mutated from Unity's main thread.
52. As a maintainer, I want main-thread UI changes handed off for simulation-safe application, so that changing the toggle cannot race simulation updates.

## Implementation Decisions

- Replace the existing narrower world setting for keeping loaded planters in the field with one persisted `forestryVehicleOptimizations` setting whose default is true.
- Do not migrate the value of the old setting. Ignore the obsolete field. Saves without the new field receive the new default of true.
- Present the new setting in the existing optimization section of AFD's settings UI with the approved label and tooltip.
- Treat the setting as world-wide only; do not add per-tower overrides.
- Use a single simulation-thread-owned coordinator and global claim registry as the behavioral authority.
- The main thread may display synchronized state and request a setting change, but the simulation thread applies the change at a safe update boundary.
- Keep future planting claims and future harvest claims entirely transient and derived. Do not serialize claims, staging ownership, custom entities, or custom jobs.
- Before saving, relinquish AFD-created transient staging control. After loading, derive fresh claims from the world and currently idle vehicles.
- Coordinate through work claims, not persistent planter-to-harvester pairing. Escort-like movement should emerge only from repeated claims on successive targets.
- Identify claimed harvest targets by stable tree identity and claimed planting opportunities by the exact tile under that tree.
- Enforce one claim per claiming vehicle and one global owner per claimed tree or tile.
- Restrict planter recruitment to work-ready planters: enabled, carrying saplings, assigned to the same enabled forestry tower, and free of higher-priority vanilla work.
- Restrict future harvest claims to enabled, idle harvesters assigned to an enabled forestry tower and trees that are valid candidates for that tower.
- Empty planters continue through vanilla sapling-pickup logic. If no pickup or other real job is available, they stay put rather than returning to the tower merely to wait.
- Observe both actual harvester target assignment and future harvest claim creation on the simulation thread. Either kind of target may recruit a same-tower work-ready planter.
- Recruit the closest eligible planter using straight-line tile distance. Resolve equal distances by stable vehicle entity ID.
- Active harvest targets have higher planter-recruitment priority than future harvest claims. They may preempt only a lower-priority AFD future planting claim.
- Equal-priority future planting claims remain stable and are not rebalanced for a newly closer planter.
- Release a future planting claim when the planter becomes ineligible, accepts actual work, needs fuel or saplings, leaves the tower relationship, cannot navigate, or when the target harvest job/tree becomes invalid.
- When a claimed tree is removed, validate the exact tile as a vanilla planting opportunity. If valid, allow ordinary planting behavior to use it; if invalid, release it and resume normal target selection.
- Preserve vanilla stump-first planting and select species from the tower's configured planting mix.
- Score a future harvest candidate by `max(time until the tower-configured harvest threshold, estimated straight-line travel time)`.
- Use linear tree growth and the tree prototype's effective maximum age when estimating time to the exact tower harvest threshold.
- Break equal future-harvest scores by shorter straight-line distance and then stable tree ID.
- Do not impose a maximum future-harvest look-ahead horizon.
- Future harvest claims remain stable and are not periodically rebalanced.
- Hide a future-harvest-claimed tree from competing harvesters. When it reaches the threshold, its claimant receives first opportunity to convert the claim into an ordinary vanilla harvesting job.
- Any newly available actual harvesting work preempts a harvester's AFD speculative staging.
- Claims are globally exclusive even where tower areas overlap, but every planter/harvester collaboration must remain within one tower. Once a claim is released, any eligible overlapping tower may acquire it.
- Use vanilla annular tree/planting vehicle goals for collision-aware staging at working distance, and use vanilla navigation jobs rather than introducing a persistent custom job type.
- Reuse vanilla unreachable-tree and unreachable-tile tracking and its retry behavior. Do not add a separate AFD cooldown.
- While the setting is enabled, suppress vanilla return-to-tower parking for enabled, tower-assigned planters and harvesters after real and speculative target selection is exhausted. With no current or future target, the vehicle receives no AFD movement job and stays at its current position. Disabled vehicles, disabled towers, and operation with the master setting off retain vanilla behavior.
- Turning the setting off releases all claims and cancels only staging created by AFD. It must not cancel actual harvesting, planting, resupply, refuelling, or other vanilla jobs.
- Emit Debug logs for normal claim acquisition, release, staging, conversion, and preemption. Use Warning or Error only for genuine exceptional conditions. Emit Info when the master setting changes.
- Retire the two narrower backlog concepts—planters trailing harvesters and smart harvesters—in favor of this combined claim-based feature.

## Testing Decisions

- Use one acceptance seam: end-to-end behavior in the live game simulation around controlled forestry towers. Tests should assert visible vehicle behavior, claim exclusivity inferred from behavior, save/load outcomes, and logs rather than private coordinator internals.
- Extend AFD's existing manual regression-plan approach. Do not introduce a mock-heavy unit-test project for game internals.
- Run the active AFD solution's Debug build as structural verification before in-game testing.
- Verify the setting is visible once, defaults on in a new world, persists both on and off, and uses default-on when loading state that lacks the new field.
- Verify changing the setting logs once at Info and takes effect at a simulation-safe boundary without thread-related errors.
- Verify a loaded idle planter claims and stages for an actual same-tower harvest target, while an empty planter collects saplings normally.
- Verify the closest of multiple eligible planters wins a new target and only one planter stages for each target.
- Verify a claimed planter leaving for saplings releases its claim and permits another loaded idle planter to replace it.
- Verify active planting, refuelling, resupply, and other real jobs preempt speculative planter staging without being cancelled.
- Verify the exact tree tile is planted after harvesting when valid, uses the configured tree mix, and is released when invalid.
- Verify an idle harvester selects an immature tree using the agreed maturation/travel score and stages at working distance.
- Verify two harvesters cannot claim one tree and that the claimant receives first opportunity when the tree matures.
- Verify ready harvest work preempts future-harvest staging.
- Verify an active harvest target can recruit a planter away from a lower-priority future target, while equal-priority claims remain stable.
- Verify overlapping tower areas still produce one global owner, never cross-tower planter/harvester collaboration, and permit reacquisition after release.
- Verify staging vehicles keep working distance and do not occupy the target tile or overlap another vehicle.
- Verify navigation failure uses vanilla unreachable behavior and does not cause rapid retry loops.
- Verify enabled, tower-assigned planters and harvesters with no current or future target remain at their current positions and receive no return-to-tower parking job.
- Verify an empty planter still accepts an available sapling-pickup job, but stays put if no pickup or other real job exists.
- Verify disabling the setting releases speculative claims/staging but leaves all actual jobs running.
- Verify saving and reloading produces no serialized AFD claims, reconstructs useful behavior afterward, and remains loadable after removing AFD according to the workspace's save-removability procedure.
- Verify routine operation emits Debug-only messages and no unwarranted Warning or Error entries.

## Out of Scope

- Permanent planter-to-harvester escort assignments or explicit paired-vehicle state.
- Cross-tower pairing, even where forestry areas overlap.
- Optimization of unassigned harvesters or manual harvesting designations.
- Per-tower overrides for Forestry vehicle optimizations.
- A configurable prediction horizon.
- Periodic rebalancing of equal-priority claims.
- Custom pathfinding, collision logic, or unreachable-target cooldowns.
- Persisting speculative claims or AFD-created vehicle jobs in saves.
- Changes to vanilla tree growth, harvest thresholds, planting validity, stump priority, or configured species selection.
- The separate forestry truck-manager backlog item.
- A player-facing claim visualization or vehicle-pairing UI.

## Further Notes

- The existing domain glossary defines Forestry vehicle optimizations, work-ready planter, future planting claim, future harvest claim, active harvest target, and staging position; implementation and documentation should use those terms.
- The accepted architecture records claims as globally exclusive but same-tower coordinated, derived runtime state owned by the simulation thread.
- Active-work preemption is expected to be uncommon in stable areas, but is required for area edits, threshold changes, re-enabling harvesting, differing tree ages/species, unreachable-state recovery, route-estimation error, manual immediate work, and multi-vehicle contention.
- The user will execute the acceptance suite in game.
