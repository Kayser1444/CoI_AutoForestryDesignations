# 🌲 Automatic Forestry Designations

Kayser's Automatic Forestry Designations is the long-awaited sister mod to **Automatic Terrain Designations**, bringing the same one-click quality-of-life spirit to forestry towers.

Download the latest release: https://github.com/Kayser1444/CoI_AutoForestryDesignations/releases/latest

Instead of painting forestry designations by hand, select a forestry tower and use **Create designations** to scan its area and fill eligible ground automatically. Use **Clear designations** to remove only forestry designations in that tower's area without touching mining, dumping, or leveling designations.

The mod also adds a live **Forestry information** panel to the tower inspector, giving you a quick read on tree count, maturity, sustainable yield, estimated capacity, and growth distribution.

## ⚙️ Features
- 🌱 **Create designations** - scan the tower area and place forestry designations automatically.
- 🗑️ **Clear designations** - remove forestry designations in the selected tower area without touching other designation types.
- 🚚 **Reachable tiles only** - skip candidate tiles not reachable by vehicle pathfinding.
- 📐 **Avoid terrain designations** - skip tiles that already have mining, dumping, or leveling designations.
- 🍃 **Fertile tiles only** - place designations only where the ground supports tree growth.
- 📍 **Closest tiles first** - fill reachable candidates by driving distance from the selected tower.
- 🔢 **Maximum number of designations** - cap the number of designations placed per run, with Shift/Ctrl step controls.
- 📊 **Forestry information panel** - inspect tree count, estimated capacity, maturity, sustainable yield, and growth buckets.
- 🏗️ **Vehicle enqueueing** - order vehicle construction directly from the forestry tower's assignment UI, automatically routed to the closest depot by driving distance. Ordered vehicles are pre-assigned and will automatically join the tower once completed.
- 🔧 **Configurable defaults** - edit `AFDsettings.json` for startup defaults; most options are also adjustable per tower in the inspector.

## 📋 Notes
- Compatible with vanilla saves.
- Can be added to or removed from existing saves.
- Requires Captain of Industry `0.8.5` or newer; older versions may work but are not supported or tested.

## 💾 Installation
- Download the latest version of the mod from GitHub Releases
- Extract the mod folder into your Captain of Industry mods directory (`%AppData%\Captain of Industry\Mods`)
- Enable the mod when loading or starting a new game
- Can be safely added and removed from saves

## 🛠️ Build from source
- Install the .NET SDK with .NET Framework 4.8 targeting support
- Make sure Captain of Industry is installed, or set `CAPTAIN_INDUSTRY_MANAGED_PATH` to the game's `Captain of Industry_Data\Managed` directory
- Run `./build.ps1 -Configuration Release`
- The release zip is created in the project root

## 📜 License
MIT. See [LICENSE](LICENSE).

## Attribution and trademarks

Auto Forestry Designations is an unofficial, community-made mod for Captain of Industry.

Captain of Industry, MaFi Games, and related names, trademarks, game code, and
assets are the property of MaFi Games. This mod is not affiliated with,
endorsed by, or sponsored by MaFi Games.

This repository is intended to contain only original mod code and configuration,
licensed under the MIT License. It does not intentionally include Captain of
Industry game code, game assets, or other MaFi Games intellectual property. If
any such material is found to have been included by mistake, I intend to correct
it promptly upon discovery or notice.
