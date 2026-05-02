// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
// Auto Forestry Designations - Terrain Designation Panel
using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Localization;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using Row = Mafi.Unity.UiToolkit.Library.Row;
using UnityEngine;

namespace AutoForestryDesignations
{
    /// <summary>
    /// Builds the "Forestry Designations" inspector panel independently of any specific
    /// inspector type. Call <see cref="Build"/> and insert the returned panel wherever
    /// needed. Can be used by external mods via <see cref="AutoForestryDesignationsApi"/>.
    /// </summary>
    internal static class DesignationPanel
    {
        private static ProtosDb? s_protosDb;

        private sealed class Bindings
        {
            public Func<IAreaManagingTower?> GetTower { get; }
            public Mafi.Unity.Ui.Library.Display OnlyFertileDisplay { get; }
            public Mafi.Unity.Ui.Library.Display AvoidWithTreesDisplay { get; }
            public Mafi.Unity.Ui.Library.Display AvoidMiningDisplay { get; }
            public Mafi.Unity.Ui.Library.Display OnlyReachableDisplay { get; }
            public Mafi.Unity.Ui.Library.Display MaxTilesDisplay { get; }
            public Mafi.Unity.Ui.Library.Display MarkHarvestReadyDisplay { get; }

            public Bindings(
                Func<IAreaManagingTower?> getTower,
                Mafi.Unity.Ui.Library.Display onlyFertileDisplay,
                Mafi.Unity.Ui.Library.Display avoidWithTreesDisplay,
                Mafi.Unity.Ui.Library.Display avoidMiningDisplay,
                Mafi.Unity.Ui.Library.Display onlyReachableDisplay,
                Mafi.Unity.Ui.Library.Display maxTilesDisplay,
                Mafi.Unity.Ui.Library.Display markHarvestReadyDisplay)
            {
                GetTower = getTower;
                OnlyFertileDisplay = onlyFertileDisplay;
                AvoidWithTreesDisplay = avoidWithTreesDisplay;
                AvoidMiningDisplay = avoidMiningDisplay;
                OnlyReachableDisplay = onlyReachableDisplay;
                MaxTilesDisplay = maxTilesDisplay;
                MarkHarvestReadyDisplay = markHarvestReadyDisplay;
            }
        }

        private static readonly Dictionary<object, Bindings> s_bindings =
            new Dictionary<object, Bindings>();

        internal static void Initialize(ProtosDb? protosDb)
        {
            s_protosDb = protosDb;
        }

        /// <summary>
        /// Refreshes the display values of a previously built panel.
        /// Call this when the inspector switches to a different tower.
        /// </summary>
        internal static void RefreshDisplays(object key)
        {
            if (!s_bindings.TryGetValue(key, out var b)) return;
            var tower = b.GetTower();
            if (tower == null) return;
            b.OnlyFertileDisplay.SetValue(new LocStrFormatted(BoolText(AutoForestryDesignation.GetTowerOnlyFertileTiles(tower))));
            b.AvoidWithTreesDisplay.SetValue(new LocStrFormatted(BoolText(AutoForestryDesignation.GetTowerAvoidTilesWithTrees(tower))));
            b.AvoidMiningDisplay.SetValue(new LocStrFormatted(BoolText(AutoForestryDesignation.GetTowerAvoidMiningDesignations(tower))));
            b.OnlyReachableDisplay.SetValue(new LocStrFormatted(BoolText(AutoForestryDesignation.GetTowerOnlyReachableTiles(tower))));
            b.MaxTilesDisplay.SetValue(new LocStrFormatted(MaxTilesText(AutoForestryDesignation.GetTowerMaxTiles(tower))));
            b.MarkHarvestReadyDisplay.SetValue(new LocStrFormatted(BoolText(AutoForestryDesignation.GetTowerMarkHarvestReadyForHarvest(tower))));
        }

