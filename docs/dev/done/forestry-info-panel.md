# Forestry Information Panel — Architecture Reference

## Feature summary

`AFD.ForestryInfoPanel.cs` implements the **Forestry Information** panel that
appears in the Forestry Tower inspector. It samples live tree data inside the
tower's area and presents a snapshot of tree count, growth maturity, sustainable
yield, estimated capacity, and a growth-bucket bar chart.

## Source file

| File | Role |
|---|---|
| `AFD.ForestryInfoPanel.cs` | Panel construction, tree data sampling, bar chart rendering, refresh callbacks |

---

## Panel layout

The panel is a `PanelWithHeader` containing a `Column` with `PANEL_GAP_PT = 2`
point gap between children. On first display it shows a prompt label
("Press refresh to scan composition"). After a refresh the prompt is replaced
with the computed stat cards.

A `ButtonIcon` (Repeat icon) triggers a refresh. The button's click handler
calls `PopulateContent(contentCol, getTower())`.

### Stat cards

After a scan, `PopulateContent` builds a `Column` with individual stat cards:

| Card | Meaning |
|---|---|
| **Trees** | Total tree count / estimated capacity |
| **Maturity** | Fraction of trees at or above the harvest-ready growth stage |
| **Sustainable yield** | Estimated wood output per harvest cycle at current stocking density |
| **Growth distribution** | Six-bucket horizontal bar chart: 5 growing buckets + 1 harvest-ready bucket |

The bar chart uses two distinct color palettes:

- **Below harvest** (greens, `s_belowHarvestColors`): `0xc6e7b2 → 0x184628`
- **Above harvest** (ambers, `s_aboveHarvestColors`): `0xc9a36a → 0x553015`
- **Unused capacity**: `s_unusedCapacityColor = 0x1a1a1a`

The palette constants are:

```
BUCKET_COUNT = 6
GROWTH_STAGE_BUCKET_COUNT = 5  (growing stages; sixth bucket is harvest-ready)
```

---

## Refresh callback pattern

`ForestryInfoPanel` uses two static dictionaries keyed by the opaque `object key`
passed to `Build`:

```csharp
Dictionary<object, Action>                   s_refreshCallbacks
Dictionary<object, Func<IAreaManagingTower?>> s_towerResolvers
```

When `Build(getTower, key)` is called:

1. `s_refreshCallbacks[key]` is set to a delegate that calls
   `PopulateContent(contentCol, getTower())`.
2. `s_towerResolvers[key]` is set to `getTower`.

When the refresh button is clicked, the stored callback is invoked.

`RefreshContent(key)` (called from `AutoForestryDesignationsApi.RefreshForestryInfoPanel`)
looks up and invokes the stored callback directly.

This pattern lets external callers force a refresh (e.g. after the inspector
switches tower) without needing a direct reference to the panel instance.

---

## Tree data sampling

`PopulateContent` iterates the tower's area using `TerrainDesignationsManager`
and queries `TreesManager` for each occupied tile. For each tree it records:

- growth stage (mapped to one of the six buckets)
- whether the tree is at or above the harvest-ready stage

From the bucket counts the panel derives:

- **total trees** and **estimated capacity** (based on the area's designatable
  tile count)
- **maturity ratio** (harvest-ready trees / total trees)
- **sustainable yield** (mature trees × wood yield per tree, clamped to
  replanting throughput)

All data is computed synchronously on the frame the refresh button is pressed.
For very large tower areas this may cause a brief hitch; batching was considered
but not implemented given the infrequent refresh cadence.
