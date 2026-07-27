using Mafi.Localization;

namespace AutoForestryDesignations
{
    internal static class AfdLocalization
    {
        // --- Panel titles ---
        public static LocStr ForestryDesignationsTitle = Loc.Str(
            "AFD_ForestryDesignationsTitle",
            "Forestry designations",
            "Title of the Forestry designations inspector panel.");

        public static LocStr ForestryInformationTitle = Loc.Str(
            "AFD_ForestryInformationTitle",
            "Forestry information",
            "Title of the Forestry information inspector panel.");

        // --- Buttons ---
        public static LocStr CreateDesignationsBtn = Loc.Str(
            "AFD_CreateDesignationsBtn",
            "Create designations",
            "Label on the Create designations button.");

        public static LocStr CreateDesignationsBtnTip = Loc.Str(
            "AFD_CreateDesignationsBtnTip",
            "Scan the tower area and place forestry designations.",
            "Tooltip on the Create designations button.");

        public static LocStr ClearDesignationsBtnTip = Loc.Str(
            "AFD_ClearDesignationsBtnTip",
            "Clear all forestry designations in this tower's area.",
            "Tooltip on the Clear designations (trash) button.");

        public static LocStr RefreshCompositionTip = Loc.Str(
            "AFD_RefreshCompositionTip",
            "Refresh forestry composition",
            "Tooltip on the refresh button in the Forestry information panel.");

        // --- Bool toggle display values ---
        public static LocStr BoolYes = Loc.Str(
            "AFD_BoolYes",
            "Yes",
            "Displayed in a toggle control when the option is enabled.");

        public static LocStr BoolNo = Loc.Str(
            "AFD_BoolNo",
            "No",
            "Displayed in a toggle control when the option is disabled.");

        // --- Setting labels and tooltips ---
        public static LocStr OnlyFertileTilesLabel = Loc.Str(
            "AFD_OnlyFertileTilesLabel",
            "Fertile tiles only",
            "Label for the Fertile Tiles Only toggle setting.");

        public static LocStr OnlyFertileTilesTip = Loc.Str(
            "AFD_OnlyFertileTilesTip",
            "Place designations only where the ground is valid for tree planting: fertile terrain (not rock, sand, or ocean) and not occupied by a building or other entity.",
            "Tooltip for the Fertile Tiles Only toggle setting.");

        public static LocStr OnlyReachableTilesLabel = Loc.Str(
            "AFD_OnlyReachableTilesLabel",
            "Reachable tiles only",
            "Label for the Reachable Tiles Only toggle setting.");

        public static LocStr OnlyReachableTilesTip = Loc.Str(
            "AFD_OnlyReachableTilesTip",
            "When enabled, skip designation tiles that are not reachable by vehicle pathability from the tower area.",
            "Tooltip for the Reachable Tiles Only toggle setting.");

        public static LocStr AvoidTerrainDesignationsLabel = Loc.Str(
            "AFD_AvoidTerrainDesignationsLabel",
            "Avoid terrain designations",
            "Label for the Avoid Terrain Designations toggle setting.");

        public static LocStr AvoidTerrainDesignationsTip = Loc.Str(
            "AFD_AvoidTerrainDesignationsTip",
            "Skip tiles that already contain any terrain designation, including mining, dumping, or leveling.",
            "Tooltip for the Avoid Terrain Designations toggle setting.");

        public static LocStr MaxTilesLabel = Loc.Str(
            "AFD_MaxTilesLabel",
            "Maximum number of designations",
            "Label for the Max Tiles step setting.");

        public static LocStr MaxTilesTip = Loc.Str(
            "AFD_MaxTilesTip",
            "Maximum number of designation tiles to place per run. 0 = no limit (\u221e).",
            "Tooltip for the Max Tiles step setting.");

        // --- State / fallback labels ---
        public static LocStr NoForestryTowerSelected = Loc.Str(
            "AFD_NoForestryTowerSelected",
            "No forestry tower selected.",
            "Shown in the Forestry information panel when no tower is active.");

        public static LocStr NoForestryDesignations = Loc.Str(
            "AFD_NoForestryDesignations",
            "No forestry designations.",
            "Shown in the Forestry information panel when the tower has no managed forestry designations.");

        public static LocStr PressToScanComposition = Loc.Str(
            "AFD_PressToScanComposition",
            "Press \u21ba to scan forestry composition.",
            "Prompt shown in the Forestry information panel before the first scan.");

        // --- KPI card labels ---
        public static LocStr KpiTreesLabel = Loc.Str(
            "AFD_KpiTreesLabel",
            "Trees",
            "Label on the Trees KPI card in the Forestry information panel.");

