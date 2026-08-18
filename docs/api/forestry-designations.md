# AFD public API — Forestry designations

This document covers the public API surface of AutoForestryDesignations.

All members are `static` on `AutoForestryDesignations.AutoForestryDesignationsApi`.

---

## Guard

```csharp
bool IsInitialized
```

Returns `true` once AFD has finished initializing. Check this before calling any
other API method from early-init code.

---

## Designation operations

```csharp
void CreateDesignationsForTower(IAreaManagingTower tower)
```

Scans the tower's area and creates forestry designations according to the world
designation behavior setting and the tower's current per-tower settings (fertile
tiles only, avoid flat tiles, target yield, and so on). The scan runs as a
coroutine and may span multiple frames. A finite target plans toward the tower's
projected sustainable wood output; ∞ means no target. Existing designations are
not removed unless the world-level **Override terrain designations** setting is
enabled.

```csharp
void ClearDesignationsForTower(IAreaManagingTower tower)
```

Removes all AFD-placed forestry designations inside the tower's area.

---

## Panel builders

Use these to embed AFD's inspector panels inside a custom inspector layout.

```csharp
PanelWithHeader BuildDesignationPanel(Func<IAreaManagingTower?> getTower, object key)
```

Builds the **Forestry designations** panel (create / clear buttons and per-tower
settings toggles). Pass an opaque `key` (typically your inspector instance) that
is used to identify this panel for refresh operations.

```csharp
void RefreshDesignationPanel(object key)
```

Refreshes the display values of a previously built Forestry designations panel.
Call this when the inspector activates or switches to a different tower.

```csharp
PanelWithHeader BuildForestryInfoPanel(Func<IAreaManagingTower?> getTower, object key)
```

Builds the **Forestry information** panel (tree count, maturity, sustainable
yield, estimated capacity, growth distribution). The panel's built-in refresh
button triggers a re-scan of tree data.

```csharp
void RefreshForestryInfoPanel(object key)
```

Refreshes the display values of a previously built Forestry information panel.

Pass the same `key` to all panel-builder and refresh calls for a given inspector
instance.

---

## What is not public API

The following are intentionally internal and should not be treated as stable
integration points:

- the designation scan coroutine and its filtering pipeline
- pathability search internals (`s_pathabilitySearchDirections`, BFS logic)
- per-tower `AFDTowerSettings` field access and mutation
- tree data sampling and growth bucket calculation internals
- the parsing details of `AFDsettings.json`
- `AfdLocalization` static class members

If you need integration hooks in one of those areas, add or request a dedicated
API entry point instead of binding to internal classes.
