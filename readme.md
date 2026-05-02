# Automatic Forestry Designations

A sister mod to AutoTerrainDesignations for Captain of Industry.

Auto Forestry Designations adds Create/Clear controls to Forestry Towers, generating valid forestry designations inside the selected tower area. It also extends the Forestry Tower inspector with a live composition panel showing tree counts, maturity distribution, and yield estimates.

## Features
- **Create Designations** – scan the tower area and place forestry designations automatically
- **Clear Designations** – remove forestry designations in the selected tower area without touching other designation types
- **Forestry Composition panel** – live KPI cards for tree count, average maturity, and production capacity, plus a color-coded maturity distribution chart relative to the tower's harvest threshold
- **Only fertile tiles** – place designations only where the ground supports tree growth
- **Avoid terrain designations** – skip tiles that already have mining, dumping, or leveling designations
- **Only reachable tiles** – skip candidate tiles not reachable by vehicle pathfinding; interior holes are back-filled automatically in unlimited mode
- **Designations sorted by driving distance** – closest reachable tiles are filled first
- **Limit tiles per run** – cap the number of designations placed per Create Designations call
- **Configure defaults** in `AFDsettings.json`; most options are also adjustable per tower in the inspector

## Build from source
- Install the .NET SDK with .NET Framework 4.8 targeting support
- Make sure Captain of Industry is installed, or set `CAPTAIN_INDUSTRY_MANAGED_PATH` to the game's `Captain of Industry_Data\Managed` directory
- Run `./build.ps1 -Configuration Release`
- The release zip is created in the project root

## License
MIT. See [LICENSE](LICENSE).