        public static LocStr KpiMaturityLabel = Loc.Str(
            "AFD_KpiMaturityLabel",
            "Maturity",
            "Label on the Maturity KPI card in the Forestry information panel.");

        public static LocStr KpiSustainableYieldLabel = Loc.Str(
            "AFD_KpiSustainableYieldLabel",
            "Sustainable yield",
            "Label on the Sustainable yield KPI card in the Forestry information panel.");

        // --- KPI card tooltips (format templates: {0} = formatted age string) ---
        public static LocStr KpiTreesTip = Loc.Str(
            "AFD_KpiTreesTip",
            "Live count of current managed trees inside this tower's forestry area. First number is trees right now; second number is estimated max trees based on currently valid planting positions.",
            "Tooltip on the Trees KPI card.");

        public static LocStr KpiMaturityTipFmt = Loc.Str(
            "AFD_KpiMaturityTipFmt",
            "Average maturity and current age across managed trees. Full maturity is currently <b>{0}</b>, including difficulty settings.",
            "Tooltip template on the Maturity KPI card. {0} = average full maturity age string.");

        public static LocStr KpiSustainableYieldTipFmt = Loc.Str(
            "AFD_KpiSustainableYieldTipFmt",
            "Maximum long-term wood production that can be maintained for this tower per in-game month, accounting for designated area, harvest threshold, and tree growth speed as per the difficulty settings (currently <b>{0}</b> average full maturity).",
            "Tooltip template on the Sustainable yield KPI card. {0} = average full maturity age string.");

        // --- Growth breakdown section ---
        public static LocStr GrowthBreakdownHeader = Loc.Str(
            "AFD_GrowthBreakdownHeader",
            "Growth breakdown",
            "Header label for the growth breakdown bar section.");

        public static LocStr GrowthBreakdownTipFmt = Loc.Str(
            "AFD_GrowthBreakdownTipFmt",
            "Tree maturity breakdown across managed trees. Full maturity age depends on tree type and difficulty; current average full maturity is {0}. Vanilla forestry yield curve: 40% age = 30% yield, 60% = 60%, 80% = 88%, 100% = full.",
            "Tooltip template on the whole growth breakdown section. {0} = average full maturity age string.");

        public static LocStr GrowthHeaderTipNoCutFmt = Loc.Str(
            "AFD_GrowthHeaderTipNoCutFmt",
            "Distribution by maturity relative to each tree's current full-growth age. Harvest option: no cutting. Average full maturity: {0}.",
            "Tooltip template on the growth breakdown header when harvest is disabled. {0} = average full maturity age string.");

        public static LocStr GrowthHeaderTipWithCutFmt = Loc.Str(
            "AFD_GrowthHeaderTipWithCutFmt",
            "Distribution by maturity relative to each tree's current full-growth age. Harvest option: {0} ({1}% of a {2} full-growth tree).",
            "Tooltip template on the growth breakdown header when harvest is enabled. {0} = harvest age, {1} = harvest percent string, {2} = max age string.");

        public static LocStr HarvestThresholdTip = Loc.Str(
            "AFD_HarvestThresholdTip",
            "Harvest threshold",
            "Tooltip on the harvest threshold divider in the growth bar.");

        public static LocStr SegmentTipFmt = Loc.Str(
            "AFD_SegmentTipFmt",
            "<b>{0}</b>\n{1} trees ({2:P0} planted, {3:P0} capacity)\n{4}",
            "Tooltip template on a growth bar segment. {0}=bucket label, {1}=count, {2}=fraction of planted, {3}=fraction of capacity, {4}=above/below harvest.");

        public static LocStr SegmentAboveHarvest = Loc.Str(
            "AFD_SegmentAboveHarvest",
            "above harvest threshold",
            "Used as {4} in SegmentTipFmt when the bucket is above the harvest threshold.");

        public static LocStr SegmentBelowHarvest = Loc.Str(
            "AFD_SegmentBelowHarvest",
            "below harvest threshold",
            "Used as {4} in SegmentTipFmt when the bucket is below the harvest threshold.");

        public static LocStr UnusedCapacityTipFmt = Loc.Str(
            "AFD_UnusedCapacityTipFmt",
            "Unused capacity: {0} tiles ({1:P0} of capacity)",
            "Tooltip template on the unused capacity segment in the growth bar. {0}=count, {1}=fraction of capacity.");

        public static LocStr SegmentInteractionHint = Loc.Str(
            "AFD_SegmentInteractionHint",
            "Hover to highlight trees in this stage.\nClick to mark or unmark them for harvest.",
            "Interaction hint appended to growth bar segment tooltips.");

        // --- Settings window ---
        public static LocStr SettingsModName = Loc.Str(
            "AFD_SettingsModName",
            "Auto Forestry Designations",
            "Mod name shown in the shared mod settings window.");

