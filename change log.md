# Change Log

## 0.1.0
- Initial AutoForestryDesignations project scaffolded from AutoTerrainDesignations
- Added Forestry Tower Create/Clear controls for automatic forestry designation placement
- Added `AFDsettings.json` defaults for fertile-only placement, existing tree filtering, terrain designation filtering, max tiles, and harvest-ready tree marking
- Fixed: Clear only removes forestry designations in the selected tower area
- Fixed: Harvest marking respects the tower's vanilla Harvesting Options
- Fixed: **Max tiles** now chooses eligible tiles closest to the selected tower first
- Changed **Max tiles** controls so Shift steps by 10 and Ctrl steps by 100
- Added first-pass **Forestry Composition** panel with trees, average age, wood reserve, capacity, and growth distribution
- Made **Forestry Composition** collapsible and fixed its manual refresh button so it does not also toggle the panel
- Fixed **Forestry Composition** tree capacity to scan actual plantable designation tiles instead of using the vanilla approximate capacity helper
- Refresh **Forestry Composition** automatically after creating or clearing forestry designations
- Tightened **Forestry Composition** card alignment and spacing
- Added a custom product-style mature tree icon for **Forestry Composition**
- Updated **Forestry Composition** age labels and tooltips to use dynamic tree maturity age from game difficulty settings
- Added a harvest-threshold divider between below-threshold and harvest-ready growth buckets
- Simplified **Forestry Composition** UI wording to be more player-friendly (Trees, Tree Maturity, Growth Breakdown)
