// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Terrain.Trees;
using Mafi.Localization;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using UnityEngine;
using UiImage = UnityEngine.UIElements.Image;
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
        private static Texture2D? s_matureTreeTexture;

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

        internal static PanelWithHeader Build(Func<IAreaManagingTower?> getTower, object key)
        {
            var contentCol = new Column(PANEL_GAP_PT.pt());
            var promptLabel = new Label(new LocStrFormatted("Press \u21ba to scan forestry composition."))
                .Color(Theme.InactiveColor);
            contentCol.Add(promptLabel);

            s_refreshCallbacks[key] = (Action)delegate
            {
                PopulateContent(contentCol, getTower());
            };
            s_towerResolvers[key] = getTower;

            var panel = new PanelWithHeader()
                .Title(new LocStrFormatted("Forestry Composition"),
                       new LocStrFormatted("Current trees and projected wood output in this tower's forestry area."));

            var refreshButton = new ButtonIcon(Button.General,
                "Assets/Unity/UserInterface/General/Repeat.svg",
                (Action)delegate
                {
                    PopulateContent(contentCol, getTower());
                })
                .Compact()
                .IconSize(14.px())
                .MarginLeft(4.pt())
                .Tooltip(new LocStrFormatted("Refresh forestry composition"));
            refreshButton.OnClick((ClickEvent evt) => evt.StopPropagation());

            panel.Header.Add(refreshButton);
            panel.BodyAdd(contentCol);
            panel.Collapsed(false);
            return panel;
        }

        internal static void RefreshContent(object key)
        {
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
            col.Clear();

            var forestryTower = tower as ForestryTower;
            var treesManager = AutoForestryDesignation.GetTreesManager();
            var currentStep = AutoForestryDesignation.GetCurrentSimStep();

            if (forestryTower == null || treesManager == null || !currentStep.HasValue)
            {
                col.Add(new Label(new LocStrFormatted("No forestry tower selected.")));
                return;
            }

            var stats = CollectStats(forestryTower, treesManager, currentStep.Value);
            if (stats.TreeCount == 0)
            {
                col.Add(new Label(new LocStrFormatted("No managed trees found.")));
                return;
            }

            col.Add(BuildKpiRow(stats));
            col.Add(BuildGrowthSection(forestryTower, stats));
        }

        private static ForestryStats CollectStats(ForestryTower tower, TreesManager treesManager, SimStep currentStep)
        {
            int treeCount = 0;
            float growthSum01 = 0f;
            int woodReserve = 0;
            int[] growthBuckets = new int[BUCKET_COUNT];
            float maxAgeYears = 0f;

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
                woodReserve += woodThisTree;
                growthBuckets[bucket]++;
                maxAgeYears = Math.Max(maxAgeYears, treeMaxAgeYears);

#if DEBUG
                Log.Info(string.Format("[AFD] tree {0}: proto={1} plantedAt={2} maxAge={3:F1}y growth={4:P0} ageYears={5:F1}y wood={6}",
                    treeId, treeData.Proto.Id, treeData.PlantedAtTick, treeMaxAgeYears, growth01, ageYears, woodThisTree));
#endif
            }

            float averageMaturityPercent = treeCount > 0 ? (growthSum01 / treeCount) * 100f : 0f;
            int treeCapacity = EstimateTreeCapacity(tower, treesManager, treeCount);
            float capacityPerYear = EstimateCapacityPerYear(tower, treesManager, treeCapacity);
#if DEBUG
            Log.Info(string.Format("[AFD] CollectStats: trees={0}/{1} woodReserve={2} maturity={3:F1}% maxAge={4:F1}y buckets=[{5}] capacity/month={6:F1}",
                treeCount, treeCapacity, woodReserve, averageMaturityPercent, maxAgeYears,
                string.Join(",", growthBuckets), capacityPerYear / 12f));
#endif
            return new ForestryStats(treeCount, treeCapacity, averageMaturityPercent, woodReserve, capacityPerYear, maxAgeYears, growthBuckets);
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

