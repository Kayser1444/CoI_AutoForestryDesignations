v0.2.0 [unreleased]

* Added **Truck Pooling** feature for Forestry Towers: virtually pools and balances assigned trucks across all active tree harvesters of a tower.
* Added **Truck pool** vehicle assigner section (`TruckPoolTitle`) to the Forestry Tower inspector, displaying real-time pooled vehicle count and assignments.
* Added per-tower and global **Truck pooling** toggle settings with `AFDsettings.json` persistence (`truckPoolingEnabled`), including global Mod Settings tab integration.
* Added dynamic visibility observer so the "Truck pool" inspector section automatically shows when Truck Pooling is enabled and hides completely when disabled.
* Disabled `+` and `-` truck assignment buttons on Tree Harvester inspectors when Truck Pooling is enabled on their assigned tower, adding explanatory tooltips ("Truck pooling enabled. Manage assignments via {0}").
* Added automatic truck redistribution when pausing or unpausing tree harvesters or forestry towers (`Entity.OnEnabledChanged`).
* Added capacity- and physical footprint-based truck allocation priority sorting largest harvesters and largest trucks first.
* Fixed batch truck collection (`AssignTrucksToTower`) when enabling Truck Pooling to ensure all trucks from all harvesters are pooled before rebalancing, preventing accidental truck unassignments.
* Fixed vehicle assigner row visibility when starting a new game where vehicle technology is not yet unlocked but initial starting vehicles (Pickups) are owned (`stats.Owned > 0`).
* Improved Russian translation with reviewed community localization and updated translatable strings across all 7 supported languages (`de`, `en`, `es`, `it`, `pt`, `ru`, `sv`, `zh`).

v0.1.12 [released]
* Restored vehicle-prototype-based pre-allocation UI patching for tree harvesters and tree planters, including compatible modded subclasses and non-tower assignment panels.
* Fixed the pre-allocation visibility observer to use the stable inspector parent, matching vanilla and avoiding a hidden-row update cycle.
* Added a full Shift-Alt-click vehicle-order hint to the vanilla assign tooltip and aligned the confirmation wording and action button on "Order".
* Fixed the full vehicle-order tooltip to resolve its target and depot on hover, after the inspector entity provider is initialized, while always preserving the vanilla floater.
* Corrected the manifest's maximum verified game version to 0.8.6b, matching the existing Update 4.2 compatibility declaration.

v0.1.11 [released]
* Fixed: Restricted vehicle pre-allocation UI patches to ForestryTower entities only.

v0.1.10 [released]
* Updated for compatibility with Captain of Industry Update 4.2 (v0.8.6).
* Changed: Minimum supported Captain of Industry version is now 0.8.5. Older versions may still work, but are unsupported and untested.
* Fixed build script compatibility with PowerShell 5.1 / .NET Framework environments.

v0.1.9 [released]
* Fixed AFD-only patching for vehicle-construction assignment controls so mine towers are no longer affected when ATD is installed; AFD now targets forestry towers only and ATD handles the equivalent mining feature.
* Fixed overlapping depot-queue decorations between AFD and ATD when both mods are installed; each mod now only clears UI decoration state it owns.
* Changed nearest-depot selection for tower vehicle orders to use immediate straight-line distance, avoiding the confirmation delay that came from the earlier heavier terrain/path-search approach.
* Fixed plus-button behavior for tower vehicle orders so supported unlocked forestry vehicles can still be ordered even when no idle vehicle is currently assignable or owned.
* Tightened assignment-control ownership by matching vehicle proto families instead of the inspector entity-provider runtime type, preserving forestry-only and mining-only handlers without disabling order buttons.
* Added concise nearest-depot selection diagnostics for vehicle orders, including eligible depot count, chosen depot, and squared straight-line distance.
* Updated the Zoom-to-depot button icon to the MapPin icon.
* Added a pending-ticket lifecycle for pre-allocated vehicle orders, including stale-ticket expiry, cleanup after failed enqueue attempts, and cancellation handling before depot queue removal.
* Improved queue completion and desync recovery by tracking actual build-queue count changes, healing prototype mismatches during completion, and reconciling queues before saving.
* Fixed enqueue confirmation so it respects `closestDepot.CanWork` instead of relying on an outdated hard-coded queue-count check.
* Optimized depot inspector queue decoration by replacing recursive child-list allocations with a lighter helper.
* Added diagnostic logging for edge cases such as destroyed towers, failed assignment eligibility checks, missing tower references, and prototype queue mismatches.
* Fixed release ZIP entry paths to use portable forward slashes, allowing reliable extraction on Linux.
* Updated localized growth-range formatting across the supported non-English languages.

