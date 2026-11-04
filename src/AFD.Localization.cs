using Mafi.Localization;

namespace AutoForestryDesignations
{
    internal static class AfdLocalization
    {
        public static LocStr ShowUnownedVehicles =
            Loc.Str("AutoForestryDesignations.PanelVehiclesShowUnowned", "Show unlocked vehicle types you do not own so you can order them from this tower. Owned vehicles and this tower's queued orders are always shown.", "Tooltip for the union symbol button in vehicle panel headers, when unowned types are hidden.");
        public static LocStr HideUnownedVehicles =
            Loc.Str("AutoForestryDesignations.PanelVehiclesHideUnowned", "Hide vehicle types you do not own. Owned vehicles and this tower's queued orders remain visible.", "Tooltip for the intersection symbol button in vehicle panel headers, when unowned types are shown.");
        // --- Panel titles ---
        public static LocStr ForestryDesignationsTitle = Loc.Str(
            "AutoForestryDesignations.ForestryDesignationsTitle",
            "Forestry designations",
            "Title of the Forestry designations inspector panel.");

        public static LocStr ForestryInformationTitle = Loc.Str(
            "AutoForestryDesignations.ForestryInformationTitle",
            "Forestry information",
            "Title of the Forestry information inspector panel.");

        public static LocStr ForestryDesignationsTitleTipFmt = Loc.Str(
            "AutoForestryDesignations.ForestryDesignationsTitleTipFmt",
            "Create automatic forestry designations. [Kayser's Automatic Forestry Designations v{0}]",
            "Tooltip on the Forestry designations inspector panel header. {0} = mod version.");

        public static LocStr ForestryInformationTitleTipFmt = Loc.Str(
            "AutoForestryDesignations.ForestryInformationTitleTipFmt",
            "Current trees and projected wood output in this tower's forestry area. [Kayser's Automatic Forestry Designations v{0}]",
            "Tooltip on the Forestry information inspector panel header. {0} = mod version.");

        // --- Buttons ---
        public static LocStr CreateDesignationsBtn = Loc.Str(
            "AutoForestryDesignations.CreateDesignationsBtn",
            "Create designations",
            "Label on the Create designations button.");

        public static LocStr CreateDesignationsBtnTip = Loc.Str(
            "AutoForestryDesignations.CreateDesignationsBtnTip",
            "Scan the tower area and place forestry designations.",
            "Tooltip on the Create designations button.");

        public static LocStr ClearDesignationsBtnTip = Loc.Str(
            "AutoForestryDesignations.ClearDesignationsBtnTip",
            "Clear all forestry designations in this tower's area.",
            "Tooltip on the Clear designations (trash) button.");

        public static LocStr RefreshCompositionTip = Loc.Str(
            "AutoForestryDesignations.RefreshCompositionTip",
            "Refresh forestry composition",
            "Tooltip on the refresh button in the Forestry information panel.");

        // --- Bool toggle display values ---
        public static LocStr BoolYes = Loc.Str(
            "AutoForestryDesignations.BoolYes",
            "Yes",
            "Displayed in a toggle control when the option is enabled.");

        public static LocStr BoolNo = Loc.Str(
            "AutoForestryDesignations.BoolNo",
            "No",
            "Displayed in a toggle control when the option is disabled.");

        // --- Setting labels and tooltips ---
        public static LocStr OnlyFertileTilesLabel = Loc.Str(
            "AutoForestryDesignations.OnlyFertileTilesLabel",
            "Fertile tiles only",
            "Label for the Fertile Tiles Only toggle setting.");

        public static LocStr OnlyFertileTilesTip = Loc.Str(
            "AutoForestryDesignations.OnlyFertileTilesTip",
            "Place designations only where the ground is valid for tree planting: fertile terrain (not rock, sand, or ocean) and not occupied by a building or other entity.",
            "Tooltip for the Fertile Tiles Only toggle setting.");

        public static LocStr OnlyReachableTilesLabel = Loc.Str(
            "AutoForestryDesignations.OnlyReachableTilesLabel",
            "Reachable tiles only",
            "Label for the Reachable Tiles Only toggle setting.");

        public static LocStr OnlyReachableTilesTip = Loc.Str(
            "AutoForestryDesignations.OnlyReachableTilesTip",
            "When enabled, skip designation tiles that are not reachable by vehicle pathability from the tower area.",
            "Tooltip for the Reachable Tiles Only toggle setting.");