#if DEBUG
            Log.Info(string.Format(
                "[AFD] EstimateTreeCapacity: live={0} spacing={1} validNow={2} future={3} => capacity={4}",
                liveManagedTreeCount, spacing, candidates.Count, futureTrees.Count,
                liveManagedTreeCount + futureTrees.Count));
#endif
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
#if DEBUG
                Log.Info("[AFD] EstimateCapacityPerYear: effectiveTreeCapacity=0, returning 0");
#endif
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
#if DEBUG
            Log.Info(string.Format(
                "[AFD] EstimateCapacityPerYear: effectiveCap={0} harvestDisabled={1} harvestGrowth={2:P0} yieldPerTree/y={3:F2} (fallback={4}) => capacity/y={5:F1}",
                effectiveTreeCapacity,
                harvestDisabled, harvestGrowth01,
                weightedYieldPerTreePerYear, usedFallback, capacity));
            Log.Info(string.Format("[AFD] NOTE: capacity/month={0:F1} (capacity/y={1:F1})", capacity / 12f, capacity));
#endif
            return capacity;
        }

        private static float EstimateConfiguredYieldPerTreePerYear(ForestryTower tower, float harvestGrowth01)
        {
            var treeTypes = tower.TreeTypes;
            if (treeTypes.Count == 0)
            {
#if DEBUG
                Log.Info("[AFD] EstimateConfiguredYieldPerTreePerYear: no configured tree types");
#endif
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
#if DEBUG
                    Log.Info(string.Format(
                        "[AFD]   configured group '{0}' proto '{1}': maxAge={2:F1}y harvestAge={3:F1}y yieldAtHarvest={4} yieldPerYear={5:F2}",
                        entry.Key.Id, treeProto.Id, maxAgeY, harvestAgeYears, yieldAtHarvest, yieldPerYear));
#endif
                }
                groupYieldPerTreePerYear /= entry.Key.Trees.Length;

                weightedSum += groupYieldPerTreePerYear * entry.Value;
                totalWeight += entry.Value;
            }

            float result = totalWeight > 0 ? weightedSum / totalWeight : 0f;
#if DEBUG
            Log.Info(string.Format("[AFD] EstimateConfiguredYieldPerTreePerYear: weightedSum={0:F2} totalWeight={1} => {2:F2}/tree/y",
                weightedSum, totalWeight, result));
#endif
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
#if DEBUG
                Log.Info(string.Format(
                    "[AFD]   current tree proto '{0}': maxAge={1:F1}y harvestAge={2:F1}y yieldAtHarvest={3} yieldPerYear={4:F2}",
                    treeData.Proto.Id, maxAgeY, harvestAgeYears, yieldAtHarvest, yieldPerYear));
#endif
            }

            float result = count > 0 ? sum / count : 0f;
#if DEBUG
            Log.Info(string.Format("[AFD] EstimateCurrentYieldPerTreePerYear: sum={0:F2} count={1} => {2:F2}/tree/y",
                sum, count, result));
