// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Terrain.Trees;
using Mafi.Localization;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using Row = Mafi.Unity.UiToolkit.Library.Row;

namespace AutoForestryDesignations
{
    internal static class ForestryInfoPanel
    {
        private const int BUCKET_COUNT = 5;

        private static readonly ColorRgba[] s_growthColors =
        {
            new ColorRgba(0x586f58),
            new ColorRgba(0x76a85f),
            new ColorRgba(0x9abd73),
            new ColorRgba(0xc7ad54),
            new ColorRgba(0xd7d08a),
        };

        private static readonly Dictionary<object, Action> s_refreshCallbacks =
            new Dictionary<object, Action>();

        internal static PanelWithHeader Build(Func<IAreaManagingTower?> getTower, object key)
        {
            var contentCol = new Column(2.pt());
            var promptLabel = new Label(new LocStrFormatted("Press \u21ba to scan forestry composition."))
                .Color(Theme.InactiveColor);
            contentCol.Add(promptLabel);

            s_refreshCallbacks[key] = (Action)delegate
            {
                PopulateContent(contentCol, getTower());
            };

            var panel = new PanelWithHeader()
                .Title(new LocStrFormatted("Forestry Composition"),
                       new LocStrFormatted("Current trees and projected wood output in this tower's forestry area."));

            panel.Header.Add(new ButtonIcon(Button.General,
                "Assets/Unity/UserInterface/General/Repeat.svg",
                (Action)delegate
                {
                    PopulateContent(contentCol, getTower());
                })
                .Compact()
                .IconSize(14.px())
                .MarginLeft(4.pt())
                .Tooltip(new LocStrFormatted("Scan forestry composition")));

            panel.BodyAdd(contentCol);
            return panel;
        }

        internal static void RefreshContent(object key)
        {
            if (s_refreshCallbacks.TryGetValue(key, out var cb))
                try { cb?.Invoke(); } catch { }
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
            float ageYearsSum = 0f;
            int woodReserve = 0;
            int[] growthBuckets = new int[BUCKET_COUNT];
            float maxAgeYears = 0f;

            foreach (TreeId treeId in tower.Trees)
            {
                if (!treesManager.Trees.TryGetValue(treeId, out TreeData treeData))
                    continue;

                treeCount++;
                Duration age = currentStep - treeData.PlantedAtTick;
                float ageYears = Math.Max(0f, age.Years.ToFloat());
                float treeMaxAgeYears = Math.Max(0.01f, treeData.Proto.GetTreeMaxAge().Years.ToFloat());
                float growth01 = Math.Min(1f, ageYears / treeMaxAgeYears);
                int bucket = Math.Min(BUCKET_COUNT - 1, Math.Max(0, (int)(growth01 * BUCKET_COUNT)));

                ageYearsSum += ageYears;
                woodReserve += treeData.GetHarvestedQuantityAt(currentStep).Value;
                growthBuckets[bucket]++;
                maxAgeYears = Math.Max(maxAgeYears, treeMaxAgeYears);
            }

            float averageAgeYears = treeCount > 0 ? ageYearsSum / treeCount : 0f;
            float capacityPerYear = EstimateCapacityPerYear(tower, treesManager);
            return new ForestryStats(treeCount, averageAgeYears, woodReserve, capacityPerYear, maxAgeYears, growthBuckets);
        }

        private static float EstimateCapacityPerYear(ForestryTower tower, TreesManager treesManager)
        {
            int effectiveTreeCapacity = Math.Max(tower.GetApproxMaxTreesAllowed(), tower.Trees.Count);
            if (effectiveTreeCapacity <= 0)
                return 0f;

            float weightedYieldPerTreePerYear = EstimateConfiguredYieldPerTreePerYear(tower);
            if (weightedYieldPerTreePerYear <= 0f)
                weightedYieldPerTreePerYear = EstimateCurrentYieldPerTreePerYear(tower, treesManager);

            return effectiveTreeCapacity * weightedYieldPerTreePerYear;
        }

        private static float EstimateConfiguredYieldPerTreePerYear(ForestryTower tower)
        {
            var treeTypes = tower.TreeTypes;
            if (treeTypes.Count == 0)
                return 0f;

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
                    float years = Math.Max(0.01f, treeProto.GetTreeMaxAge().Years.ToFloat());
                    groupYieldPerTreePerYear += treeProto.ProductWhenHarvested.Quantity.Value / years;
                }
                groupYieldPerTreePerYear /= entry.Key.Trees.Length;

                weightedSum += groupYieldPerTreePerYear * entry.Value;
                totalWeight += entry.Value;
            }