        public static LocStr OverrideTerrainDesignationsLabel = Loc.Str(
            "AutoForestryDesignations.OverrideTerrainDesignationsLabel",
            "Override terrain designations",
            "Label for the Override Terrain Designations toggle setting.");

        public static LocStr OverrideTerrainDesignationsTip = Loc.Str(
            "AutoForestryDesignations.OverrideTerrainDesignationsTip",
            "Allow forestry designations to replace existing terrain designations, including mining, dumping, or leveling. Default: off.",
            "Tooltip for the Override Terrain Designations toggle setting.");

        public static LocStr AvoidFlatTilesLabel = Loc.Str(
            "AutoForestryDesignations.AvoidFlatTilesLabel",
            "Avoid flat tiles",
            "Label for the Avoid flat tiles toggle setting.");

        public static LocStr AvoidFlatTilesTip = Loc.Str(
            "AutoForestryDesignations.AvoidFlatTilesTip",
            "Use only uneven, rough tiles for forestry. (This preserves flat tiles for buildings.)",
            "Tooltip for the Avoid flat tiles toggle setting.");

        public static LocStr TargetYieldLabel = Loc.Str(
            "AutoForestryDesignations.TargetYieldLabel",
            "Target yield",
            "Label for the Target yield step setting.");

        public static LocStr TargetYieldTip = Loc.Str(
            "AutoForestryDesignations.TargetYieldTip",
            "Target sustainable wood production per in-game month. ∞ = no target (stored as 0).",
            "Tooltip for the Target yield step setting. Zero is displayed as infinity.");

        public static LocStr TargetYieldLegacyTipFmt = Loc.Str(
            "AutoForestryDesignations.TargetYieldLegacyTipFmt",
            "Legacy maximum designations retained: {0}.",
            "Additional Target yield tooltip line shown when a migrated legacy Max tiles limit is still active. {0} = tile count.");

        public static LocStr MaxTilesLabel = Loc.Str(
            "AutoForestryDesignations.MaxTilesLabel",
            "Maximum number of designations",
            "Label for the Max Tiles step setting.");

        public static LocStr MaxTilesTip = Loc.Str(
            "AutoForestryDesignations.MaxTilesTip",
            "Maximum number of designation tiles to place per run. 0 = no limit (\u221e).",
            "Tooltip for the Max Tiles step setting.");

        public static LocStr TruckPoolingLabel = Loc.Str(
            "AutoForestryDesignations.TruckPoolingLabel",
            "Truck pooling",
            "Label for the Truck Pooling toggle setting.");

        public static LocStr TruckPoolingTip = Loc.Str(
            "AutoForestryDesignations.TruckPoolingTip",
            "Virtually pool and balance assigned trucks across all active tree harvesters of this tower.",
            "Tooltip for the Truck Pooling toggle setting.");

        public static LocStr ForestryVehicleOptimizationsLabel = Loc.Str(
            "AutoForestryDesignations.ForestryVehicleOptimizationsLabel",
            "Forestry vehicle optimizations",
            "Label for the world-level Forestry vehicle optimizations toggle setting.");

        public static LocStr ForestryVehicleOptimizationsTip = Loc.Str(
            "AutoForestryDesignations.ForestryVehicleOptimizationsTip",
            "Suppresses the mandatory bathroom breaks enforced by the Union of Forestry Workers. Idle planters and harvesters are forced to remain in the field and proactively move to places where work is expected to materialize.",
            "Tooltip for the Forestry vehicle optimizations toggle setting.");

        public static LocStr TruckPoolTitle = Loc.Str(
            "AutoForestryDesignations.TruckPoolTitle",
            "Truck pool",
            "Header title for the Truck Pool vehicle assigner section in the Forestry Tower inspector.");

        public static LocStr TruckPoolingHarvesterButtonsDisabledTip = Loc.Str(
            "AutoForestryDesignations.TruckPoolingHarvesterButtonsDisabledTip",
            "Truck pooling enabled. Manage assignments via {0}.",
            "Tooltip shown on harvester vehicle assignment buttons when truck pooling is enabled on its assigned tower. {0} is replaced by the tower name.");

