# Forestry designations — Player guide

## Overview

Automatic Forestry Designations adds two buttons to the forestry tower inspector and a live **Forestry information** panel. Instead of painting forestry designations by hand you select a tower and press **Create designations** to fill its area automatically.

---

## Create designations

Scans the selected tower's area and places forestry designations on eligible tiles. The scan respects all current per-tower settings.

**Eligible tiles** (with default settings) are tiles that:

- are inside the tower's area
- do not already have any terrain designation
- are fertile (can support tree growth)

The scan runs in the background across multiple frames to avoid hitches.

---

## Clear designations

Removes all forestry designations inside the selected tower's area. Mining, dumping, and leveling designations are not affected.

---

## Per-tower settings

The following settings appear in the **Forestry designations** panel. Each can be toggled independently per tower. Global defaults are read from `AFDsettings.json` on startup.

| Setting | Default | Description |
|---|---|---|
| **Fertile tiles only** | On | Place designations only where the ground supports tree growth (not rock, sand, or ocean). |
| **Avoid terrain designations** | On | Skip tiles that overlap a mining, dumping, or leveling designation. |
| **Reachable tiles only** | On | Run a vehicle pathability check; skip tiles that harvester / planter vehicles cannot reach. |
| **Maximum number of designations** | 0 (no limit) | Cap the number of designations placed per run. Use **Shift** / **Ctrl** on the +/− buttons for larger steps. |

Per-tower settings and panel collapsed states are saved with the game. A tower only stores values that differ from the current global defaults, so changing the defaults in `AFDsettings.json` still affects towers that have not been customized.

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

`AFDsettings.json` in the mod folder sets the global defaults for all per-tower settings. Changes take effect on the next game load. If the file is missing it is regenerated with built-in defaults on startup.
