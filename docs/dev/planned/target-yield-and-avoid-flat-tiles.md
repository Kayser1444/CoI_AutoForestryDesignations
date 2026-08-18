# AFD: Target yield and avoid flat tiles

> Status: implemented in the current worktree. The completed architecture and
> verification notes are in `docs/dev/done/target-yield-and-avoid-flat-tiles.md`.

## Problem Statement

Auto Forestry Designations currently limits a scan by the number of forestry
designation areas placed in that run. A designation count is an implementation
quantity, not a player-facing production goal. Players who want a sustainable
wood supply must estimate how many areas and trees they need, and the estimate
changes with tree type, harvest threshold, tree spacing, and existing woodland.

Players also want to use rough, uneven ground for forestry while preserving
level ground for buildings. AFD currently has no filter for this intent.

The feature must preserve AFD's explicit **Create designations** workflow,
per-tower settings, save persistence, and safe behavior for existing saves.

## Solution

Add an **Avoid flat tiles** per-tower filter and replace the new-user-facing
**Maximum number of designations** control with **Target yield**.

**Avoid flat tiles** skips a 4×4 planting designation when all four of its
corner `HeightTilesF` values are within the game's surface-height tolerance of
the same integer height. This uses the four designation vertices, not a raw
floating-point equality test.
It is opt-in, defaults off, and only affects future scans. Its player-facing
tooltip is:

> Use only uneven, rough tiles for forestry. (This preserves flat tiles for
> buildings.)

**Target yield** is a per-tower sustainable wood output target expressed in
wood per month. A finite target causes AFD to select eligible planting areas
until projected total sustainable capacity for the tower reaches or exceeds
the target. The final area may overshoot the target. **∞** means no target and
allows the normal unlimited scan; ∞ is the default for new towers.

Target planning uses the same spacing-aware sustainable-yield model as the
Forestry information panel and includes existing managed capacity plus
newly selected designation reservations. Existing designations are never
removed automatically. The player applies changed settings by pressing
**Create designations**.

## User Stories