        public static LocStr SettingsTabDefaults = Loc.Str(
            "AFD_SettingsTabDefaults",
            "Defaults",
            "Title of the Defaults tab in the AFD mod settings window.");

        public static LocStr SettingsTabGameSettings = Loc.Str(
            "AFD_SettingsTabGameSettings",
            "Game settings",
            "Title of the Game settings tab in the AFD mod settings window.");

        public static LocStr SettingsHeadingForestryTowerDefaults = Loc.Str(
            "AFD_SettingsHeadingForestryTowerDefaults",
            "Forestry tower defaults",
            "Section heading for forestry tower default settings.");

        public static LocStr SettingsHeadingDesignationDefaults = Loc.Str(
            "AFD_SettingsHeadingDesignationDefaults",
            "Designation defaults",
            "Section heading for game-level designation settings (harvest, avoid-trees).");

        public static LocStr SettingsHeadingScanPerformance = Loc.Str(
            "AFD_SettingsHeadingScanPerformance",
            "Scan performance",
            "Section heading for scan performance settings.");

        public static LocStr SettingsHeadingPanelDefaults = Loc.Str(
            "AFD_SettingsHeadingPanelDefaults",
            "Panel defaults",
            "Section heading for panel collapsed-state defaults.");

        public static LocStr SettingsAvoidTilesWithTreesLabel = Loc.Str(
            "AFD_SettingsAvoidTilesWithTreesLabel",
            "Avoid tiles with trees",
            "Label for the Avoid Tiles With Trees toggle in the settings tab.");

        public static LocStr SettingsAvoidTilesWithTreesTip = Loc.Str(
            "AFD_SettingsAvoidTilesWithTreesTip",
            "Default: skip tiles that already contain a tree when creating designations.",
            "Tooltip for the Avoid Tiles With Trees toggle in the settings tab.");

        public static LocStr SettingsMarkHarvestReadyLabel = Loc.Str(
            "AFD_SettingsMarkHarvestReadyLabel",
            "Mark harvest-ready trees",
            "Label for the Mark Harvest Ready toggle in the settings tab.");

        public static LocStr SettingsMarkHarvestReadyTip = Loc.Str(
            "AFD_SettingsMarkHarvestReadyTip",
            "Default: after creating designations, automatically mark trees that meet the tower's harvest threshold for harvesting.",
            "Tooltip for the Mark Harvest Ready toggle in the settings tab.");

        public static LocStr SettingsBatchSizeLabel = Loc.Str(
            "AFD_SettingsBatchSizeLabel",
            "Batch size",
            "Label for the Batch Size step control in the settings tab.");

        public static LocStr SettingsBatchSizeTip = Loc.Str(
            "AFD_SettingsBatchSizeTip",
            "Number of designations placed per frame. Lower = more responsive; higher = faster scans. Clamped 1\u2013200.",
            "Tooltip for the Batch Size step control in the settings tab.");

        public static LocStr SettingsForestryPanelCollapsedLabel = Loc.Str(
            "AFD_SettingsForestryPanelCollapsedLabel",
            "Forestry panel collapsed",
            "Label for the Forestry designations panel default collapsed state toggle.");

        public static LocStr SettingsForestryPanelCollapsedTip = Loc.Str(
            "AFD_SettingsForestryPanelCollapsedTip",
            "Default collapsed state for the Forestry designations panel when a tower inspector is opened.",
            "Tooltip for the Forestry panel collapsed toggle.");

        public static LocStr SettingsInfoPanelCollapsedLabel = Loc.Str(
            "AFD_SettingsInfoPanelCollapsedLabel",
            "Info panel collapsed",
            "Label for the Forestry information panel default collapsed state toggle.");

        public static LocStr SettingsInfoPanelCollapsedTip = Loc.Str(
            "AFD_SettingsInfoPanelCollapsedTip",
            "Default collapsed state for the Forestry information panel when a tower inspector is opened.",
            "Tooltip for the Forestry info panel collapsed toggle.");

        public static LocStr SettingsSaveAsGlobal = Loc.Str(
            "AFD_SettingsSaveAsGlobal",
            "Save as config",
            "Button label to save AFD settings as config default.");

        public static LocStr SettingsSaveAsGlobalTooltip = Loc.Str(
            "AFD_SettingsSaveAsGlobalTooltip",
            "Save these settings to AFDsettings.json. They will be used as the defaults for all new games.",
            "Tooltip for the Save as config button in the AFD settings tab.");

        public static LocStr SettingsSavedToFile = Loc.Str(
            "AFD_SettingsSavedToFile",
            "Saved to AFDsettings.json.",
            "Status message shown after settings are successfully saved.");

