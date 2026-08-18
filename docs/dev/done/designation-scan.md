# Designation Scan — Architecture Reference

## Feature summary

`AFD.Scan.cs` implements the coroutine-driven scan pipeline that discovers
candidate forestry tiles within a tower's area and places designations according
to world and per-tower settings.

## Source files

| File | Role |
|---|---|
| `AFD.Scan.cs` | Scan coroutine, candidate collection, filtering, sorting, pathability BFS |
| `AFD.State.cs` | Static state fields, per-tower settings, initialization |
| `AFD.TowerSettingsConfigPersistence.cs` | Save-backed JSON persistence for per-tower settings |
| `AFD.Ticker.cs` | Coroutine host; dispatches `CreateDesignationsCoroutine` |

---

## Core model

### `DesignationCandidate`

A `readonly struct` collected during the scan phase when the candidate pipeline
is active:

| Field | Type | Purpose |
|---|---|---|
| `Origin` | `Tile2i` | Designation grid origin |
| `Data` | `DesignationData` | Precomputed designation data |
| `DistanceSqrToTower` | `long` | Euclidean squared distance for fallback sorting |
| `DrivingDistanceToTower` | `int?` | Pathfinding distance; `null` if not computed or not reachable |

### `AFDTowerSettings`

Per-tower settings snapshot. A new instance is read from the current global
defaults each time `GetOrCreateTowerSettings` is called for a tower that has no
saved overrides.

| Property | Default source | Meaning |
|---|---|---|
| `OnlyFertileTiles` | `AFDsettings.json` | Skip tiles that do not support tree growth |
| `AvoidTilesWithTrees` | `AFDsettings.json` | Skip tiles that already contain a tree |
| `AvoidFlatTiles` | `AFDsettings.json` | Skip 4×4 candidates whose four `HeightTilesF` corner heights are within the game's 0.0625-tile surface tolerance of one integer height |
| `OnlyReachableTiles` | `AFDsettings.json` | Run pathability BFS; skip unreachable tiles |
| `TargetYield` | `AFDsettings.json` | Fill toward projected sustainable wood/month; `0` = no target |
| `MaxTiles` | legacy settings | Hidden cap retained for old saves until Target yield is explicitly changed |
| `MarkHarvestReadyForHarvest` | `AFDsettings.json` | Set harvest-ready flag on mature-enough trees |

`OverrideTerrainDesignations` is a world setting stored in the mod cache. It
allows forestry designations to replace existing mining, dumping, or leveling
designations and defaults to `false`.

Tower settings are persisted through CoI AutoHelpers' JSON state storage using
the vanilla mod config save chunk. The saved root object currently contains
`schemaVersion` and `towerSettings`. Each `towerSettings` entry is keyed by
`entityId` and is sparse: fields are written only when they differ from the
current global defaults. Loading starts from the current defaults and applies
any saved fields, which keeps older full records compatible and lets default
changes flow through to towers that have no overrides.

---

## Scan pipeline

### Entry point

```csharp
public static void CreateDesignationsForTower(IAreaManagingTower tower)
```

Validates state and starts `CreateDesignationsCoroutine` as a Unity coroutine
on the stored `s_coroutineHost`.

### Two pipeline modes

The scan has two operating modes selected at runtime:

**Direct candidate collection** (no reachability or finite-target planning):

- Eligible tiles are collected as pending `DesignationData` while the scan is
  sliced by the 10/30 ms planning budget.
- The pending set is committed through vanilla's bulk input command after
  planning completes.

**Candidate pipeline** (finite `TargetYield`, legacy `MaxTiles > 0`,
`OnlyReachableTiles` = true, or `AvoidFlatTiles` = true):

- All eligible tiles are collected into a `List<DesignationCandidate>`.
- After the scan loop, candidates are sorted by `DrivingDistanceToTower`
  (ascending), falling back to `DistanceSqrToTower` for tiles where the driving
  distance is unavailable.
- With a finite Target yield, the planner builds one spacing-aware sustainable-
  yield projection from existing managed capacity. It selects candidates in
  priority order and updates that projection incrementally after each selected
  designation. Planning stops as soon as the target is reached; only unavoidable
  single-designation granularity may overshoot.
- With a legacy Max tiles cap, the sorted list is placed up to that cap. With no
  finite target or legacy cap, all eligible candidates are placed.

### Tile filtering

Each scanned tile is checked in order:

1. **Bounding box**: must be within the tower's `Area.BoundingBoxMin/Max`.
2. **Designation grid alignment**: only origins that align to the 4×4 designation
   grid are considered.
3. **Existing designation**: tiles that already have any designation are skipped
   unless the world-level `OverrideTerrainDesignations` setting is enabled.
4. **Fertile tiles only** (if enabled): `TerrainManager.IsFertile(origin)`.
5. **Avoid tiles with trees** (if enabled): `TreesManager` is queried.
6. **Terrain designations**: skips tiles with existing non-forestry terrain
   designations unless the world-level `OverrideTerrainDesignations` setting is
   enabled.
7. **Avoid flat tiles** (if enabled): the four `HeightTilesF` designation-corner
   heights must not all be within the game's surface-height tolerance of one
   shared integer height.

Target-yield planning is fail-closed when the sustainable-yield estimate cannot
be evaluated. Its estimator builds one spatially indexed capacity projection,
then updates it as each designation is selected for the pending plan; it never falls back to
unlimited placement. The setting is only read when Create designations is
explicitly invoked, and the scan never removes existing designations.

### Slicing and commit

Candidate scanning, pathability work, and finite-target estimation use elapsed-
time budgets per rendered frame:

```
TARGET_PLANNING_PLAY_BUDGET_MS = 10
TARGET_PLANNING_PAUSED_BUDGET_MS = 30
```

After planning, AFD submits the complete set through vanilla's
`AddTerrainDesignationsCmd`. This applies mutations at the simulation-safe input
boundary instead of changing designation collections from a coroutine between
simulation ticks. The finished set appears together without a progress toast.

---

## Pathability search

When `OnlyReachableTiles` is enabled a BFS reachability map is computed once per
scan, before the tile loop:

```
PATHABILITY_SEARCH_MARGIN_TILES = 96
MAX_PATHABILITY_SEARCH_TILES = 250,000
```

The BFS starts from the tower's footprint tile and expands using
`IVehiclePathFindingManager` with the standard vehicle params stored in
`s_standardVehiclePathFindingParams`. Tiles not reached by the BFS are filtered
out of the candidate list.

---

## Runtime state fields

These fields live in `AFD.State.cs` and are set once during `Initialize`:

| Field | Purpose |
|---|---|
| `s_desigManager` | `TerrainDesignationsManager` — creates and removes designations |
| `s_forestryProto` | `TerrainDesignationProto` for the forestry designation type |
| `s_coroutineHost` | `MonoBehaviour` used to run scan coroutines |
| `s_protosDb` | `ProtosDb` for proto lookups |
| `s_worldMapManager` | Used to locate the tower on the terrain grid |
| `s_vehiclePathFindingManager` | Used for reachability BFS |
| `s_standardVehiclePathFindingParams` | Params snapped from a representative vehicle |

All fields are nullable. If any required field is null when a scan is triggered,
`CreateDesignationsCoroutine` exits early via `yield break`.