            return totalWeight > 0 ? weightedSum / totalWeight : 0f;
        }

        private static float EstimateCurrentYieldPerTreePerYear(ForestryTower tower, TreesManager treesManager)
        {
            float sum = 0f;
            int count = 0;
            foreach (TreeId treeId in tower.Trees)
            {
                if (!treesManager.Trees.TryGetValue(treeId, out TreeData treeData))
                    continue;

                float years = Math.Max(0.01f, treeData.Proto.GetTreeMaxAge().Years.ToFloat());
                sum += treeData.Proto.ProductWhenHarvested.Quantity.Value / years;
                count++;
            }

            return count > 0 ? sum / count : 0f;
        }

        private static Row BuildKpiRow(ForestryStats stats)
        {
            var row = new Row().Gap(2.pt()).AlignItemsStretch();
            row.Add(BuildKpi("Trees", stats.TreeCount.ToString()));
            row.Add(BuildKpi("Avg age", FormatYears(stats.AverageAgeYears)));
            row.Add(BuildKpi("Wood reserve", FormatAmount(stats.WoodReserve)));
            row.Add(BuildKpi("Capacity", FormatAmount(stats.CapacityPerYear) + "/y"));
            return row;
        }

        private static Column BuildKpi(string label, string value)
        {
            var col = new Column()
                .FlexGrow(1f)
                .Background(Theme.BackgroundDark)
                .Padding(4.pt())
                .Gap(1.pt());

            col.Add(new Label(new LocStrFormatted(value)).FontSize(15).FontBold().NoTextWrap());
            col.Add(new Label(new LocStrFormatted(label)).FontSize(11).Color(Theme.InactiveColor).NoTextWrap());
            return col;
        }

        private static Column BuildGrowthSection(ForestryTower tower, ForestryStats stats)
        {
            float maxAgeYears = Math.Max(1f, stats.MaxAgeYears);
            bool harvestDisabled = tower.TargetHarvestPercent >= ForestryTower.NO_CUT_AT;
            float thresholdPercent = harvestDisabled
                ? 0f
                : Math.Min(100f, tower.TargetHarvestPercent.ToFix32().ToFloat());

            var section = new Column(2.pt()).MarginTop(2.pt());
            var header = new Row().AlignItemsCenter();
            header.Add(new Label(new LocStrFormatted("Growth distribution")).FontBold());
            header.Add(new UiComponent().FlexGrow(1f));
            header.Add(new Label(new LocStrFormatted(harvestDisabled
                    ? "Harvest option: no cutting"
                    : "Harvest option: " + FormatYears(maxAgeYears * thresholdPercent / 100f)))
                .FontSize(12)
                .Color(Theme.InactiveColor));
            section.Add(header);

            var ticks = new Row().AlignItemsCenter();
            ticks.Add(BuildTickLabel("0y", TextAlignment.LeftMiddle));
            ticks.Add(BuildTickLabel(FormatYears(maxAgeYears * 0.25f), TextAlignment.CenterMiddle));
            ticks.Add(BuildTickLabel(FormatYears(maxAgeYears * 0.5f), TextAlignment.CenterMiddle));
            ticks.Add(BuildTickLabel(FormatYears(maxAgeYears * 0.75f), TextAlignment.CenterMiddle));
            ticks.Add(BuildTickLabel(FormatYears(maxAgeYears), TextAlignment.RightMiddle));
            section.Add(ticks);

            if (!harvestDisabled)
                section.Add(BuildThresholdMarker(thresholdPercent));

            var bar = new Row().AlignSelfStretch().Height(18.px()).AlignItemsStretch().Background(Theme.BackgroundPanelLike);
            int total = Math.Max(1, stats.TreeCount);
            for (int i = 0; i < BUCKET_COUNT; i++)
            {
                float grow = stats.GrowthBuckets[i] > 0 ? stats.GrowthBuckets[i] : 0.15f;
                var segment = new UiComponent()
                    .FlexGrow(grow)
                    .Background(stats.GrowthBuckets[i] > 0 ? s_growthColors[i] : Theme.BackgroundDark)
                    .Tooltip(new LocStrFormatted(string.Format("{0}-{1}: {2} trees",
                        FormatYears(maxAgeYears * i / BUCKET_COUNT),
                        FormatYears(maxAgeYears * (i + 1) / BUCKET_COUNT),
                        stats.GrowthBuckets[i])));
                bar.Add(segment);
            }
            section.Add(bar);

            var footer = new Label(new LocStrFormatted(
                    "Wood reserve uses vanilla per-tree age yield: 40% age = 30% yield, 60% = 60%, 80% = 88%, 100% = full."))
                .FontSize(11)
                .Color(Theme.InactiveColor);
            section.Add(footer);

            return section;
        }

        private static Row BuildThresholdMarker(float thresholdPercent)
        {
            float left = Math.Max(0f, Math.Min(100f, thresholdPercent));
            float right = Math.Max(0f, 100f - left);
            var row = new Row().AlignSelfStretch().Height(4.px()).AlignItemsStretch();
            row.Add(new UiComponent().FlexGrow(Math.Max(0.1f, left)));
            row.Add(new UiComponent().Width(2.px()).Background(ColorRgba.White));
            row.Add(new UiComponent().FlexGrow(Math.Max(0.1f, right)));
            return row;
        }

        private static Label BuildTickLabel(string text, TextAlignment alignment)
        {
            return new Label(new LocStrFormatted(text))
                .FlexGrow(1f)
                .FontSize(10)
                .Color(Theme.InactiveColor)
                .TextAlign(alignment);
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

        private readonly struct ForestryStats
        {
            public int TreeCount { get; }
            public float AverageAgeYears { get; }
            public int WoodReserve { get; }
            public float CapacityPerYear { get; }
            public float MaxAgeYears { get; }
            public int[] GrowthBuckets { get; }

            public ForestryStats(
                int treeCount,
                float averageAgeYears,
                int woodReserve,
                float capacityPerYear,
                float maxAgeYears,
                int[] growthBuckets)
            {
                TreeCount = treeCount;
                AverageAgeYears = averageAgeYears;
                WoodReserve = woodReserve;
                CapacityPerYear = capacityPerYear;
                MaxAgeYears = maxAgeYears;
                GrowthBuckets = growthBuckets;
            }
        }
    }
}