        /// <summary>
        /// Builds the full "Forestry Designations" panel and returns it. Insert the result
        /// at any position in any inspector's <c>Column</c>.
        /// </summary>
        /// <param name="getTower">
        /// Delegate that returns the currently active tower, called lazily inside button
        /// handlers and display refresh. May return null between inspector activations.
        /// </param>
        /// <param name="key">
        /// Opaque key (typically the inspector instance) used to route
        /// <see cref="RefreshDisplays"/> calls back to this panel.
        /// </param>
        internal static PanelWithHeader Build(Func<IAreaManagingTower?> getTower, object key)
        {
            // --- Create / Clear buttons ---
            var createBtn = new ButtonIconText(
                Button.Primary,
                "Assets/Unity/UserInterface/EntityIcons/Designation.png",
                new LocStrFormatted("Create Designations"))
                .OnClick((Action)delegate
                {
                    try
                    {
                        var tower = getTower();
                        if (tower == null) return;
                        AutoForestryDesignation.CreateDesignationsForTower(tower);
                    }
                    catch (Exception ex) { Debug.Log($"[AFD] Create button EXCEPTION: {ex}"); }
                });
            createBtn.Tooltip(new LocStrFormatted("Scan the tower area and place forestry designations."));
            createBtn.Icon.Size(Px.Auto, 24.px());

            var clearBtn = new ButtonIcon(
                Button.General,
                "Assets/Unity/UserInterface/General/Trash128.png",
                (Action)delegate
                {
                    try
                    {
                        var tower = getTower();
                        if (tower == null) return;
                        AutoForestryDesignation.ClearDesignationsForTower(tower);
                    }
                    catch (Exception ex) { Debug.Log($"[AFD] Clear button EXCEPTION: {ex}"); }
                })
                .Tooltip(new LocStrFormatted("Clear all forestry designations in this tower's area."));

            createBtn.MarginTopBottom(1.pt());
            clearBtn.MarginTopBottom(1.pt()).AlignSelfEnd();

            var contentRow = new Row().Gap(3.pt()).AlignItemsEnd();
            contentRow.Add(new UiComponent().FlexGrow(1f));
            contentRow.Add(createBtn);
            contentRow.Add(new UiComponent().FlexGrow(1f));
            contentRow.Add(clearBtn);

            var panel = new PanelWithHeader()
                .Title(new LocStrFormatted("Forestry Designations"),
                       new LocStrFormatted($"Create automatic forestry designations. [Kayser's Automatic Forestry Designations v{AutoForestryDesignationsMod.ModVersion}]"));
            panel.Collapsed(false);
            panel.BodyAdd(contentRow);

            var initialTower = getTower();

            // --- Only fertile tiles ---
            bool initOnlyFertile = initialTower != null
                ? AutoForestryDesignation.GetTowerOnlyFertileTiles(initialTower)
                : AutoForestryDesignationsMod.OnlyFertileTiles;
            var onlyFertileDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(BoolText(initOnlyFertile)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildToggleRow(
                new LocStrFormatted("Only fertile tiles"),
                new LocStrFormatted("Place designations only where the ground is fertile for tree growth (e.g. not rock, sand, or ocean)."),
                onlyFertileDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerOnlyFertileTiles(tower, true);
                    onlyFertileDisplay.SetValue(new LocStrFormatted(BoolText(true)));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerOnlyFertileTiles(tower, false);
                    onlyFertileDisplay.SetValue(new LocStrFormatted(BoolText(false)));
                }));

            // --- Avoid tiles with trees ---
            bool initAvoidTrees = initialTower != null
                ? AutoForestryDesignation.GetTowerAvoidTilesWithTrees(initialTower)
                : AutoForestryDesignationsMod.AvoidTilesWithTrees;
            var avoidWithTreesDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(BoolText(initAvoidTrees)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());

            // --- Avoid mining designations ---
            bool initAvoidMining = initialTower != null
                ? AutoForestryDesignation.GetTowerAvoidMiningDesignations(initialTower)
                : AutoForestryDesignationsMod.AvoidMiningDesignations;
            var avoidMiningDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(BoolText(initAvoidMining)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildToggleRow(
                new LocStrFormatted("Avoid terrain designations"),
                new LocStrFormatted("Skip tiles that already contain any terrain designation, including mining, dumping, or leveling."),
                avoidMiningDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerAvoidMiningDesignations(tower, true);
                    avoidMiningDisplay.SetValue(new LocStrFormatted(BoolText(true)));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerAvoidMiningDesignations(tower, false);
                    avoidMiningDisplay.SetValue(new LocStrFormatted(BoolText(false)));
                }));

