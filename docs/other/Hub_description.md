![image.png](/content-images/fa7571fbbdb5d7f6349692f02d72811329bfc886cdecffefe38d3cfdba53af4a/image.png)

# 🌲 Automatic Forestry Designations

*Forestry for Professional Captains*

## Overview

**Kayser's Automatic Forestry Designations (AFD)** automates day-to-day work around Forestry Towers. Create and clear forestry designations, inspect live tree and growth data, order vehicles, coordinate field work, and balance trucks across active Tree Harvesters.

Select a Forestry Tower and use **Create designations** to scan its area and fill eligible ground automatically. The integrated **Forestry Information** panel reports tree count, maturity, sustainable yield, and growth distribution, with interactive controls for highlighting trees and toggling harvest designations.

All tower settings are persisted in the vanilla save file. The mod can be added to or removed from games at any time. 100% open source.

For automated mining designations, see [Automatic Terrain Designations](https://coigame.com/Mod/4/Kaysers-Automatic-Terrain-Designations).

## ✨ Feature List

[🌱 **Create designations**](#create-designations)

[📊 **Forestry Information panel**](#forestry-information-panel)

[🏗️ **Vehicle ordering**](#vehicle-ordering)

[🧭 **Forestry vehicle optimizations**](#forestry-vehicle-optimizations)

[🚚 **Truck pooling**](#truck-pooling)

[⚙️ **Additional settings**](#additional-settings)

### 🌱 Create designations

![image.png](/content-images/b46ea422d98d7706b6d696e6d47785347c2b1a0aa8096118e43472f56f668b0f/image.png)

*Automatic designation scanning and placement across eligible tiles in the Forestry Tower area.*

Scan the selected Forestry Tower's area and place forestry designations automatically on eligible tiles. The scan respects configurable per-tower placement options:

- 🍃 **Fertile tiles only** — Place designations only where the ground supports tree growth.
- 🚚 **Reachable tiles only** — Skip candidate tiles that vehicles cannot reach.
- 🎯 **Target yield** — Set the desired sustainable wood production per in-game month. A scan adds eligible designations until the projected total reaches the target; ∞ means no target.
- 🗑️ **Clear designations** — Clear forestry designations instantly without affecting terrain designations.

### 📊 Forestry Information panel

![image.png](/content-images/ac90a0414910d2c87413ff619aadb5415a199a11674a1059bec026f7f8304c5d/image.png)

*Live Forestry Information panel with tree KPIs and an interactive growth distribution chart.*

Inspect real-time forestry data for the tower's assigned area:

- **Trees & capacity** — Live tree count versus the area's estimated maximum capacity.
- **Maturity** — Average tree age relative to fully grown age.
- **Sustainable yield** — Estimated monthly wood output for a fully planted and growing designation.
- **Interactive growth distribution** — A bar chart split into growth brackets. Hover to highlight matching trees in the world; click to toggle their harvest designations.

### 🏗️ Vehicle ordering

![image.png](/content-images/8e6d5efcf25edd0c3acc4a6a260554a2063f0143ce0a99d568ddda6a651c0fee/image.png)

*Order and pre-assign vehicles directly from the Forestry Tower inspector.*

Order vehicle construction directly from the Forestry Tower's vehicle assignment UI. AFD selects the closest eligible Vehicle Depot, places the order, and highlights the ordered depot card. Once built, the vehicle automatically reports to the tower.

Shift and Ctrl modifiers let you order 5 or 10 vehicles at once. Shift+Alt-click the **+** button to order directly even when a free vehicle could be assigned.

### 🧭 Forestry vehicle optimizations

![image.png](/content-images/26783cf2c3e7297f8cf8028071611568271be7779265afe92622e07a3ae9be49/image.png)

*Keep assigned forestry vehicles working in the field instead of making unnecessary return trips.*

Enable the default-on **Forestry vehicle optimizations** toggle in the world-level mod settings to coordinate Tree Planters and Tree Harvesters:

- **Predictive positioning** — Tree Harvesters stage near the best same-tower trees expected to reach the configured harvest threshold next.
- **Planting coordination** — The closest loaded, idle Tree Planter reserves the future planting spot for a Tree Harvester's target, preventing several planters from flocking to one tile.
- **Field waiting** — When no current or future target exists, enabled assigned vehicles stay put instead of returning to the tower.
- **Vanilla work first** — Active harvesting, planting, refueling, and resupply jobs always take priority over staging.

### 🚚 Truck pooling

![image.png](/content-images/a24dcd590dbe254b3b3c55d865ce84b961bf414c8ea7e83862b07f34b0fc554c/image.png)

*Truck pooling controls in the Forestry Tower inspector.*

![image.png](/content-images/a32e5c6de86060d96a4862834955f583b5112a07140070e871374e8f0ccabf97/image.png)

*Pooled trucks balanced across the tower's active Tree Harvesters.*

Enable **Truck pooling** to manage trucks at the tower level. Trucks assigned to the Forestry Tower are pooled and automatically distributed to active Tree Harvesters based on capacity and physical footprint. Pausing, unpausing, or changing the harvester setup dynamically rebalances allocations.

### ⚙️ Additional settings

[insert screenshot here]

*AFD's Mod Settings tab provides world-wide behavior, tower defaults, and interface preferences.*

Open the AFD tab in the Mod Settings window for controls that are not available from an individual Forestry Tower:

- **Override terrain designations** — Allow forestry designations to replace existing mining, dumping, or leveling designations. Disabled by default.
- **Avoid tiles with trees** — Skip tiles that already contain trees when creating designations.
- **Mark harvest-ready trees** — Automatically mark trees that meet the tower's harvest threshold after creating designations.
- **Forestry vehicle optimizations** — Enable or disable coordinated field waiting and predictive positioning for Tree Planters and Tree Harvesters.
- **Tower panel defaults** — Choose whether the Forestry Designations and Forestry Information panels start collapsed.
- **Tower settings defaults** — Set the initial fertility, flat-terrain, reachability, Truck pooling, and Target yield preferences, then optionally save them as the configuration for new games.

---

Chop away!

PS. Leave a 👍 or a ⭐️ if you found this mod useful.
