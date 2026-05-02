# Change Log

## 0.1.0
- Initial AutoForestryDesignations project scaffolded from AutoTerrainDesignations
- Added Forestry Tower Create/Clear controls for automatic forestry designation placement
- Added `AFDsettings.json` defaults for fertile-only placement, existing tree filtering, terrain designation filtering, max tiles, and harvest-ready tree marking
- Fixed: Clear only removes forestry designations in the selected tower area
- Fixed: Harvest marking respects the tower's vanilla Harvesting Options
- Fixed: **Max tiles** now chooses eligible tiles closest to the selected tower first
- Changed **Max tiles** controls so Shift steps by 10, Ctrl steps by 100, and the display reserves four digits
