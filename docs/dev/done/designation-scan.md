# Designation Scan — Architecture Reference

## Feature summary

`AFD.Scan.cs` implements the coroutine-driven scan pipeline that discovers
candidate forestry tiles within a tower's area and places designations according
to per-tower settings.

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
| `AvoidMiningDesignations` | `AFDsettings.json` | Skip tiles with an existing mining/dumping/leveling designation |
| `OnlyReachableTiles` | `AFDsettings.json` | Run pathability BFS; skip unreachable tiles |
| `MaxTiles` | `AFDsettings.json` | Cap the number of designations placed; `0` = no cap |
| `MarkHarvestReadyForHarvest` | `AFDsettings.json` | Set harvest-ready flag on mature-enough trees |

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

**Direct placement** (no `MaxTiles` cap, `OnlyReachableTiles` = false):

- Each eligible tile is placed immediately as it is scanned.
- No `candidates` list is allocated.

**Candidate pipeline** (`MaxTiles > 0` or `OnlyReachableTiles` = true):

- All eligible tiles are collected into a `List<DesignationCandidate>`.
- After the scan loop, candidates are sorted by `DrivingDistanceToTower`
  (ascending), falling back to `DistanceSqrToTower` for tiles where the driving
  distance is unavailable.
- The sorted list is then placed up to the `MaxTiles` cap (or fully if no cap).

### Tile filtering

Each scanned tile is checked in order:

1. **Bounding box**: must be within the tower's `Area.BoundingBoxMin/Max`.
2. **Designation grid alignment**: only origins that align to the 4×4 designation
   grid are considered.
3. **Existing designation**: tiles that already have any designation are skipped.
4. **Fertile tiles only** (if enabled): `TerrainManager.IsFertile(origin)`.
5. **Avoid tiles with trees** (if enabled): `TreesManager` is queried.
6. **Avoid mining designations** (if enabled): checks all four corners of the
   tile for non-forestry terrain designations.

### Batching

The coroutine yields every `s_batchSize` tiles scanned to avoid frame spikes:

```
BATCH_SIZE = 30          default; used when the game simulation is running
MAX_BATCH_SIZE = 200     hard ceiling
PAUSED_BATCH_MULTIPLIER = 4   batch size multiplier when the game is paused
```

The effective batch size at any given moment is `BATCH_SIZE × multiplier`,
clamped to `MAX_BATCH_SIZE`.

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