        public static LocStr SettingsSaveFailed = Loc.Str(
            "AFD_SettingsSaveFailed",
            "Save failed \u2014 check the log",
            "Status message shown when settings save fails.");

        public static LocStr SettingsRestoreDefaults = Loc.Str(
            "AFD_SettingsRestoreDefaults",
            "Reset to defaults",
            "Button label to reset AFD settings to built-in defaults.");

        public static LocStr SettingsRestoreDefaultsTooltip = Loc.Str(
            "AFD_SettingsRestoreDefaultsTooltip",
            "Reset all designation and panel defaults to their built-in values. (Does not automatically save them as config.)",
            "Tooltip for the Reset to defaults button in the AFD settings tab.");

        public static LocStr SettingsRestoredDefaults = Loc.Str(
            "AFD_SettingsRestoredDefaults",
            "Defaults restored",
            "Status message shown after defaults have been reset.");

        public static LocStr NewlyPlantedTip = Loc.Str(
            "AFD_NewlyPlantedTip",
            "Newly planted / lowest maturity",
            "Tooltip on the sapling icon at the left of the growth bar.");

        public static LocStr FullyGrownTipFmt = Loc.Str(
            "AFD_FullyGrownTipFmt",
            "Fully grown / highest maturity ({0} average full maturity in this tower).",
            "Tooltip template on the mature tree icon at the right of the growth bar. {0} = average full maturity age string.");

        public static LocStr BucketFullyMatureFmt = Loc.Str(
            "AFD_BucketFullyMatureFmt",
            "Fully mature ({0}, 100%)",
            "Label template for the fully-mature growth bucket. {0} = max age string.");

        public static LocStr BucketNewlyPlantedName = Loc.Str(
            "AFD_BucketNewlyPlantedName",
            "Newly planted",
            "Name of the 0-20% tree maturity bucket.");

        public static LocStr BucketYoungName = Loc.Str(
            "AFD_BucketYoungName",
            "Young",
            "Name of the 20-40% tree maturity bucket.");

        public static LocStr BucketGrowingName = Loc.Str(
            "AFD_BucketGrowingName",
            "Growing",
            "Name of the 40-60% tree maturity bucket.");

        public static LocStr BucketMaturingName = Loc.Str(
            "AFD_BucketMaturingName",
            "Maturing",
            "Name of the 60-80% tree maturity bucket.");

        public static LocStr BucketNearlyMatureName = Loc.Str(
            "AFD_BucketNearlyMatureName",
            "Nearly mature",
            "Name of the 80-100% tree maturity bucket.");

        public static LocStr BucketRangeFmt = Loc.Str(
            "AFD_BucketRangeFmt",
            "{0} ({1}-{2}, {3:P0}-{4:P0})",
            "Label template for non-full growth buckets. {0}=bucket name, {1}=start age, {2}=end age, {3}=start maturity percent, {4}=end maturity percent.");

        public static LocStr OrderConstructionTooltip = Loc.Str(
            "AFD_OrderConstructionTooltip",
            "Order a new {0} for {2} at {1}",
            "Tooltip shown on the assign vehicle button. {0} = vehicle description, {1} = depot description, {2} = tower description.");

        public static LocStr OrderConstructionShortcutHint = Loc.Str(
            "AFD_OrderConstructionShortcutHint",
            "Shift-Alt-click to order a new {0} for {2} at {1}",
            "Hint appended to the vanilla assign-vehicle floater. Vehicle, depot, target.");

        public static LocStr PreAssignedTooltipFmt = Loc.Str(
            "AFD_PreAssignedTooltipFmt",
            "Pre-assigned to {0}",
            "Tooltip shown on enqueued vehicles in depot build queue, with the target tower title.");

        public static LocStr EnqueueConfirmPromptSingular = Loc.Str(
            "AFD_EnqueueConfirmPromptSingular",
            "Order a new {0} for {2} at {1}?",
            "Prompt shown when enqueuing 1 vehicle. {0} = vehicle description, {1} = depot description, {2} = tower description.");

        public static LocStr EnqueueConfirmPromptPlural = Loc.Str(
            "AFD_EnqueueConfirmPromptPlural",
            "Order {0} new {1}s for {3} at {2}?",
            "Prompt shown when enqueuing multiple vehicles. {0} = count, {1} = vehicle description, {2} = depot description, {3} = tower description.");

        public static LocStr EnqueueConfirmBtnText = Loc.Str(
            "AFD_EnqueueConfirmBtnText",
            "Order",
            "Text on the confirmation button for enqueuing construction.");

        public static LocStr ZoomToDepotTooltip = Loc.Str(
            "AFD_ZoomToDepotTooltip",
            "Zoom to {0}",
            "Tooltip on the zoom to depot button in the confirmation popup. {0} = depot description.");
    }
}
