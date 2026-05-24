// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System;
using System.Collections;
using System.Collections.Generic;
using Mafi;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Terrain.Trees;
using Mafi.Localization;
using Mafi.Unity.Terrain;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using Row = Mafi.Unity.UiToolkit.Library.Row;
using ClickEvent = UnityEngine.UIElements.ClickEvent;

namespace AutoForestryDesignations
{
    internal static class ForestryInfoPanel
    {
        private const int BUCKET_COUNT = 6;
        private const int GROWTH_STAGE_BUCKET_COUNT = BUCKET_COUNT - 1;
        private const int PANEL_GAP_PT = 2;
        private const int CARD_PADDING_PT = 6;

        private static readonly ColorRgba[] s_belowHarvestColors =
        {
            new ColorRgba(0xc6e7b2),
            new ColorRgba(0x95cc7a),
            new ColorRgba(0x5fa85a),
            new ColorRgba(0x2f7b44),
            new ColorRgba(0x1f5a31),
            new ColorRgba(0x184628),
        };

        private static readonly ColorRgba[] s_aboveHarvestColors =
        {
            new ColorRgba(0xc9a36a),
            new ColorRgba(0xb78654),
            new ColorRgba(0xa16d3f),
            new ColorRgba(0x8b5a2b),
            new ColorRgba(0x6f431f),
            new ColorRgba(0x553015),
        };

        private static readonly ColorRgba s_unusedCapacityColor = new ColorRgba(0x1a1a1a);

        private static readonly Dictionary<object, Action> s_refreshCallbacks =
            new Dictionary<object, Action>();
        private static readonly Dictionary<object, Func<IAreaManagingTower?>> s_towerResolvers =
            new Dictionary<object, Func<IAreaManagingTower?>>();
        private static readonly Dictionary<object, PanelWithHeader> s_panels =
            new Dictionary<object, PanelWithHeader>();

        private static ColorRgba s_activeHighlightColor;
        private static TreeId[]? s_hoveredBucketTrees;

        private static Mafi.Unity.Ui.Library.Display? s_treeCountDisplay;
        private static UnityEngine.Coroutine? s_liveCountCoroutine;

        internal static void StopLiveTreeCount()
        {
            AutoForestryDesignation.StopCoroutine(s_liveCountCoroutine);
            s_liveCountCoroutine = null;
            s_treeCountDisplay = null;
        }

        private static IEnumerator LiveTreeCountCoroutine(ForestryTower tower)
        {
            var wait = new UnityEngine.WaitForSeconds(0.5f);
            while (true)
            {
                yield return wait;
                var display = s_treeCountDisplay;
                var treesManager = AutoForestryDesignation.GetTreesManager();
                if (display == null || treesManager == null) yield break;
                int count = 0;
                foreach (TreeId treeId in tower.Trees)
                    if (IsTreeInManagedDesignation(tower, treeId) && treesManager.Trees.ContainsKey(treeId))
                        count++;
                display.SetValue(new LocStrFormatted(count.ToString()));
            }
        }

        private static void ClearActiveHighlights()
        {
            var trees = s_hoveredBucketTrees;
            if (trees == null) return;
            s_hoveredBucketTrees = null;
            var renderer = AutoForestryDesignation.GetTreesRenderer();
            if (renderer == null) return;
            foreach (var treeId in trees)
            {
                try { renderer.RemoveHighlight(treeId, s_activeHighlightColor, ignoreDestroyedTrees: true); }
                catch { }
            }
        }