1. As a player with limited building space, I want to avoid flat planting areas, so that AFD reserves level ground for buildings.
2. As a player, I want “flat” to have a deterministic meaning, so that I can predict why a planting area was skipped.
3. As a player, I want a 4×4 area to count as flat only when all four designation-corner heights are within the game's surface-height tolerance of the same integer, so that visibly uneven ground remains available for forestry.
4. As a player, I want the flatness test to use the game's `HeightTilesF` values and surface-height tolerance, so that it agrees with the terrain behavior used by building placement.
5. As a player, I want Avoid flat tiles to be configurable independently for each forestry tower, so that one tower can preserve building land while another uses all suitable ground.
6. As a player, I want the global Avoid flat tiles default to be off, so that existing forestry behavior and new towers are not unexpectedly restricted.
7. As a player, I want changing Avoid flat tiles to affect future scans only, so that changing a setting does not silently remove work I already ordered.
8. As a player, I want existing forestry designations to remain when I enable Avoid flat tiles, so that I retain explicit control over cleanup and replacement.
9. As a player, I want the Avoid flat tiles tooltip to explain both the rough-ground behavior and the building-space purpose, so that I understand the tradeoff without reading developer documentation.
10. As a player, I want to set a wood-per-month target for a tower, so that I can request a sustainable supply instead of guessing a designation count.
11. As a player, I want Target yield to mean total projected capacity for the tower, including existing managed trees and reserved future planting capacity, so that repeated scans converge on one production goal.
12. As a player, I want a target to be treated as a minimum, so that AFD can reach or slightly exceed it despite 4×4 area granularity and planting-spacing constraints.
13. As a player, I want the final designation that crosses the target to be accepted, so that AFD does not leave the tower unnecessarily below its requested yield.
14. As a player, I want AFD to keep its existing candidate priority while filling a target, so that reachable and nearby areas are preferred for practical forestry logistics.
15. As a player, I want reachable candidates to be prioritized before geometric-distance fallbacks, so that Target yield does not choose theoretically productive but inaccessible areas.
16. As a player, I want newly selected but not-yet-fulfilled designations to count immediately toward the target, so that repeated Create actions during planting do not overshoot because the information panel has not refreshed yet.
17. As a player, I want Create designations to add nothing when current managed and reserved capacity already meets the target, so that a scan is safe to repeat.
18. As a player, I want raising a target and scanning again to add capacity incrementally, so that I can expand production without clearing and rebuilding the tower's designations.
19. As a player, I want lowering a target not to remove existing designations, so that the target setting remains non-destructive and explicit cleanup stays with me.
20. As a player, I want ∞ to mean no yield target, so that I can opt out of Target yield and restore unlimited eligible scanning.
21. As a player, I want ∞ to be displayed instead of 0, including in the relevant tooltip text, so that the control communicates “unlimited/no target” directly.
22. As a player, I want the Target yield control to use the existing native plus/minus interaction, so that the new setting feels consistent with the rest of the Forestry designations panel.
23. As a player, I want normal, Shift, and Ctrl adjustments of 1, 10, and 100 wood/month, so that I can tune a target precisely or raise it quickly.
24. As a player, I want the Target yield row to show the configured target only, so that the compact settings panel remains readable.
25. As a player, I want the existing Forestry information panel to remain the place where current sustainable yield is displayed, so that the target control does not duplicate live statistics.
26. As a player, I want Target yield to use the forestry tower's existing vanilla tree-type configuration, so that AFD can estimate output without adding a second tree-selection system.
27. As a player, I want Target yield to use the existing spacing-aware capacity model, so that different tree spacing, existing trees, and neighboring planting positions affect the estimate realistically.
28. As a player, I want only managed forestry capacity to count toward the target, so that unrelated woodland inside the tower's area does not falsely satisfy my production goal.
29. As a player, I want Target yield to remain independent of harvest-ready marking, so that I can use the yield target and harvesting preference separately.
30. As a player, I want all other scan filters to remain authoritative while Target yield is active, so that AFD never uses flat, infertile, occupied, designated, or unreachable areas merely to satisfy a target.
31. As a player, I want an unreachable target to consume all eligible capacity and then stop, so that AFD makes the best safe effort without overriding my filters.
32. As a player, I want no special notification or counter for skipped flat areas or an unmet target, so that scans remain quiet and consistent with existing filters.
33. As a player, I want a finite-target scan to stop safely if its yield estimate is unavailable, so that a calculation failure cannot silently become unlimited planting.
34. As a player, I want a very large finite-target scan to use no more than about 10 ms per rendered frame while playing or 30 ms while paused, so that yield planning remains responsive.
35. As a player, I want a long scan to show progress through newly appearing forestry designations rather than a toast, so that I can see that the operation is advancing.
36. As a player with an existing save, I want my saved Max tiles limit preserved during migration, so that an update does not silently turn a deliberately limited tower into unlimited forestry.
37. As a player with a migrated Max tiles limit, I want the legacy state disclosed in the Target yield tooltip, so that the displayed ∞ is not misleading.
38. As a player, I want explicitly setting Target yield—including setting it to ∞—to clear the hidden legacy Max tiles limit, so that adopting the new control removes invisible old behavior.
39. As a player, I want Target yield and Avoid flat tiles to persist per tower and through save/reload, so that my forestry planning survives game sessions.
40. As a player using a supported language, I want the new labels and tooltips localized consistently with the existing AFD settings, so that the feature is understandable in-game.
41. As a mod integrator, I want the existing AFD public API to remain source-compatible, so that changing scan policy does not require unrelated mods to change.
42. As a maintainer, I want the scan planner and sustainable-yield estimator to be testable at their highest cohesive seams, so that target convergence and filter behavior can be verified without coupling tests to UI implementation details.

## Implementation Decisions

