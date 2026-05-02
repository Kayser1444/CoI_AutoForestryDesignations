# Automatic Forestry Designations

A sister mod to AutoTerrainDesignations for Captain of Industry.

Auto Forestry Designations adds Create/Clear controls to Forestry Towers, generating valid forestry designations inside the selected tower area.

## Features
- Create forestry designations inside a Forestry Tower area
- Clear forestry designations in the selected tower area without removing other designation types
- Skip infertile tiles or tiles that already contain trees
- Skip existing mining and leveling designations by default
- Limit the number of tiles created per run
- Optionally mark harvest-ready trees in the area after creating designations, respecting the tower's Harvesting Options
- Configure defaults in `AFDsettings.json`

## Build from source
- Install the .NET SDK with .NET Framework 4.8 targeting support
- Make sure Captain of Industry is installed, or set `CAPTAIN_INDUSTRY_MANAGED_PATH` to the game's `Captain of Industry_Data\Managed` directory
- Run `./build.ps1 -Configuration Release`
- The release zip is created in the project root

## License
MIT. See [LICENSE](LICENSE).