        internal static PanelWithHeader Build(Func<IAreaManagingTower?> getTower, object key)
        {
            var contentCol = new Column(PANEL_GAP_PT.pt());
            var promptLabel = new Label(AfdLocalization.PressToScanComposition)
                .Color(Theme.InactiveColor);
            contentCol.Add(promptLabel);

            s_refreshCallbacks[key] = (Action)delegate
            {
                PopulateContent(contentCol, getTower());
            };
            s_towerResolvers[key] = getTower;

            var panel = new PanelWithHeader()
                .Title(AfdLocalization.ForestryInformationTitle,
                       new LocStrFormatted($"Current trees and projected wood output in this tower's forestry area. [Kayser's Automatic Forestry Designations v{AutoForestryDesignationsMod.ModVersion}]"));

            var refreshButton = new ButtonIcon(Button.General,
                "Assets/Unity/UserInterface/General/Repeat.svg",
                (Action)delegate
                {
                    PopulateContent(contentCol, getTower());
                })
                .Compact()
                .IconSize(14.px())
                .MarginLeft(4.pt())
                .Tooltip(AfdLocalization.RefreshCompositionTip);
            refreshButton.OnClick((ClickEvent evt) => evt.StopPropagation());

            panel.Header.Add(refreshButton);
            panel.BodyAdd(contentCol);
            var initialTower = getTower();
            panel.Collapsed(initialTower != null
                ? AutoForestryDesignation.GetTowerForestryInformationPanelCollapsed(initialTower)
                : AutoForestryDesignationsMod.ForestryInformationPanelCollapsed);
            panel.Header.OnClick((ClickEvent evt) =>
            {
                var tower = getTower();
                if (tower != null)
                    AutoForestryDesignation.SetTowerForestryInformationPanelCollapsed(tower, panel.IsCollapsed);
            });
            s_panels[key] = panel;
            return panel;
        }

        internal static void RefreshContent(object key)
        {
            if (s_panels.TryGetValue(key, out var panel)
                && s_towerResolvers.TryGetValue(key, out var getTower))
            {
                var tower = getTower();
                if (tower != null)
                    panel.Collapsed(AutoForestryDesignation.GetTowerForestryInformationPanelCollapsed(tower));
            }

            if (s_refreshCallbacks.TryGetValue(key, out var cb))
                try { cb?.Invoke(); } catch { }
        }

        internal static void RefreshAll()
        {
            foreach (var cb in s_refreshCallbacks.Values)
                try { cb?.Invoke(); } catch { }
        }

        internal static void RefreshForTower(ForestryTower tower)
        {
            foreach (var kvp in s_towerResolvers)
            {
                try
                {
                    if (ReferenceEquals(kvp.Value(), tower))
                        RefreshContent(kvp.Key);
                }
                catch { }
            }
        }

        private static void PopulateContent(Column col, IAreaManagingTower? tower)
        {
            StopLiveTreeCount();
            ClearActiveHighlights();
            col.Clear();

            var forestryTower = tower as ForestryTower;
            var treesManager = AutoForestryDesignation.GetTreesManager();
            var currentStep = AutoForestryDesignation.GetCurrentSimStep();

            if (forestryTower == null || treesManager == null || !currentStep.HasValue)
            {
                col.Add(new Label(AfdLocalization.NoForestryTowerSelected));
                return;
            }

            if (!HasManagedForestryDesignation(forestryTower))
            {
                col.Add(new Label(AfdLocalization.NoForestryDesignations));
                return;
            }

            var stats = CollectStats(forestryTower, treesManager, currentStep.Value);
            col.Add(BuildKpiRow(forestryTower, stats));
            col.Add(BuildGrowthSection(forestryTower, stats));
        }

        private static bool HasManagedForestryDesignation(ForestryTower tower)
        {
            foreach (TerrainDesignation designation in tower.ManagedDesignations)
            {
                if (designation.IsForestry)
                    return true;
            }
            return false;
        }