v0.1.8 [packaged]
* Added vehicle construction enqueueing for forestry tower assignments, including closest-depot by driving distance ordering, gold border highlighting on enqueued cards, and pre-assignment tooltips in the vehicle depot UI
* Added confirmation dialog when enqueuing vehicles with camera panning to the target depot and bold entity highlights
* Added click sound on confirmation popup buttons and support for canceling enqueued orders at the closest depot via the minus button when active vehicle allocation is zero
* Added Shift+Alt+Click shortcuts to directly enqueue or cancel a single vehicle, bypassing confirmation prompts and available vehicle checks
* Added button click sound when interactive growth-stage segments are clicked in the Forestry information panel
* Fixed: enqueued vehicle construction orders now correctly survive save and load

v0.1.7 | 2026-06-08 [released]
* Fixed: Optimized tree designation checks (using `HashSet` lookup instead of linear nested loops) to eliminate tower inspector UI lag

v0.1.6 | 2026-06-06
* Added AutoHelpers shared **Mod Settings** tabs for AFD defaults, game settings, scan performance, and panel defaults, with localized labels and tooltips
* Added Chinese translation
* Fixed: release package now includes `config.json`, which is required for save-backed per-tower settings storage
* Updated latest verified game version to 0.8.5

v0.1.5 | 2026-05-24
* Added save-backed per-tower settings persistence using CoI AutoHelpers JSON state storage. Tower options and panel collapsed states now survive quit/reload and remain preserved in the save even if the mod is temporarily removed.
* Kept saved tower settings compact by storing only per-tower values that differ from the current global defaults.
* Improved the Forestry information panel visuals with speckled panel card backgrounds, better growth breakdown alignment, a clearer bordered growth bar, and more consistent bottom spacing.
* Made the Trees KPI a live green display box that refreshes the current managed-tree count while the inspector is open.
* Made growth-stage segments interactive: hover highlights the corresponding trees with a brighter tint of the segment colour, and click marks or unmarks those trees for harvest.
* Opening a growth-stage segment now activates the Tree harvesting overlay so marked trees are visible; the overlay is hidden again when the inspector closes.
* Cleaned up growth-stage tooltips with named maturity brackets, multi-line formatting, simpler harvest-threshold wording, and localized hover/click instructions.
* Refined English UI labels for vanilla-style sentence case, including **Fertile tiles only**, **Reachable tiles only**, and **Maximum number of designations**.
* Added Portuguese translation.
* Updated localized UI strings and tooltips to match the revised labels and clarify that the Trees KPI is a live count of current managed trees.

v0.1.4 | 2026-05-14
* Revised Swedish, German, and Russian translations — terminology aligned with base game wording and reviewed for accuracy across all panel labels, tooltips, and status messages


v0.1.3 | 2026-05-13
* Renamed the Forestry information KPI label from **Tree maturity** to **Maturity** to fit translations
* Fixed: The **Fertile tiles only** filter now also excludes tiles blocked by buildings or other entities
* Added experimental translation framework, including Swedish, Russian, and German translations
* Minor cosmetic corrections

v0.1.2 | 2026-05-04
* **AFDsettings.json** is no longer distributed inside the mod ZIP — it is generated automatically in the mod folder on first run, populated with the current defaults and inline documentation.
* Settings file now contains a **settingsVersion** stamp; when the mod version advances the file is automatically migrated while preserving user values.
* Added **afd_save_settings** console command to write current in-memory global defaults back to **AFDsettings.json** at any time.
* Added global defaults for whether the Forestry designations and Forestry information panels start expanded or collapsed, configurable via **AFDsettings.json** or console commands.

v0.1.1 | 2026-05-03
* Added concise license and attribution notices to source files and README.

v0.1.0 | 2026-05-03
* First public release of Kayser's Automatic Forestry Designations.
* Added Create designations and Clear designations controls directly to forestry towers.
* Automatically scans the selected tower area and places valid forestry designations on eligible 4x4 tiles.
* Clear removes only forestry designations inside the selected tower area and leaves other terrain designations alone.
* Supports per-tower placement options for fertile-only tiles, reachable tiles, existing terrain designations, max tiles per run, and harvest-ready tree marking.
* Uses vehicle pathability to prefer tiles reachable by TreePlanters and TreeHarvesters.
* Sorts designation candidates by driving distance so nearby reachable ground is filled first.
* Adds a collapsible Forestry information panel with tree count, estimated capacity, tree maturity, sustainable yield, and growth breakdown.
* Estimates tree capacity from actual plantable designation tiles instead of vanilla approximate capacity.
* Uses the tower's harvest threshold and game difficulty tree maturity settings in labels, charts, and yield estimates.
* Refreshes forestry information after creating or clearing designations and when the tower harvest threshold changes.
* Includes AFDsettings.json for startup defaults.
