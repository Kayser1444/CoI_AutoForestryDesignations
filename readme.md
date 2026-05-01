# Automatic Forestry Designations

A sister mod to AutoTerrainDesignations for Captain of Industry.

Auto Forestry Designations is planned to add Create/Clear controls and an information panel to Forestry Towers, generating valid forestry designations inside the selected tower area.

## Planned Features
- Create forestry designations inside a Forestry Tower area
- Clear forestry designations managed by the selected tower
- Show planting coverage, existing designations, trees, and stumps
- Settings for spacing and generation behavior

## Build from source
- Install the .NET SDK with .NET Framework 4.8 targeting support
- Make sure Captain of Industry is installed, or set `CAPTAIN_INDUSTRY_MANAGED_PATH` to the game's `Captain of Industry_Data\Managed` directory
- Run `./build.ps1 -Configuration Release`
- The release zip is created in the project root

## License
MIT. See [LICENSE](LICENSE).