        private static ForestryStats CollectStats(ForestryTower tower, TreesManager treesManager, SimStep currentStep)
        {
            int treeCount = 0;
            float growthSum01 = 0f;
            int woodReserve = 0;
            int[] growthBuckets = new int[BUCKET_COUNT];
            var bucketTreeLists = new List<TreeId>[BUCKET_COUNT];
            for (int k = 0; k < BUCKET_COUNT; k++) bucketTreeLists[k] = new List<TreeId>();
            float maxAgeYears = 0f;
            float ageYearsSum = 0f;
            float maxAgeYearsSum = 0f;

            foreach (TreeId treeId in tower.Trees)
            {
                if (!IsTreeInManagedDesignation(tower, treeId))
                    continue;

                if (!treesManager.Trees.TryGetValue(treeId, out TreeData treeData))
                    continue;

                treeCount++;
                float treeMaxAgeYears = Math.Max(0.01f, treeData.Proto.GetTreeMaxAge().Years.ToFloat());
                // Use the game's own growth ratio (clamps at 1.0, handles GENERATED_TREE_PLANTED_AT_TICK = -720000 ticks correctly)
                float growth01 = treeData.GetGrowthPercentAt(currentStep).ToFloat();
                float ageYears = treeMaxAgeYears * growth01;
                int bucket = GetGrowthBucketIndex(growth01);
                int woodThisTree = treeData.GetHarvestedQuantityAt(currentStep).Value;

                growthSum01 += growth01;
                ageYearsSum += ageYears;
                maxAgeYearsSum += treeMaxAgeYears;
                woodReserve += woodThisTree;
                growthBuckets[bucket]++;
                bucketTreeLists[bucket].Add(treeId);
                maxAgeYears = Math.Max(maxAgeYears, treeMaxAgeYears);
            }

            float averageMaturityPercent = treeCount > 0 ? (growthSum01 / treeCount) * 100f : 0f;
            float averageAgeYears = treeCount > 0 ? ageYearsSum / treeCount : 0f;
            float averageMaxAgeYears = treeCount > 0 ? maxAgeYearsSum / treeCount : 0f;
            int treeCapacity = EstimateTreeCapacity(tower, treesManager, treeCount);
            float capacityPerYear = EstimateCapacityPerYear(tower, treesManager, treeCapacity);
            AutoForestryDesignation.LogDebug(string.Format("[AFD] CollectStats: trees={0}/{1} woodReserve={2} maturity={3:F1}% avgAge={4:F1}y avgMaxAge={5:F1}y maxAge={6:F1}y buckets=[{7}] capacity/month={8:F1}",
                treeCount, treeCapacity, woodReserve, averageMaturityPercent, averageAgeYears, averageMaxAgeYears, maxAgeYears,
                string.Join(",", growthBuckets), capacityPerYear / 12f));
            var bucketTrees = new TreeId[BUCKET_COUNT][];
            for (int k = 0; k < BUCKET_COUNT; k++) bucketTrees[k] = bucketTreeLists[k].ToArray();
            return new ForestryStats(treeCount, treeCapacity, averageMaturityPercent, averageAgeYears, averageMaxAgeYears, woodReserve, capacityPerYear, maxAgeYears, growthBuckets, bucketTrees);
        }

        private static int EstimateTreeCapacity(ForestryTower tower, TreesManager treesManager, int liveManagedTreeCount)
        {
            if (tower.TreeTypes.Count == 0)
                return liveManagedTreeCount;

            int spacing = GetEstimatedPlantingSpacing(tower, treesManager);
            if (spacing <= 0)
                return liveManagedTreeCount;

            var candidates = new List<PlantingCandidate>();
            var seenTiles = new HashSet<Tile2i>();
            Tile2i towerTile = tower.Position2f.Tile2i;

            foreach (TerrainDesignation designation in tower.ManagedDesignations)
            {
                if (!designation.IsForestry || !designation.IsFulfilled)
                    continue;

                Tile2i origin = designation.OriginTileCoord;
                for (int y = 0; y < TerrainDesignation.SIZE_TILES; y++)
                {
                    for (int x = 0; x < TerrainDesignation.SIZE_TILES; x++)
                    {
                        var tile = origin + new RelTile2i(x, y);
                        if (!seenTiles.Add(tile))
                            continue;

                        if (tower.Area.ContainsTile(tile) && treesManager.IsValidTileForPlanting(tile, spacing))
                            candidates.Add(new PlantingCandidate(tile, tile.DistanceSqrTo(towerTile)));
                    }
                }
            }

            candidates.Sort((a, b) => a.DistanceSqrToTower.CompareTo(b.DistanceSqrToTower));

            var futureTrees = new List<Tile2i>();
            long requiredFutureSpacing = spacing * 2L;
            long requiredFutureSpacingSqr = requiredFutureSpacing * requiredFutureSpacing;
            foreach (PlantingCandidate candidate in candidates)
            {
                bool hasEnoughFutureSpacing = true;
                foreach (Tile2i futureTree in futureTrees)
                {
                    if (candidate.Tile.DistanceSqrTo(futureTree) < requiredFutureSpacingSqr)
                    {
                        hasEnoughFutureSpacing = false;
                        break;
                    }
                }

                if (hasEnoughFutureSpacing)
                    futureTrees.Add(candidate.Tile);
            }

            AutoForestryDesignation.LogDebug(string.Format(
                "[AFD] EstimateTreeCapacity: live={0} spacing={1} validNow={2} future={3} => capacity={4}",
                liveManagedTreeCount, spacing, candidates.Count, futureTrees.Count,
                liveManagedTreeCount + futureTrees.Count));
            return liveManagedTreeCount + futureTrees.Count;
        }

