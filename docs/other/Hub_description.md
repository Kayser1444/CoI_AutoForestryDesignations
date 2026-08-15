![image.png](/content-images/fa7571fbbdb5d7f6349692f02d72811329bfc886cdecffefe38d3cfdba53af4a/image.png)

# 🌲 Automatic Forestry Designations

*Forestry for Professional Captains*

### Overview

**Kayser's Automatic Forestry Designations** is the long-awaited sister mod to *Automatic Terrain Designations*, bringing the same one-click quality-of-life spirit to Forestry Towers.

Instead of painting forestry designations by hand, select a Forestry Tower and use **Create designations** to scan its area and fill eligible ground automatically.

Use **Clear designations** to remove all forestry designations in the tower's area without touching mining, dumping, or leveling designations.

The mod also adds a live **Forestry Information** panel to the tower inspector, giving you a quick read on tree count, maturity, sustainable yield, and growth distribution. The Growth distribution chart is interactive and allows highlighting and toggling harvesting designations for each bracket.

**Order and pre-assign vehicles** directly from the forestry tower's vehicle assignment UI, automatically ordered from the closest eligible depot. Vehicles ordered this way will automatically assign themselves to the tower once construction completes.

**Truck pooling** virtually pools and balances assigned trucks across all active tree harvesters of a tower, ensuring efficient vehicle distribution without manual harvester management.

All tower settings are persisted in the vanilla save file. The mod can be added and removed from games at any time. 100% open source.

## ✨ Feature List

- [🌱 Create designations](#create-designations)
- [📊 Forestry Information panel](#forestry-information-panel)
- [🏗️ Vehicle ordering](#vehicle-ordering)
- [🧭 Forestry vehicle optimizations](#forestry-vehicle-optimizations)
- [🚚 Truck pooling](#truck-pooling)

### 🌱 Create designations

![image.png](/content-images/b46ea422d98d7706b6d696e6d47785347c2b1a0aa8096118e43472f56f668b0f/image.png)

*Automatic designation scanning and placement across eligible tiles in the tower area.*

Scan the selected tower's area and place forestry designations automatically on eligible tiles. The scan respects configurable per-tower placement options:

- 🍃 **Fertile tiles only** – place designations only where the ground supports tree growth.
- 🚚 **Reachable tiles only** – skip candidate tiles not reachable by vehicle pathfinding.
- 📐 **Avoid terrain designations** – skip tiles that already have mining, dumping, or leveling designations.
- 🔢 **Max number of designations** – cap the number of designations placed per run, with Shift/Ctrl step controls. Tiles are filled based on driving distance to the tower.
- 🗑️ **Clear designations** – clear forestry designations instantly without affecting other terrain designations.

### 📊 Forestry Information panel

![image.png](/content-images/ac90a0414910d2c87413ff619aadb5415a199a11674a1059bec026f7f8304c5d/image.png)

*Live Forestry Information panel with tree KPIs and an interactive growth distribution chart.*

Inspect real-time forestry data for the tower's assigned area:

- **Trees & Capacity** – live tree count versus estimated maximum capacity for the area.
- **Maturity** – average age of trees in the designation in relation to the fully grown age.
- **Sustainable yield** – estimated wood output per month, assuming fully planted and growing designation.
- **Interactive growth distribution** – bar chart split into growth brackets. Hovering highlights trees of that bracket in-world, and clicking toggles harvest designations for those trees.

### 🏗️ Vehicle ordering

![image.png](/content-images/8e6d5efcf25edd0c3acc4a6a260554a2063f0143ce0a99d568ddda6a651c0fee/image.png)

*Order and pre-assign vehicles directly from the forestry tower inspector.*

Order vehicle construction directly from the forestry tower's vehicle assignment UI. The mod automatically selects the closest eligible vehicle depot, places the order, and highlights the ordered depot card. Once built, vehicles automatically report to the tower.

Shift- and Ctrl- modifiers work, so you can order 5 or 10 new vehicles at once, if desired.

Shift-Alt-click the + button to bypass the free vehicles and directly order a vehicle, even if there are free vehicles that could be assigned.

### 🧭 Forestry vehicle optimizations

*Keep assigned forestry vehicles working in the field instead of making unnecessary return trips.*

Enable the default-on **Forestry vehicle optimizations** toggle in the world-level mod settings to coordinate planters and harvesters:

- **Predictive positioning** – harvesters stage near the best same-tower trees expected to reach the configured harvest threshold next.
- **Planting coordination** – the closest loaded, idle planter reserves the future planting spot for a harvester's target, preventing multiple planters from flocking to one tile.
- **Field waiting** – when no current or future target exists, enabled assigned vehicles stay put instead of driving back to the tower.
- **Vanilla work first** – active harvesting, planting, refuelling, and resupply jobs always take priority over staging.

### 🚚 Truck pooling

![image.png](/content-images/a24dcd590dbe254b3b3c55d865ce84b961bf414c8ea7e83862b07f34b0fc554c/image.png)

![image.png](/content-images/a32e5c6de86060d96a4862834955f583b5112a07140070e871374e8f0ccabf97/image.png)

*Virtually pool and balance assigned trucks across all tree harvesters of a forestry tower.*

Enable **Truck pooling** to manage vehicles at the tower level. Vehicles assigned to the tower are pooled and automatically distributed to active tree harvesters based on capacity and physical footprint. Pausing, unpausing, or adjusting harvesters dynamically rebalances truck allocations so your forestry operation always runs at peak efficiency.

---

Chop away!

PS. Leave a 👍 or a ⭐️ if you found this mod useful.