        public static LocStr TruckPoolingHarvesterNote = Loc.Str(
            "AutoForestryDesignations.TruckPoolingHarvesterNote",
            "Note: Truck pooling enabled on {0}. Assigned vehicles may be redistributed.",
            "Note text shown below truck cards in harvester inspector when truck pooling is enabled on its assigned tower. {0} is replaced by the tower name.");

        // --- State / fallback labels ---
        public static LocStr NoForestryTowerSelected = Loc.Str(
            "AutoForestryDesignations.NoForestryTowerSelected",
            "No forestry tower selected.",
            "Shown in the Forestry information panel when no tower is active.");

        public static LocStr NoForestryDesignations = Loc.Str(
            "AutoForestryDesignations.NoForestryDesignations",
            "No forestry designations.",
            "Shown in the Forestry information panel when the tower has no managed forestry designations.");

        public static LocStr PressToScanComposition = Loc.Str(
            "AutoForestryDesignations.PressToScanComposition",
            "Press \u21ba to scan forestry composition.",
            "Prompt shown in the Forestry information panel before the first scan.");

        public static LocStr AssignedTrucksForestryTowerTooltip = Loc.Str(
            "AutoForestryDesignations.AssignedTrucksForestryTowerTooltip",
            "Truck pool will automatically distribute trucks to tree harvesters assigned to this tower.",
            "Tooltip that explains what trucks do when assigned to a forestry tower.");

        // --- KPI card labels ---
        public static LocStr KpiTreesLabel = Loc.Str(
            "AutoForestryDesignations.KpiTreesLabel",
            "Trees",
            "Label on the Trees KPI card in the Forestry information panel.");

        public static LocStr KpiMaturityLabel = Loc.Str(
            "AutoForestryDesignations.KpiMaturityLabel",
            "Maturity",
            "Label on the Maturity KPI card in the Forestry information panel.");

        public static LocStr KpiSustainableYieldLabel = Loc.Str(
            "AutoForestryDesignations.KpiSustainableYieldLabel",
            "Sustainable yield",
            "Label on the Sustainable yield KPI card in the Forestry information panel.");

        // --- KPI card tooltips (format templates: {0} = formatted age string) ---
        public static LocStr KpiTreesTip = Loc.Str(
            "AutoForestryDesignations.KpiTreesTip",
            "Live count of current managed trees inside this tower's forestry area. First number is trees right now; second number is estimated max trees based on currently valid planting positions.",
            "Tooltip on the Trees KPI card.");

        public static LocStr KpiMaturityTipFmt = Loc.Str(
            "AutoForestryDesignations.KpiMaturityTipFmt",
            "Average maturity and current age across managed trees. Full maturity is currently <b>{0}</b>, including difficulty settings.",
            "Tooltip template on the Maturity KPI card. {0} = average full maturity age string.");

        public static LocStr KpiSustainableYieldTipFmt = Loc.Str(
            "AutoForestryDesignations.KpiSustainableYieldTipFmt",
            "Maximum long-term wood production that can be maintained for this tower per in-game month, accounting for designated area, harvest threshold, and tree growth speed as per the difficulty settings (currently <b>{0}</b> average full maturity).",
            "Tooltip template on the Sustainable yield KPI card. {0} = average full maturity age string.");

        // --- Growth breakdown section ---
        public static LocStr GrowthBreakdownHeader = Loc.Str(
            "AutoForestryDesignations.GrowthBreakdownHeader",
            "Growth breakdown",
            "Header label for the growth breakdown bar section.");

        public static LocStr GrowthBreakdownTipFmt = Loc.Str(
            "AutoForestryDesignations.GrowthBreakdownTipFmt",
            "Tree maturity breakdown across managed trees. Full maturity age depends on tree type and difficulty; current average full maturity is {0}. Vanilla forestry yield curve: 40% age = 30% yield, 60% = 60%, 80% = 88%, 100% = full.",
            "Tooltip template on the whole growth breakdown section. {0} = average full maturity age string.");

        public static LocStr GrowthHeaderTipNoCutFmt = Loc.Str(
            "AutoForestryDesignations.GrowthHeaderTipNoCutFmt",
            "Distribution by maturity relative to each tree's current full-growth age. Harvest option: no cutting. Average full maturity: {0}.",
            "Tooltip template on the growth breakdown header when harvest is disabled. {0} = average full maturity age string.");