        private static int GetEstimatedPlantingSpacing(ForestryTower tower, TreesManager treesManager)
        {
            int weightedSpacingSum = 0;
            int totalWeight = 0;
            var treeTypes = tower.TreeTypes;
            for (int i = 0; i < treeTypes.Count; i++)
            {
                var entry = treeTypes[i];
                if (entry.Value <= 0 || entry.Key.Trees.Length == 0)
                    continue;

                foreach (TreeProto treeProto in entry.Key.Trees)
                {
                    weightedSpacingSum += treeProto.SpacingToOtherTree * entry.Value;
                    totalWeight += entry.Value;
                }
            }

            if (totalWeight > 0)
                return Math.Max(1, (int)Math.Round((double)weightedSpacingSum / totalWeight));

            int currentSpacingSum = 0;
            int currentTreeCount = 0;
            foreach (TreeId treeId in tower.Trees)
            {
                if (!IsTreeInManagedDesignation(tower, treeId))
                    continue;

                if (!treesManager.Trees.TryGetValue(treeId, out TreeData treeData))
                    continue;

                currentSpacingSum += treeData.Proto.SpacingToOtherTree;
                currentTreeCount++;
            }

            if (currentTreeCount > 0)
                return Math.Max(1, (int)Math.Round((double)currentSpacingSum / currentTreeCount));

            return TreeProto.MAX_TREE_SPACING;
        }

        private static float EstimateCapacityPerYear(ForestryTower tower, TreesManager treesManager, int effectiveTreeCapacity)
        {
            if (effectiveTreeCapacity <= 0)
            {
                AutoForestryDesignation.LogDebug("[AFD] EstimateCapacityPerYear: effectiveTreeCapacity=0, returning 0");
                return 0f;
            }

            // Clamp to [0,1]: NO_CUT_AT = 200% sentinel means no cutting → treat as 100%
            bool harvestDisabled = tower.TargetHarvestPercent >= ForestryTower.NO_CUT_AT;
            float harvestGrowth01 = harvestDisabled
                ? 1f
                : Math.Min(1f, tower.TargetHarvestPercent.ToFloat());

            float weightedYieldPerTreePerYear = EstimateConfiguredYieldPerTreePerYear(tower, harvestGrowth01);
            bool usedFallback = weightedYieldPerTreePerYear <= 0f;
            if (usedFallback)
                weightedYieldPerTreePerYear = EstimateCurrentYieldPerTreePerYear(tower, treesManager, harvestGrowth01);

            float capacity = effectiveTreeCapacity * weightedYieldPerTreePerYear;
            AutoForestryDesignation.LogDebug(string.Format(
                "[AFD] EstimateCapacityPerYear: effectiveCap={0} harvestDisabled={1} harvestGrowth={2:P0} yieldPerTree/y={3:F2} (fallback={4}) => capacity/y={5:F1}",
                effectiveTreeCapacity,
                harvestDisabled, harvestGrowth01,
                weightedYieldPerTreePerYear, usedFallback, capacity));
            AutoForestryDesignation.LogDebug(string.Format("[AFD] NOTE: capacity/month={0:F1} (capacity/y={1:F1})", capacity / 12f, capacity));
            return capacity;
        }

