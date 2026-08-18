# Target yield and avoid flat tiles — implementation reference

## Behavior

The Forestry designations panel exposes two per-tower controls, while terrain
designation replacement is a separate world setting:

- **Avoid flat tiles** rejects a 4×4 candidate only when its four terrain
  corner heights are all within the game's surface-height tolerance (`0.0625`
  terrain tile) of the same integer height. It defaults off and never removes
  existing designations.
- **Target yield** is an integer sustainable wood/month floor. `0` is stored as
  no target and rendered as `∞`; finite planning builds one spacing-aware
  capacity projection and updates it as designations are placed. Only
  unavoidable single-designation granularity may overshoot. The default is
  `∞`.

Both settings are read only by an explicit **Create designations** action.
Existing reachability, fertility, and tree filters remain authoritative, and
candidate priority remains driving distance followed by geometric distance.
The world-level **Override terrain designations** setting controls whether an
existing mining, dumping, or leveling designation may be replaced.

## Shared estimator

`ForestryInfoPanel.TryEstimateProjectedYield` remains the synchronous shared
seam used by the information panel, while
`ForestryInfoPanel.ProjectedYieldEstimateWork` drives the same calculation as a
resumable planner seam. It uses the Forestry Tower's existing vanilla tree-type
configuration, harvest threshold, tree yield/maturity data, spacing, managed
trees, and managed designation origins. Accepted future-tree positions are held
in a spacing-sized spatial index, and each successfully placed designation is
added to that same projection without rebuilding prior work.

Finite planning uses elapsed-time slices of 10 ms while playing and 30 ms while
paused. The completed plan is committed through vanilla's simulation-safe bulk
designation command, so a large operation shows confirmation when the
designation set appears. Planning stops below target
when the estimator is unavailable, never changes to unlimited mode, and never
removes already committed designations.

## Persistence and migration

The save-backed tower settings schema is version 3. Existing schema 1 and 2
records continue to load. New world field `overrideTerrainDesignations` replaces
the old inverted `avoidMiningDesignations` field; old per-tower values are
ignored because the replacement policy is now world-owned. `avoidFlatTiles` and
`targetYield` remain per-tower-capable fields.

`MaxTiles` remains serialized as a hidden legacy field. Old global and per-tower
limits are preserved and disclosed in the Target yield tooltip while the target
is unset. Explicitly setting Target yield, including `∞`, clears that tower's
legacy limit. The public AFD API signatures are unchanged.

## Verification

- `dotnet build AutoForestryDesignations.sln -c Debug`
- Parse validation for every file in `translations/`.
- `git diff --check`.
- In-game regression: flat and mixed-height areas, already-met and unmet finite
  targets, target increases, legacy Max tiles migration, save/reload, and
  per-tower isolation.