        public static LocStr GrowthHeaderTipWithCutFmt = Loc.Str(
            "AutoForestryDesignations.GrowthHeaderTipWithCutFmt",
            "Distribution by maturity relative to each tree's current full-growth age. Harvest option: {0} ({1}% of a {2} full-growth tree).",
            "Tooltip template on the growth breakdown header when harvest is enabled. {0} = harvest age, {1} = harvest percent string, {2} = max age string.");

        public static LocStr HarvestThresholdTip = Loc.Str(
            "AutoForestryDesignations.HarvestThresholdTip",
            "Harvest threshold",
            "Tooltip on the harvest threshold divider in the growth bar.");

        public static LocStr SegmentTipFmt = Loc.Str(
            "AutoForestryDesignations.SegmentTipFmt",
            "<b>{0}</b>\n{1} trees ({2:P0} planted, {3:P0} capacity)\n{4}",
            "Tooltip template on a growth bar segment. {0}=bucket label, {1}=count, {2}=fraction of planted, {3}=fraction of capacity, {4}=above/below harvest.");

        public static LocStr SegmentAboveHarvest = Loc.Str(
            "AutoForestryDesignations.SegmentAboveHarvest",
            "above harvest threshold",
            "Used as {4} in SegmentTipFmt when the bucket is above the harvest threshold.");

        public static LocStr SegmentBelowHarvest = Loc.Str(
            "AutoForestryDesignations.SegmentBelowHarvest",
            "below harvest threshold",
            "Used as {4} in SegmentTipFmt when the bucket is below the harvest threshold.");

        public static LocStr UnusedCapacityTipFmt = Loc.Str(
            "AutoForestryDesignations.UnusedCapacityTipFmt",
            "Unused capacity: {0} tiles ({1:P0} of capacity)",
            "Tooltip template on the unused capacity segment in the growth bar. {0}=count, {1}=fraction of capacity.");

        public static LocStr SegmentInteractionHint = Loc.Str(
            "AutoForestryDesignations.SegmentInteractionHint",
            "Hover to highlight trees in this stage.\nClick to mark or unmark them for harvest.",
            "Interaction hint appended to growth bar segment tooltips.");

        // --- Settings window ---
        public static LocStr SettingsModName = Loc.Str(
            "AutoForestryDesignations.SettingsModName",
            "Auto Forestry Designations",
            "Mod name shown in the shared mod settings window.");

        public static LocStr SettingsTabDefaults = Loc.Str(
            "AutoForestryDesignations.SettingsTabDefaults",
            "Defaults",
            "Title of the Defaults tab in the AFD mod settings window.");

        public static LocStr SettingsTabGameSettings = Loc.Str(
            "AutoForestryDesignations.SettingsTabGameSettings",
            "Game settings",
            "Title of the Game settings tab in the AFD mod settings window.");

        public static LocStr SettingsHeadingForestryTowerDefaults = Loc.Str(
            "AutoForestryDesignations.SettingsHeadingForestryTowerDefaults",
            "Tower settings defaults",
            "Section heading for forestry tower default settings.");

        public static LocStr SettingsHeadingDesignationDefaults = Loc.Str(
            "AutoForestryDesignations.SettingsHeadingDesignationDefaults",
            "Designation behaviors",
            "Section heading for game-level designation settings (terrain overrides, harvest, avoid-trees).");

        public static LocStr SettingsHeadingScanPerformance = Loc.Str(
            "AutoForestryDesignations.SettingsHeadingScanPerformance",
            "Scan performance",
            "Section heading for scan performance settings.");

        public static LocStr SettingsHeadingPanelDefaults = Loc.Str(
            "AutoForestryDesignations.SettingsHeadingPanelDefaults",
            "Tower panels defaults",
            "Section heading for panel collapsed-state defaults.");

        public static LocStr SettingsHeadingOptimizations = Loc.Str(
            "AutoForestryDesignations.SettingsHeadingOptimizations",
            "Optimizations",
            "Section heading for optional optimization settings.");

        public static LocStr SettingsAvoidTilesWithTreesLabel = Loc.Str(
            "AutoForestryDesignations.SettingsAvoidTilesWithTreesLabel",
            "Avoid tiles with trees",
            "Label for the Avoid Tiles With Trees toggle in the settings tab.");