        private static float EstimateConfiguredYieldPerTreePerYear(ForestryTower tower, float harvestGrowth01)
        {
            var treeTypes = tower.TreeTypes;
            if (treeTypes.Count == 0)
            {
                AutoForestryDesignation.LogDebug("[AFD] EstimateConfiguredYieldPerTreePerYear: no configured tree types");
                return 0f;
            }

            var harvestPercent = Percent.FromFloat(harvestGrowth01);

            float weightedSum = 0f;
            int totalWeight = 0;
            for (int i = 0; i < treeTypes.Count; i++)
            {
                var entry = treeTypes[i];
                if (entry.Value <= 0 || entry.Key.Trees.Length == 0)
                    continue;

                float groupYieldPerTreePerYear = 0f;
                foreach (TreeProto treeProto in entry.Key.Trees)
                {
                    float maxAgeY = treeProto.GetTreeMaxAge().Years.ToFloat();
                    float harvestAgeYears = Math.Max(0.01f, maxAgeY * harvestGrowth01);
                    int yieldAtHarvest = treeProto.GetHarvestedQuantity(harvestPercent).Value;
                    float yieldPerYear = yieldAtHarvest / harvestAgeYears;
                    groupYieldPerTreePerYear += yieldPerYear;
                }
                groupYieldPerTreePerYear /= entry.Key.Trees.Length;

                weightedSum += groupYieldPerTreePerYear * entry.Value;
                totalWeight += entry.Value;
            }

            float result = totalWeight > 0 ? weightedSum / totalWeight : 0f;
            AutoForestryDesignation.LogDebug(string.Format("[AFD] EstimateConfiguredYieldPerTreePerYear: weightedSum={0:F2} totalWeight={1} => {2:F2}/tree/y",
                weightedSum, totalWeight, result));
            return result;
        }

        private static float EstimateCurrentYieldPerTreePerYear(ForestryTower tower, TreesManager treesManager, float harvestGrowth01)
        {
            var harvestPercent = Percent.FromFloat(harvestGrowth01);
            float sum = 0f;
            int count = 0;
            foreach (TreeId treeId in tower.Trees)
            {
                if (!IsTreeInManagedDesignation(tower, treeId))
                    continue;

                if (!treesManager.Trees.TryGetValue(treeId, out TreeData treeData))
                    continue;

                float maxAgeY = treeData.Proto.GetTreeMaxAge().Years.ToFloat();
                float harvestAgeYears = Math.Max(0.01f, maxAgeY * harvestGrowth01);
                int yieldAtHarvest = treeData.Proto.GetHarvestedQuantity(harvestPercent).Value;
                float yieldPerYear = yieldAtHarvest / harvestAgeYears;
                sum += yieldPerYear;
                count++;
            }

            float result = count > 0 ? sum / count : 0f;
            AutoForestryDesignation.LogDebug(string.Format("[AFD] EstimateCurrentYieldPerTreePerYear: sum={0:F2} count={1} => {2:F2}/tree/y",
                sum, count, result));
            return result;
        }

        private static Row BuildKpiRow(ForestryTower tower, ForestryStats stats)
        {
            var row = new Row().Gap(PANEL_GAP_PT.pt()).AlignItemsStretch().AlignSelfStretch();
            row.Add(BuildTreesKpi(tower, stats, AfdLocalization.KpiTreesTip));
            row.Add(BuildKpi(
                AfdLocalization.KpiMaturityLabel,
                string.Format("{0} ({1})", FormatPercent(stats.MaturityPercent), FormatYears(stats.AverageAgeYears)),
                "Assets/Base/Products/Icons/TreeSapling.svg",
                new LocStrFormatted(string.Format(AfdLocalization.KpiMaturityTipFmt.TranslatedString,
                    FormatYears(stats.AverageMaxAgeYears)))));
            row.Add(BuildKpi(
                AfdLocalization.KpiSustainableYieldLabel,
                FormatAmount(stats.CapacityPerYear / 12f) + " /mo",
                "Assets/Base/Products/Icons/Wood.svg",
                new LocStrFormatted(string.Format(AfdLocalization.KpiSustainableYieldTipFmt.TranslatedString,
                    FormatYears(stats.AverageMaxAgeYears)))));
            return row;
        }

        private static Column BuildTreesKpi(ForestryTower tower, ForestryStats stats, LocStrFormatted tooltip)
        {
            var col = new Column()
                .FlexGrow(1f)
                .Background(Theme.BackgroundDark)
                .OverflowHidden()
                .Padding(CARD_PADDING_PT.pt())
                .Gap(2.pt());
            col.BorderRadius(8);
            col.Border(1.px(), Theme.BorderColor, 8);
            col.Tooltip(tooltip);

            var topRow = new Row().AlignItemsCenter().Gap(4.pt());
            topRow.Add(BuildMatureTreeIcon(44));
            var textCol = new Column(1.pt());
            textCol.Add(new Label(AfdLocalization.KpiTreesLabel).FontSize(13).FontBold().NoTextWrap());
            var countDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(stats.TreeCount.ToString())).MinDigits(3);
            ((IComponentWithStateColor)countDisplay).SetState(DisplayState.Positive);
            s_treeCountDisplay = countDisplay;
            s_liveCountCoroutine = AutoForestryDesignation.StartCoroutine(LiveTreeCountCoroutine(tower));
            var valueRow = new Row().AlignItemsCenter().Gap(2.pt());
            valueRow.Add(countDisplay);
            valueRow.Add(new Label(new LocStrFormatted($"/ {stats.TreeCapacity}")).FontSize(12).NoTextWrap());
            textCol.Add(valueRow);
            topRow.Add(textCol);
            col.Add(topRow);
            return col;
        }

