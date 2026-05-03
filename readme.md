# Automatic Forestry Designations

Kayser's Automatic Forestry Designations is a Captain of Industry mod that adds one-click forestry designation tools to Forestry Towers.

Instead of painting forestry designations by hand, select a Forestry Tower and use **Create Designations** to scan its area and fill eligible ground automatically. Use **Clear Designations** to remove only forestry designations in that tower's area without touching mining, dumping, or leveling designations.

The mod also adds a live **Forestry Information** panel to the tower inspector, giving you a quick read on tree count, maturity, sustainable yield, estimated capacity, and growth distribution.

## Features
- **Create Designations** - scan the tower area and place forestry designations automatically.
- **Clear Designations** - remove forestry designations in the selected tower area without touching other designation types.
- **Only reachable tiles** - skip candidate tiles not reachable by vehicle pathfinding.
- **Avoid terrain designations** - skip tiles that already have mining, dumping, or leveling designations.
- **Only fertile tiles** - place designations only where the ground supports tree growth.
- **Closest tiles first** - fill reachable candidates by driving distance from the selected tower.
- **Max tiles** - cap the number of designations placed per run, with Shift/Ctrl step controls.
- **Harvest-ready marking** - optionally mark trees for harvest after creating designations, respecting the tower's vanilla Harvesting Options.
- **Forestry Information panel** - inspect tree count, estimated capacity, maturity, sustainable yield, and growth buckets.
- **Configurable defaults** - edit `AFDsettings.json` for startup defaults; most options are also adjustable per tower in the inspector.

## Notes
- Compatible with vanilla saves.
- Can be added to or removed from existing saves.
- Requires Captain of Industry `0.8.2` or newer.

## Build from source
- Install the .NET SDK with .NET Framework 4.8 targeting support
- Make sure Captain of Industry is installed, or set `CAPTAIN_INDUSTRY_MANAGED_PATH` to the game's `Captain of Industry_Data\Managed` directory
- Run `./build.ps1 -Configuration Release`
- The release zip is created in the project root

## License
MIT. See [LICENSE](LICENSE).