- Add `Avoid flat tiles` to the per-tower forestry settings and global defaults. The setting defaults to false and is persisted using the existing sparse per-tower settings model.
- Evaluate flatness at the 4×4 designation candidate level. Read the four candidate corner heights as `HeightTilesF` values and reject the candidate only when all four are within `TerrainDesignation.SURFACE_HEIGHT_TOLERANCE` of the same rounded integer height. Do not introduce a building-prototype lookup or building-footprint simulation.
- Replace the new-user-facing Max tiles control with a Target yield control. Target yield is an integer sustainable wood-per-month value; zero is the stored no-target value and is displayed as ∞.
- Keep the Target yield control per tower, with a global default for towers without overrides. The global and new-tower default is no target/∞.
- Keep the native plus/minus stepper and its modifier behavior: 1, 10, and 100 wood/month for normal, Shift, and Ctrl adjustments.
- Apply both settings only when the player explicitly invokes Create designations. Changing a setting must not start a scan, remove designations, or rebalance existing work.
- Treat Target yield as a total projected sustainable-capacity floor for managed forestry in the selected tower. Existing managed trees and existing managed planting capacity count toward the current total.
- Count newly selected designations as reserved future capacity during the same scan. The planner must maintain enough reservation state to make a repeated Create action converge even before vanilla marks the new designations fulfilled.
- Reuse one shared sustainable-yield estimator for the Forestry information panel and Target yield planning. The estimator must use the tower's current vanilla tree-type configuration, harvest threshold, tree maturation/yield data, and spacing-aware planting capacity model.
- The estimate is a sustainable-capacity projection, not a guarantee of current immature-tree output and not a vehicle/truck throughput simulation. It must match the existing information-panel model rather than introduce a second definition of sustainable yield.
- When the current projected managed capacity is already at or above the target, place no new designations.
- When the target is not met, select candidates in the existing priority order: reachable candidates by driving distance, then geometric distance as the existing fallback. Build one spatially indexed capacity projection, place candidates in that order, and update the same projection after each successful designation. Stop at the first designation that reaches the target; do not remove already committed designations. Accept only unavoidable single-designation overshoot.
- Apply all existing eligibility filters before target selection. Target yield must never relax Avoid flat tiles, fertility, existing-tree, or reachability policies; the world-level Override terrain designations setting controls terrain-designation replacement.
- If eligible capacity is exhausted before reaching the target, place all eligible capacity and leave the tower below target. Do not add a special player-facing alert or automatically change another setting.
- If a finite-target estimate cannot be evaluated, fail closed for that scan: do not add new designations under the finite target. ∞ retains the normal unlimited scan behavior.
- Slice candidate collection, pathability work, capacity planning, and placement by elapsed time: 10 ms per rendered frame while playing and 30 ms while paused. Large operations expose progress by placing designations before yielding; do not add a progress toast. Never fall back to unlimited planting.
- Leave Mark harvest-ready trees for harvest independent of Target yield. Target yield controls planting designation creation only.
- Count only trees and planting capacity covered by the tower's managed forestry designations. Unmanaged trees elsewhere in the tower area do not satisfy the target.
- Preserve existing Max tiles values during settings/save migration as hidden legacy limits when a saved tower or existing global configuration contains them. Do not convert tile counts to wood/month because no reliable conversion exists.
- Show the Target yield value as ∞ for a migrated tower whose yield target is unset, but disclose the retained legacy Max tiles value in the Target yield tooltip. Explicitly setting Target yield, including setting it to ∞, clears the legacy limit for that tower/default.
- Add the new setting fields to the existing settings and tower-state schema with a forward-compatible migration. Existing settings records that lack the new fields must continue to load.
- Update the Forestry designations panel, shared mod settings/defaults surface, localization source strings, and all supported translation files. The Target yield tooltip must explain that ∞ means no target; the Avoid flat tiles tooltip uses the agreed player-facing wording.
- Keep the existing public AFD API signatures and panel-builder contracts. The behavioral change is consumed through current tower settings and Create designations calls; no new public API is required.