        private static Column BuildKpi(LocStr label, string value, string iconPath, LocStrFormatted tooltip)
        {
            return BuildKpi(label, value, () => new Icon(iconPath).NoTint().Size(40.px()), tooltip);
        }

        private static Column BuildKpi(LocStr label, string value, Func<UiComponent> buildIcon, LocStrFormatted tooltip)
        {
            var col = new Column()
                .FlexGrow(1f)
                .Background(Theme.BackgroundDark)
                .OverflowHidden()
                .Padding(CARD_PADDING_PT.pt())
                .Gap(2.pt());

            col.BorderRadius(8);
            col.Border(1.px(), Theme.BorderColor, 8);
            col.Tooltip(tooltip);

            var topRow = new Row().AlignItemsCenter().Gap(4.pt());
            topRow.Add(buildIcon());
            var textCol = new Column(1.pt());
            textCol.Add(new Label(label).FontSize(13).FontBold().NoTextWrap());
            textCol.Add(new Label(new LocStrFormatted(value)).FontSize(12).NoTextWrap());
            topRow.Add(textCol);
            col.Add(topRow);
            return col;
        }

        private static Column BuildGrowthSection(ForestryTower tower, ForestryStats stats)
        {
            float maxAgeYears = Math.Max(1f, stats.MaxAgeYears);
            bool harvestDisabled = tower.TargetHarvestPercent >= ForestryTower.NO_CUT_AT;
            float threshold01 = harvestDisabled
                ? 1f
                : Math.Min(1f, tower.TargetHarvestPercent.ToFloat());
            float thresholdPercent = threshold01 * 100f;
            int totalCapacity = Math.Max(1, stats.TreeCapacity);
            int unusedCapacity = Math.Max(0, totalCapacity - stats.TreeCount);

            var section = new Column(2.pt())
                .AlignSelfStretch()
                .Background(Theme.BackgroundDark)
                .Padding(CARD_PADDING_PT.pt())
                .Gap(2.pt());
            section.BorderRadius(8);
            section.Border(1.px(), Theme.BorderColor, 8);
            section.Tooltip(new LocStrFormatted(
                string.Format(AfdLocalization.GrowthBreakdownTipFmt.TranslatedString,
                    FormatYears(stats.AverageMaxAgeYears))));

            var header = new Row().AlignItemsCenter();
            header.Add(new Label(AfdLocalization.GrowthBreakdownHeader).FontBold());
            header.Tooltip(new LocStrFormatted(harvestDisabled
                ? string.Format(AfdLocalization.GrowthHeaderTipNoCutFmt.TranslatedString,
                    FormatYears(stats.AverageMaxAgeYears))
                : string.Format(AfdLocalization.GrowthHeaderTipWithCutFmt.TranslatedString,
                    FormatYears(maxAgeYears * thresholdPercent / 100f),
                    thresholdPercent.ToString("F0"),
                    FormatYears(maxAgeYears))));
            section.Add(header);

            var bar = new Row().AlignSelfStretch().Height(24.px()).AlignItemsStretch().Background(Theme.BackgroundPanelLike).OverflowHidden().Class(Cls.displayBg).Border(1.px(), Theme.BorderColor, 3);
            int plantedTotal = Math.Max(1, stats.TreeCount);
            if (unusedCapacity > 0)
            {
                var unusedSegment = new UiComponent()
                    .FlexGrow(unusedCapacity)
                    .Background(s_unusedCapacityColor)
                    .Tooltip(new LocStrFormatted(string.Format(AfdLocalization.UnusedCapacityTipFmt.TranslatedString,
                        unusedCapacity,
                        (float)unusedCapacity / totalCapacity)));
                unusedSegment.OnMouseEnterLeave(onEnter: () => ClearActiveHighlights(), onLeave: () => { });
                bar.Add(unusedSegment);
            }

            for (int i = 0; i < BUCKET_COUNT; i++)
            {
                int bucketCount = stats.GrowthBuckets[i];
                if (bucketCount <= 0)
                    continue;

                float bucketMidpoint01 = GetBucketMidpoint01(i);
                bool isAboveHarvest = !harvestDisabled && bucketMidpoint01 >= threshold01;

                var bucketColor = isAboveHarvest ? s_aboveHarvestColors[i] : s_belowHarvestColors[i];
                var hoverBucketColor = bucketColor.Lerp(ColorRgba.White, Percent.Twenty);
                string bucketLabel = GetBucketLabel(i, maxAgeYears);

                var segment = new UiComponent()
                    .FlexGrow(bucketCount)
                    .Background(bucketColor)
                    .Class(Cls.clickable)
                    .Tooltip(new LocStrFormatted(string.Format(AfdLocalization.SegmentTipFmt.TranslatedString,
                        bucketLabel,
                        bucketCount,
                        (float)bucketCount / plantedTotal,
                        (float)bucketCount / totalCapacity,
                        isAboveHarvest ? AfdLocalization.SegmentAboveHarvest.TranslatedString : AfdLocalization.SegmentBelowHarvest.TranslatedString,
                        FormatYears(maxAgeYears))
                        + "\n\n" + AfdLocalization.SegmentInteractionHint.TranslatedString));
                var capturedBucketTrees = stats.BucketTrees[i];
                segment.OnMouseEnterLeave(
                    onEnter: () =>
                    {
                        segment.Background(hoverBucketColor);
                        ClearActiveHighlights();
                        s_activeHighlightColor = bucketColor.Lerp(ColorRgba.White, Percent.FromFloat(0.6f));
                        var treesRenderer = AutoForestryDesignation.GetTreesRenderer();
                        if (treesRenderer == null || capturedBucketTrees.Length == 0) return;
                        foreach (var treeId in capturedBucketTrees)
                        {
                            try { treesRenderer.AddHighlight(treeId, s_activeHighlightColor); }
                            catch { }
                        }
                        s_hoveredBucketTrees = capturedBucketTrees;
                    },
                    onLeave: () =>
                    {
                        segment.Background(bucketColor);
                        ClearActiveHighlights();
                    }
                );
                segment.OnClick((ClickEvent evt) =>
                {
                    evt.StopPropagation();
                    var treesManager = AutoForestryDesignation.GetTreesManager();
                    if (treesManager == null || capturedBucketTrees.Length == 0) return;
                    bool allSelected = true;
                    foreach (var treeId in capturedBucketTrees)
                    {
                        if (!treesManager.IsTreeSelected(treeId))
                        {
                            allSelected = false;
                            break;
                        }
                    }
                    if (allSelected)
                    {
                        foreach (var treeId in capturedBucketTrees)
                            try { treesManager.RemoveFromHarvest(treeId); } catch { }
                    }
                    else
                    {
                        foreach (var treeId in capturedBucketTrees)
                        {
                            if (!treesManager.IsTreeSelected(treeId))
                                try { treesManager.AddToHarvest(treeId); } catch { }
                        }
                    }
                    AutoForestryDesignation.ActivateHarvestOverlayIfNeeded();
                });
                bar.Add(segment);
            }

            bar.Add(new UiComponent().Class(Cls.displayGlass).IgnoreInputPicking());

            var barWithLegend = new Row(3.pt()).AlignSelfStretch().AlignItemsCenter();
            barWithLegend.Add(new Icon("Assets/Base/Products/Icons/TreeSapling.svg").NoTint().Size(24.px()).MarginBottom(2.px())
                .Tooltip(AfdLocalization.NewlyPlantedTip));
            barWithLegend.Add(bar.FlexGrow(1f));
            barWithLegend.Add(BuildMatureTreeIcon(26)
                .Tooltip(new LocStrFormatted(string.Format(AfdLocalization.FullyGrownTipFmt.TranslatedString,
                    FormatYears(stats.AverageMaxAgeYears)))));

            section.Add(barWithLegend);

            return section;
        }