            // --- Only reachable tiles ---
            bool initOnlyReachable = initialTower != null
                ? AutoForestryDesignation.GetTowerOnlyReachableTiles(initialTower)
                : AutoForestryDesignationsMod.OnlyReachableTiles;
            var onlyReachableDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(BoolText(initOnlyReachable)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildToggleRow(
                new LocStrFormatted("Only reachable tiles"),
                new LocStrFormatted("When enabled, skip designation tiles that are not reachable by vehicle pathability from the tower area."),
                onlyReachableDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerOnlyReachableTiles(tower, true);
                    onlyReachableDisplay.SetValue(new LocStrFormatted(BoolText(true)));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerOnlyReachableTiles(tower, false);
                    onlyReachableDisplay.SetValue(new LocStrFormatted(BoolText(false)));
                }));

            // --- Max tiles ---
            int initMaxTiles = initialTower != null
                ? AutoForestryDesignation.GetTowerMaxTiles(initialTower)
                : AutoForestryDesignationsMod.MaxTiles;
            var maxTilesDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(MaxTilesText(initMaxTiles)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildStepRow(
                new LocStrFormatted("Max tiles"),
                new LocStrFormatted("Maximum number of designation tiles to place per run. 0 = no limit (\u221e)."),
                maxTilesDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    int cur = AutoForestryDesignation.GetTowerMaxTiles(tower);
                    AutoForestryDesignation.SetTowerMaxTiles(tower, cur == 0 ? ModifierStepSize() : cur + ModifierStepSize());
                    maxTilesDisplay.SetValue(new LocStrFormatted(MaxTilesText(AutoForestryDesignation.GetTowerMaxTiles(tower))));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    int cur = AutoForestryDesignation.GetTowerMaxTiles(tower);
                    if (cur > 0)
                        AutoForestryDesignation.SetTowerMaxTiles(tower, Math.Max(0, cur - ModifierStepSize()));
                    maxTilesDisplay.SetValue(new LocStrFormatted(MaxTilesText(AutoForestryDesignation.GetTowerMaxTiles(tower))));
                }));

            // --- Mark harvest-ready trees for harvest ---
            bool initMarkHarvestReady = initialTower != null
                ? AutoForestryDesignation.GetTowerMarkHarvestReadyForHarvest(initialTower)
                : AutoForestryDesignationsMod.MarkHarvestReadyForHarvest;
            var markHarvestReadyDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(BoolText(initMarkHarvestReady)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());

            s_bindings[key] = new Bindings(getTower, onlyFertileDisplay, avoidWithTreesDisplay, avoidMiningDisplay, onlyReachableDisplay, maxTilesDisplay, markHarvestReadyDisplay);
            return panel;
        }

        private static Row BuildStepRow(
            LocStrFormatted label,
            LocStrFormatted tooltip,
            Mafi.Unity.Ui.Library.Display display,
            Action onPlus,
            Action onMinus)
        {
            var plusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Plus128.png")
                .Compact().IconSize(14.px()).OnClick(onPlus, allowKeyPresses: true);
            var minusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Minus128.png")
                .Compact().IconSize(14.px()).OnClick(onMinus, allowKeyPresses: true);
            var row = new Row().MarginTop(1.pt());
            row.Add(new Label(label).Tooltip(tooltip));
            row.Add(new UiComponent().FlexGrow(1f));
            row.Add(minusBtn);
            row.Add(display);
            row.Add(plusBtn);
            return row;
        }

        private static Row BuildToggleRow(
            LocStrFormatted label,
            LocStrFormatted tooltip,
            Mafi.Unity.Ui.Library.Display display,
            Action onYes,
            Action onNo)
        {
            var yesBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Plus128.png")
                .Compact().IconSize(14.px()).OnClick(onYes, allowKeyPresses: true);
            var noBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Minus128.png")
                .Compact().IconSize(14.px()).OnClick(onNo, allowKeyPresses: true);
            var row = new Row().MarginTop(1.pt());
            row.Add(new Label(label).Tooltip(tooltip));
            row.Add(new UiComponent().FlexGrow(1f));
            row.Add(noBtn);
            row.Add(display);
            row.Add(yesBtn);
            return row;
        }

        private static int ModifierStepSize()
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 100;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 10;
            return 1;
        }

        private static string BoolText(bool value) => value ? "YES" : "NO";
        private static string MaxTilesText(int value) => value == 0 ? "\u221e" : value.ToString();
    }
}