## Testing Decisions

- Tests should verify externally observable scan and settings behavior rather than private helper structure or exact implementation details.
- Add deterministic tests at the scan-planner seam for:
  - all four near-integer corner heights being rejected when Avoid flat tiles is enabled;
  - a visibly uneven fractional corner outside the surface tolerance being accepted;
  - a corner just within the surface tolerance still being rejected;
  - the filter being inactive when disabled;
  - all other eligibility filters remaining authoritative;
  - no automatic removal or scan when settings change;
  - no placement when an already-met target is scanned;
  - candidate placement continuing until the target is met or exceeded;
  - unavoidable single-candidate overshoot being accepted;
  - current reachability/driving-distance priority being preserved;
  - newly placed designations updating capacity during the same planning operation;
  - exhausted eligible capacity stopping below target;
  - finite-target estimator failure failing closed;
  - 10 ms play and 30 ms paused slice selection;
  - spatial spacing selection matching the former exhaustive comparison result;
  - ∞ using the unlimited scan path.
- Add estimator tests around externally visible sustainable-capacity results for configured tree types, harvest threshold, spacing, existing managed trees, and reserved future planting positions. The Forestry information panel and Target yield planner must agree for the same tower state.
- Add migration tests for new settings, old settings with no target field, saved per-tower overrides, global defaults, retained legacy Max tiles limits, explicit Target yield changes, and explicit ∞ clearing legacy limits.
- Add UI-facing tests or focused manual checks for per-tower isolation, global defaults, stepper increments, ∞ rendering, tooltip wording, localization keys, and inspector refresh when switching towers.
- Add save/reload regression coverage for Target yield, Avoid flat tiles, sparse per-tower overrides, and legacy settings. Verify that settings remain safe when AFD is removed from a save according to the maintained-mod save-removability rules.
- Use the existing AFD scan and Forestry information-panel architecture references as prior art. There is no dedicated AFD test project currently; introduce the smallest testable seams needed for deterministic planner and estimator coverage rather than coupling tests to Unity UI or static dictionaries.
- Verify the implementation with the maintained AFD build and the relevant manual in-game regression scenarios, including a large tower scan, a fully flat area, a mixed-height area, an unmet target, a target increase, a legacy Max tiles save, and save/reload.

## Out of Scope

- A building-placement or building-footprint compatibility test. Avoid flat tiles is intentionally a terrain-corner heuristic, not a guarantee that every building can fit.
- A tree-type selection UI or a new tree-mix configuration system. Target yield consumes the forestry tower's existing vanilla configuration.
- Automatic rescanning, automatic designation removal, or continuous target-yield enforcement.
- Special notifications, result toasts, skipped-flat counters, or a new target-status window.
- Optimizing candidate selection for the mathematically closest target or maximizing yield per area. Existing reachability and distance priority remains authoritative.
- Vehicle, harvester, planter, truck, or transport throughput modeling beyond the sustainable-yield model already used by the Forestry information panel.
- Changes to Mark harvest-ready trees for harvest.
- Changes to the AFD public API surface.
- A public release, changelog finalization, package build, or game-version compatibility claim as part of this specification.

## Further Notes

- The feature combines two related player goals: preserving level building land and expressing forestry scale in production terms. Avoid flat tiles is an independent filter; Target yield must not implicitly enable it.
- A finite target is intentionally a planning floor rather than an exact optimizer. 4×4 designation granularity, discrete planting positions, spacing, and existing trees make exact matching neither necessary nor reliable.
- The Target yield tooltip should make the migration behavior understandable without exposing implementation vocabulary in the normal case. The legacy-limit note is only needed for migrated towers that still carry the old cap.
- Once implemented, keep player guidance, scan architecture documentation, API
  behavior notes, translations, and the private unreleased changelog aligned
  with the behavior described here.