#endif
            return result;
        }

        private static Row BuildKpiRow(ForestryStats stats)
        {
            var row = new Row().Gap(PANEL_GAP_PT.pt()).AlignItemsStretch().AlignSelfStretch();
            row.Add(BuildKpi(
                "Trees",
                string.Format("{0}/{1}", stats.TreeCount, stats.TreeCapacity),
                () => BuildMatureTreeIcon(44),
                "Managed trees currently inside this tower's forestry designations. First number is current managed trees. Second number is estimated capacity based on currently valid planting positions."));
            row.Add(BuildKpi(
                "Average Maturity",
                FormatPercent(stats.MaturityPercent),
                "Assets/Base/Products/Icons/TreeSapling.svg",
                "Average growth maturity across all managed trees, relative to full growth term (max age, e.g. 12y), not the tower harvest age setting."));
            row.Add(BuildKpi(
                "Production Capacity",
                FormatAmount(stats.CapacityPerYear / 12f),
                "Assets/Base/Products/Icons/Wood.svg",
                "Projected sustainable wood output per in-game month (60 real-time seconds at 1x speed), based on species mix and harvest threshold."));
            return row;
        }

        private static Column BuildKpi(string label, string value, string iconPath, string tooltip)
        {
            return BuildKpi(label, value, () => new Icon(iconPath).NoTint().Size(40.px()), tooltip);
        }

        private static Column BuildKpi(string label, string value, Func<UiComponent> buildIcon, string tooltip)
        {
            var col = new Column()
                .FlexGrow(1f)
                .Background(Theme.BackgroundDark)
                .OverflowHidden()
                .Padding(CARD_PADDING_PT.pt())
                .Gap(2.pt());

            col.BorderRadius(8);
            col.Border(1.px(), Theme.BorderColor, 8);
            col.Tooltip(new LocStrFormatted(tooltip));

            var topRow = new Row().AlignItemsCenter().Gap(4.pt());
            topRow.Add(buildIcon());
            var textCol = new Column(1.pt());
            textCol.Add(new Label(new LocStrFormatted(label)).FontSize(13).FontBold().NoTextWrap());
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
                "Vanilla forestry yield curve by maturity: 40% age = 30% yield, 60% = 60%, 80% = 88%, 100% = full."));

            var header = new Row().AlignItemsCenter();
            header.Add(new Label(new LocStrFormatted("Growth Distribution")).FontBold());
            header.Tooltip(new LocStrFormatted(harvestDisabled
                ? "Distribution by maturity relative to full growth term. Harvest option: no cutting."
                : "Distribution by maturity relative to full growth term. Harvest option: " +
                  FormatYears(maxAgeYears * thresholdPercent / 100f) +
                  " (" + thresholdPercent.ToString("F0") + "%)."));
            section.Add(header);

            var bar = new Row().AlignSelfStretch().Height(20.px()).AlignItemsStretch().Background(Theme.BackgroundPanelLike);
            int plantedTotal = Math.Max(1, stats.TreeCount);
            for (int i = 0; i < BUCKET_COUNT; i++)
            {
                int bucketCount = stats.GrowthBuckets[i];
                if (bucketCount <= 0)
                    continue;

                float bucketMidpoint01 = GetBucketMidpoint01(i);
                bool isAboveHarvest = !harvestDisabled && bucketMidpoint01 >= threshold01;
                var bucketColor = isAboveHarvest ? s_aboveHarvestColors[i] : s_belowHarvestColors[i];
                string bucketLabel = GetBucketLabel(i, maxAgeYears);

                var segment = new UiComponent()
                    .FlexGrow(bucketCount)
                    .Background(bucketColor)
                    .Tooltip(new LocStrFormatted(string.Format("{0}: {1} trees ({2:P0} of planted, {3:P0} of capacity) [{4}]",
                        bucketLabel,
                        bucketCount,
                        (float)bucketCount / plantedTotal,
                        (float)bucketCount / totalCapacity,
                        isAboveHarvest ? "at or above harvest threshold" : "below harvest threshold")));
                bar.Add(segment);
            }

            if (unusedCapacity > 0)
            {
                bar.Add(new UiComponent()
                    .FlexGrow(unusedCapacity)
                    .Background(s_unusedCapacityColor)
                    .Tooltip(new LocStrFormatted(string.Format("Unused capacity: {0} tiles ({1:P0} of capacity)",
                        unusedCapacity,
                        (float)unusedCapacity / totalCapacity))));
            }

            var barWithLegend = new Row(3.pt()).AlignSelfStretch().AlignItemsCenter();
            barWithLegend.Add(new Icon("Assets/Base/Products/Icons/TreeSapling.svg").NoTint().Size(16.px())
                .Tooltip(new LocStrFormatted("Newly planted / lowest maturity")));
            barWithLegend.Add(bar.FlexGrow(1f));
            barWithLegend.Add(BuildMatureTreeIcon(18)
                .Tooltip(new LocStrFormatted("Fully grown / highest maturity")));

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
                return "Fully mature (100%)";

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
            return new RuntimeTextureIcon(GetMatureTreeTexture(), sizePx);
        }

        private static Texture2D GetMatureTreeTexture()
        {
            if (s_matureTreeTexture != null)
                return s_matureTreeTexture;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            texture.name = "AFD_MatureTreeIcon";
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new UnityEngine.Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = UnityEngine.Color.clear;

            var trunk = HexColor(0x8f5a2c);
            var trunkDark = HexColor(0x6e4122);
            var leaf = HexColor(0x4fa65d);
            var leafDark = HexColor(0x2f7b44);
            var leafOutline = HexColor(0x1f5a31);
            var leafHighlight = HexColor(0x86c77c);

            FillEllipse(pixels, size, 20, 14, 24, 26, leafOutline);
            FillEllipse(pixels, size, 6, 25, 24, 24, leafOutline);
            FillEllipse(pixels, size, 30, 24, 26, 25, leafOutline);
            FillEllipse(pixels, size, 17, 6, 30, 28, leafOutline);

            FillEllipse(pixels, size, 22, 16, 21, 23, leaf);
            FillEllipse(pixels, size, 9, 27, 21, 21, leaf);
            FillEllipse(pixels, size, 31, 26, 22, 21, leafDark);
            FillEllipse(pixels, size, 19, 9, 26, 25, leaf);

            FillRect(pixels, size, 27, 36, 10, 21, trunk);
            FillRect(pixels, size, 32, 36, 6, 21, trunkDark);
            FillCircle(pixels, size, 21, 24, 3, leafHighlight);
            FillCircle(pixels, size, 16, 35, 3, leafHighlight);

            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            s_matureTreeTexture = texture;
            return texture;
        }

        private static UnityEngine.Color HexColor(int rgb)
        {
            return new UnityEngine.Color(
                ((rgb >> 16) & 0xff) / 255f,
                ((rgb >> 8) & 0xff) / 255f,
                (rgb & 0xff) / 255f,
                1f);
        }

        private static void FillRect(UnityEngine.Color[] pixels, int size, int x, int y, int width, int height, UnityEngine.Color color)
        {
            for (int yy = y; yy < y + height; yy++)
                for (int xx = x; xx < x + width; xx++)
                    SetPixel(pixels, size, xx, yy, color);
        }

        private static void FillCircle(UnityEngine.Color[] pixels, int size, int cx, int cy, int radius, UnityEngine.Color color)
        {
            FillEllipse(pixels, size, cx - radius, cy - radius, radius * 2 + 1, radius * 2 + 1, color);
        }

        private static void FillEllipse(UnityEngine.Color[] pixels, int size, int x, int y, int width, int height, UnityEngine.Color color)
        {
            float rx = width / 2f;
            float ry = height / 2f;
            float cx = x + rx;
            float cy = y + ry;
            for (int yy = y; yy < y + height; yy++)
            {
                for (int xx = x; xx < x + width; xx++)
                {
                    float nx = (xx + 0.5f - cx) / rx;
                    float ny = (yy + 0.5f - cy) / ry;
                    if (nx * nx + ny * ny <= 1f)
                        SetPixel(pixels, size, xx, yy, color);
                }
            }
        }

        private static void SetPixel(UnityEngine.Color[] pixels, int size, int x, int y, UnityEngine.Color color)
        {
            if (x < 0 || x >= size || y < 0 || y >= size)
                return;
            pixels[(size - 1 - y) * size + x] = color;
        }

        private sealed class RuntimeTextureIcon : UiComponent<UiImage>
        {
            public RuntimeTextureIcon(Texture2D texture, int sizePx)
                : base(new UiImage())
            {
                Element.image = texture;
                this.Size(sizePx.px());
            }
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
            public int WoodReserve { get; }
            public float CapacityPerYear { get; }
            public float MaxAgeYears { get; }
            public int[] GrowthBuckets { get; }

            public ForestryStats(
                int treeCount,
                int treeCapacity,
                float maturityPercent,
                int woodReserve,
                float capacityPerYear,
                float maxAgeYears,
                int[] growthBuckets)
            {
                TreeCount = treeCount;
                TreeCapacity = treeCapacity;
                MaturityPercent = maturityPercent;
                WoodReserve = woodReserve;
                CapacityPerYear = capacityPerYear;
                MaxAgeYears = maxAgeYears;
                GrowthBuckets = growthBuckets;
            }
        }
    }
}
