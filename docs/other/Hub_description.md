# 🌲 Automatic Forestry Designations

**Kayser's Automatic Forestry Designations** is the long-awaited sister mod to *Automatic Terrain Designations*, bringing the same one-click quality-of-life spirit to Forestry Towers.

Instead of painting forestry designations by hand, select a Forestry Tower and use **Create designations** to scan its area and fill eligible ground automatically.

Use **Clear designations** to remove all forestry designations in the tower's area without touching mining, dumping, or leveling designations.

The mod also adds a live **Forestry Information** panel to the tower inspector, giving you a quick read on tree count, maturity, sustainable yield, and growth distribution. The Growth distribution chart is interactive and allows highlighting and toggling harvesting designations for each bracket.

**Order and pre-assign vehicles** directly from the forestry tower's vehicle assignment UI, automatically ordered from the closest eligible depot. Vehicles ordered this way will automatically assign themselves to the tower once construction completes.

**Truck pooling** virtually pools and balances assigned trucks across all active tree harvesters of a tower, ensuring efficient vehicle distribution without manual harvester management.

All tower settings are persisted in the vanilla save file. The mod can be added and removed from games at any time. 100% open source.

## ✨ Feature List

- [🌱 Create designations](#create-designations)
- [🗑️ Clear designations](#clear-designations)
- [📊 Forestry Information panel](#forestry-information-panel)
- [🏗️ Vehicle ordering](#vehicle-ordering)
- [🚚 Truck pooling](#truck-pooling)

### 🌱 Create designations

[Screenshot: Create designations]

*Automatic designation scanning and placement across eligible tiles in the tower area.*

Scan the selected tower's area and place forestry designations automatically on eligible tiles. The scan respects configurable per-tower placement options:

- 🚚 **Only reachable tiles** – skip candidate tiles not reachable by vehicle pathfinding.
- 📐 **Avoid terrain designations** – skip tiles that already have mining, dumping, or leveling designations.
- 🍃 **Only fertile tiles** – place designations only where the ground supports tree growth.
- 📍 **Closest tiles first** – fill reachable candidate tiles by driving distance from the selected tower.
- 🔢 **Max tiles** – cap the number of designations placed per run, with Shift/Ctrl step controls.

### 🗑️ Clear designations

[Screenshot: Clear designations]

*Clear forestry designations instantly without affecting other terrain designations.*

Remove all forestry designations within the selected tower's area in a single click, leaving mining, dumping, and leveling designations untouched.

### 📊 Forestry Information panel

![forestry-information-panel.png](/content-images/f9f826d1fc367d94c6079a03a3245ecd416223278a4055077fabc2d530b77b70/image.png)

*Live Forestry Information panel with tree KPIs and an interactive growth distribution chart.*

Inspect real-time forestry data for the tower's assigned area:

- **Trees & Capacity** – live tree count versus estimated maximum capacity for the area.
- **Maturity** – percentage of trees at or above harvest-ready growth stages.
- **Sustainable yield** – estimated wood output per harvest cycle.
- **Interactive growth distribution** – bar chart split into growth brackets. Hovering highlights trees of that bracket in-world, and clicking toggles harvest designations for those trees.

### 🏗️ Vehicle ordering

[Screenshot: Vehicle ordering]

*Order and pre-assign vehicles directly from the forestry tower inspector.*

Order vehicle construction directly from the forestry tower's vehicle assignment UI. The mod automatically selects the closest eligible vehicle depot, places the order, and highlights the ordered depot card. Once built, vehicles automatically report to the tower.

### 🚚 Truck pooling

[Screenshot: Truck pooling]

*Virtually pool and balance assigned trucks across all tree harvesters of a forestry tower.*

Enable **Truck pooling** to manage vehicles at the tower level. Vehicles assigned to the tower are pooled and automatically distributed to active tree harvesters based on capacity and physical footprint. Pausing, unpausing, or adjusting harvesters dynamically rebalances truck allocations so your forestry operation always runs at peak efficiency.

---

Chop away!

PS. Leave a 👍 or a ⭐️ if you found this mod useful.