        private static bool IsTreeInManagedDesignation(ForestryTower tower, TreeId treeId)
        {
            foreach (TerrainDesignation designation in tower.ManagedDesignations)
            {
                if (designation.Area.ContainsTile(treeId.Position))
                    return true;
            }
            return false;
        }

        private static int GetGrowthBucketIndex(float growth01)
        {
            if (growth01 >= 1f)
                return BUCKET_COUNT - 1;

            int bucket = (int)(Math.Max(0f, growth01) * GROWTH_STAGE_BUCKET_COUNT);
            return Math.Min(GROWTH_STAGE_BUCKET_COUNT - 1, Math.Max(0, bucket));
        }

        private static float GetBucketMidpoint01(int bucketIndex)
        {
            if (bucketIndex >= BUCKET_COUNT - 1)
                return 1f;

            return (bucketIndex + 0.5f) / GROWTH_STAGE_BUCKET_COUNT;
        }

        private static string GetBucketLabel(int bucketIndex, float maxAgeYears)
        {
            if (bucketIndex >= BUCKET_COUNT - 1)
                return string.Format(AfdLocalization.BucketFullyMatureFmt.TranslatedString, FormatYears(maxAgeYears));

            float start01 = (float)bucketIndex / GROWTH_STAGE_BUCKET_COUNT;
            float end01 = (float)(bucketIndex + 1) / GROWTH_STAGE_BUCKET_COUNT;
            return string.Format("{0}-{1}",
                FormatYears(maxAgeYears * start01),
                FormatYears(maxAgeYears * end01));
        }

