# Forestry designations — Player guide

## Overview

Automatic Forestry Designations adds two buttons to the forestry tower inspector and a live **Forestry information** panel. Instead of painting forestry designations by hand you select a tower and press **Create designations** to fill its area automatically.

---

## Create designations

Scans the selected tower's area and places forestry designations on eligible tiles. The scan respects the world-level designation behavior and current per-tower settings.

**Eligible tiles** (with default settings) are tiles that:

- are inside the tower's area
- do not already have any terrain designation, unless **Override terrain designations** is enabled
- are fertile (can support tree growth)

If **Avoid flat tiles** is enabled, flat 4×4 candidates are also skipped when
all four designation-corner heights are within the game's surface-height
tolerance of the same integer height.

The scan runs in the background across multiple frames to avoid hitches.

---

## Clear designations

Removes all forestry designations inside the selected tower's area. Mining, dumping, and leveling designations are not affected.

---

## World settings

The following setting is available in the **Designation behaviors** section of
the AFD mod settings and is saved in the world mod cache:

| Setting | Default | Description |
|---|---|---|
| **Override terrain designations** | Off | Allow forestry designations to replace existing mining, dumping, or leveling designations. |

## Per-tower settings

The following settings appear in the **Forestry designations** panel. Each can be toggled independently per tower. Global defaults are read from `AFDsettings.json` on startup.

| Setting | Default | Description |
|---|---|---|
| **Fertile tiles only** | On | Place designations only where the ground supports tree growth (not rock, sand, or ocean). |
| **Avoid flat tiles** | Off | Use only uneven, rough tiles for forestry. (This preserves flat tiles for buildings.) |
| **Reachable tiles only** | On | Run a vehicle pathability check; skip tiles that harvester / planter vehicles cannot reach. |
| **Target yield** | ∞ (no target) | Fill eligible managed capacity until projected sustainable wood production reaches this amount per in-game month. The final scan may overshoot. **Shift** / **Ctrl** change the target by 10 / 100. |

Settings only affect a later explicit **Create designations** action. Existing
designations are never removed automatically. The world setting, per-tower settings, and panel
collapsed states are saved with the game. A tower only stores values that differ
from the current global defaults, so changing the defaults in `AFDsettings.json`
still affects towers that have not been customized.

Target yield uses the forestry tower's existing tree-type and harvest settings,
including spacing-aware planting capacity. It counts only managed trees and
managed future planting capacity. **∞** is stored as `0` and means no target,
which restores the normal unlimited eligible scan. Old saves retain a hidden
legacy maximum-designations limit; the Target yield tooltip discloses it, and
explicitly changing Target yield (including setting it to ∞) clears it.

---

## Forestry information panel

The **Forestry information** panel gives a live read of tree data inside the tower's area. Press the refresh button (↺) to sample current data.

| Stat | Meaning |
|---|---|
| **Trees** | Current tree count versus estimated maximum capacity for the area. |
| **Maturity** | Fraction of trees that are at or above the harvest-ready growth stage. |
| **Sustainable yield** | Estimated wood output per harvest cycle at current stocking density. |
| **Growth distribution** | Bar chart split into five growing buckets (greens) and one harvest-ready bucket (amber). Unfilled capacity is shown in dark grey. |

---

## Configuration file

`AFDsettings.json` in the mod folder sets global defaults for per-tower settings. World-level settings are saved in the vanilla mod cache. Changes to the settings file take effect on the next game load. If the file is missing it is regenerated with built-in defaults on startup.