        public static LocStr SettingsAvoidTilesWithTreesTip = Loc.Str(
            "AutoForestryDesignations.SettingsAvoidTilesWithTreesTip",
            "Skip tiles that already contain a tree when creating designations.",
            "Tooltip for the Avoid Tiles With Trees toggle in the settings tab.");

        public static LocStr SettingsMarkHarvestReadyLabel = Loc.Str(
            "AutoForestryDesignations.SettingsMarkHarvestReadyLabel",
            "Mark harvest-ready trees",
            "Label for the Mark Harvest Ready toggle in the settings tab.");

        public static LocStr SettingsMarkHarvestReadyTip = Loc.Str(
            "AutoForestryDesignations.SettingsMarkHarvestReadyTip",
            "After creating designations, automatically mark trees that meet the tower's harvest threshold for harvesting.",
            "Tooltip for the Mark Harvest Ready toggle in the settings tab.");

        public static LocStr SettingsBatchSizeLabel = Loc.Str(
            "AutoForestryDesignations.SettingsBatchSizeLabel",
            "Batch size",
            "Label for the Batch Size step control in the settings tab.");

        public static LocStr SettingsBatchSizeTip = Loc.Str(
            "AutoForestryDesignations.SettingsBatchSizeTip",
            "Number of designations placed per frame. Lower = more responsive; higher = faster scans. Clamped 1\u2013200.",
            "Tooltip for the Batch Size step control in the settings tab.");

        public static LocStr SettingsForestryPanelCollapsedLabel = Loc.Str(
            "AutoForestryDesignations.SettingsForestryPanelCollapsedLabel",
            "Forestry panel collapsed",
            "Label for the Forestry designations panel default collapsed state toggle.");

        public static LocStr SettingsForestryPanelCollapsedTip = Loc.Str(
            "AutoForestryDesignations.SettingsForestryPanelCollapsedTip",
            "Default collapsed state for the Forestry designations panel when a tower inspector is opened.",
            "Tooltip for the Forestry panel collapsed toggle.");

        public static LocStr SettingsInfoPanelCollapsedLabel = Loc.Str(
            "AutoForestryDesignations.SettingsInfoPanelCollapsedLabel",
            "Info panel collapsed",
            "Label for the Forestry information panel default collapsed state toggle.");

        public static LocStr SettingsInfoPanelCollapsedTip = Loc.Str(
            "AutoForestryDesignations.SettingsInfoPanelCollapsedTip",
            "Default collapsed state for the Forestry information panel when a tower inspector is opened.",
            "Tooltip for the Forestry info panel collapsed toggle.");

        public static LocStr SettingsSaveAsGlobal = Loc.Str(
            "AutoForestryDesignations.SettingsSaveAsGlobal",
            "Save as config",
            "Button label to save AFD settings as config default.");

        public static LocStr SettingsSaveAsGlobalTooltip = Loc.Str(
            "AutoForestryDesignations.SettingsSaveAsGlobalTooltip",
            "Save these settings to AFDsettings.json. They will be used as the defaults for all new games.",
            "Tooltip for the Save as config button in the AFD settings tab.");

        public static LocStr SettingsSavedToFile = Loc.Str(
            "AutoForestryDesignations.SettingsSavedToFile",
            "Saved to AFDsettings.json.",
            "Status message shown after settings are successfully saved.");

        public static LocStr SettingsSaveFailed = Loc.Str(
            "AutoForestryDesignations.SettingsSaveFailed",
            "Save failed \u2014 check the log",
            "Status message shown when settings save fails.");

        public static LocStr SettingsRestoreDefaults = Loc.Str(
            "AutoForestryDesignations.SettingsRestoreDefaults",
            "Reset to defaults",
            "Button label to reset AFD settings to built-in defaults.");

        public static LocStr SettingsRestoreDefaultsTooltip = Loc.Str(
            "AutoForestryDesignations.SettingsRestoreDefaultsTooltip",
            "Reset all designation and panel defaults to their built-in values. (Does not automatically save them as config.)",
            "Tooltip for the Reset to defaults button in the AFD settings tab.");

        public static LocStr SettingsRestoredDefaults = Loc.Str(
            "AutoForestryDesignations.SettingsRestoredDefaults",
            "Defaults restored",
            "Status message shown after defaults have been reset.");