        private static string FormatYears(float years)
        {
            if (years >= 10f)
                return years.ToString("F0") + "y";
            return years.ToString("F1") + "y";
        }

        private static string FormatAmount(float value)
        {
            if (value >= 1_000_000f)
            {
                float v = value / 1_000_000f;
                string fmt = v >= 100f ? "F0" : v >= 10f ? "F1" : "F2";
                return v.ToString(fmt) + "M";
            }
            if (value >= 1_000f)
            {
                float v = value / 1_000f;
                string fmt = v >= 100f ? "F0" : v >= 10f ? "F1" : "F2";
                return v.ToString(fmt) + "k";
            }
            return value >= 100f ? value.ToString("F0") : value.ToString("F1");
        }

        private static string FormatPercent(float value)
        {
            value = Math.Max(0f, Math.Min(100f, value));
            return value >= 10f ? value.ToString("F0") + "%" : value.ToString("F1") + "%";
        }

        private static UiComponent BuildMatureTreeIcon(int sizePx)
        {
            return TreeIcon.BuildMature(sizePx);
        }

        private readonly struct PlantingCandidate
        {
            public Tile2i Tile { get; }
            public long DistanceSqrToTower { get; }

            public PlantingCandidate(Tile2i tile, long distanceSqrToTower)
            {
                Tile = tile;
                DistanceSqrToTower = distanceSqrToTower;
            }
        }

        private readonly struct ForestryStats
        {
            public int TreeCount { get; }
            public int TreeCapacity { get; }
            public float MaturityPercent { get; }
            public float AverageAgeYears { get; }
            public float AverageMaxAgeYears { get; }
            public int WoodReserve { get; }
            public float CapacityPerYear { get; }
            public float MaxAgeYears { get; }
            public int[] GrowthBuckets { get; }
            public TreeId[][] BucketTrees { get; }

            public ForestryStats(
                int treeCount,
                int treeCapacity,
                float maturityPercent,
                float averageAgeYears,
                float averageMaxAgeYears,
                int woodReserve,
                float capacityPerYear,
                float maxAgeYears,
                int[] growthBuckets,
                TreeId[][] bucketTrees)
            {
                TreeCount = treeCount;
                TreeCapacity = treeCapacity;
                MaturityPercent = maturityPercent;
                AverageAgeYears = averageAgeYears;
                AverageMaxAgeYears = averageMaxAgeYears;
                WoodReserve = woodReserve;
                CapacityPerYear = capacityPerYear;
                MaxAgeYears = maxAgeYears;
                GrowthBuckets = growthBuckets;
                BucketTrees = bucketTrees;
            }
        }
    }
}
