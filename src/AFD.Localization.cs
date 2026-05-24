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
            "{0}: {1} trees ({2:P0} of planted, {3:P0} of capacity) [{4}; ages use current full maturity {5}]",
            "Tooltip template on a growth bar segment. {0}=bucket label, {1}=count, {2}=fraction of planted, {3}=fraction of capacity, {4}=above/below harvest, {5}=max age string.");

        public static LocStr SegmentAboveHarvest = Loc.Str(
            "AFD_SegmentAboveHarvest",
            "at or above harvest threshold",
            "Used as {4} in SegmentTipFmt when the bucket is at or above the harvest threshold.");

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
            "Hover to highlight trees in this stage. Click to mark / unmark highlighted for harvest.",
            "Interaction hint appended to growth bar segment tooltips.");

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
    }
}