        public static LocStr NewlyPlantedTip = Loc.Str(
            "AutoForestryDesignations.NewlyPlantedTip",
            "Newly planted / lowest maturity",
            "Tooltip on the sapling icon at the left of the growth bar.");

        public static LocStr FullyGrownTipFmt = Loc.Str(
            "AutoForestryDesignations.FullyGrownTipFmt",
            "Fully grown / highest maturity ({0} average full maturity in this tower).",
            "Tooltip template on the mature tree icon at the right of the growth bar. {0} = average full maturity age string.");

        public static LocStr BucketFullyMatureFmt = Loc.Str(
            "AutoForestryDesignations.BucketFullyMatureFmt",
            "Fully mature ({0}, 100%)",
            "Label template for the fully-mature growth bucket. {0} = max age string.");

        public static LocStr BucketNewlyPlantedName = Loc.Str(
            "AutoForestryDesignations.BucketNewlyPlantedName",
            "Newly planted",
            "Name of the 0-20% tree maturity bucket.");

        public static LocStr BucketYoungName = Loc.Str(
            "AutoForestryDesignations.BucketYoungName",
            "Young",
            "Name of the 20-40% tree maturity bucket.");

        public static LocStr BucketGrowingName = Loc.Str(
            "AutoForestryDesignations.BucketGrowingName",
            "Growing",
            "Name of the 40-60% tree maturity bucket.");

        public static LocStr BucketMaturingName = Loc.Str(
            "AutoForestryDesignations.BucketMaturingName",
            "Maturing",
            "Name of the 60-80% tree maturity bucket.");

        public static LocStr BucketNearlyMatureName = Loc.Str(
            "AutoForestryDesignations.BucketNearlyMatureName",
            "Nearly mature",
            "Name of the 80-100% tree maturity bucket.");

        public static LocStr BucketRangeFmt = Loc.Str(
            "AutoForestryDesignations.BucketRangeFmt",
            "{0} ({1}-{2}, {3:P0}-{4:P0})",
            "Label template for non-full growth buckets. {0}=bucket name, {1}=start age, {2}=end age, {3}=start maturity percent, {4}=end maturity percent.");

        public static LocStr OrderConstructionTooltip = Loc.Str(
            "AutoForestryDesignations.OrderConstructionTooltip",
            "Order a new {0} for {2} at {1}",
            "Tooltip shown on the assign vehicle button. {0} = vehicle description, {1} = depot description, {2} = tower description.");

        public static LocStr OrderConstructionShortcutHint = Loc.Str(
            "AutoForestryDesignations.OrderConstructionShortcutHint",
            "Shift-Alt-click to order a new {0} for {2} at {1}",
            "Hint appended to the vanilla assign-vehicle floater. Vehicle, depot, target.");

        public static LocStr PreAssignedTooltipFmt = Loc.Str(
            "AutoForestryDesignations.PreAssignedTooltipFmt",
            "Pre-assigned to {0}",
            "Tooltip shown on enqueued vehicles in depot build queue, with the target tower title.");

        public static LocStr EnqueueConfirmPromptSingular = Loc.Str(
            "AutoForestryDesignations.EnqueueConfirmPromptSingular",
            "Order a new {0} for {2} at {1}?",
            "Prompt shown when enqueuing 1 vehicle. {0} = vehicle description, {1} = depot description, {2} = tower description.");

        public static LocStr EnqueueConfirmPromptPlural = Loc.Str(
            "AutoForestryDesignations.EnqueueConfirmPromptPlural",
            "Order {0} new {1}s for {3} at {2}?",
            "Prompt shown when enqueuing multiple vehicles. {0} = count, {1} = vehicle description, {2} = depot description, {3} = tower description.");

        public static LocStr EnqueueConfirmBtnText = Loc.Str(
            "AutoForestryDesignations.EnqueueConfirmBtnText",
            "Order",
            "Text on the confirmation button for enqueuing construction.");

        public static LocStr ZoomToDepotTooltip = Loc.Str(
            "AutoForestryDesignations.ZoomToDepotTooltip",
            "Zoom to {0}",
            "Tooltip on the zoom to depot button in the confirmation popup. {0} = depot description.");
    }
}
