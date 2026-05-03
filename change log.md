# Change Log

## 0.1.0
- First public release of **Kayser's Automatic Forestry Designations**.
- Adds **Create Designations** and **Clear Designations** controls directly to Forestry Towers.
- Automatically scans the selected tower area and places valid forestry designations on eligible 4x4 tiles.
- Clear removes only forestry designations inside the selected tower area and leaves other terrain designations alone.
- Supports per-tower placement options for fertile-only tiles, reachable tiles, existing terrain designations, max tiles per run, and harvest-ready tree marking.
- Uses vehicle pathability to prefer tiles reachable by TreePlanters and TreeHarvesters.
- Sorts designation candidates by driving distance so nearby reachable ground is filled first.
- Adds a collapsible **Forestry Information** panel with tree count, estimated capacity, tree maturity, sustainable yield, and growth breakdown.
- Estimates tree capacity from actual plantable designation tiles instead of vanilla approximate capacity.
- Uses the tower's harvest threshold and game difficulty tree maturity settings in labels, charts, and yield estimates.
- Refreshes forestry information after creating or clearing designations and when the tower harvest threshold changes.
- Includes `AFDsettings.json` for startup defaults.
