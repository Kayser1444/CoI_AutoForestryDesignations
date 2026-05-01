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
    /// Builds the "Terrain Designations" inspector panel independently of any specific
    /// inspector type. Call <see cref="Build"/> and insert the returned panel wherever
    /// needed. Can be used by external mods via <see cref="AutoForestryDesignationsApi"/>.
    /// </summary>
    internal static class DesignationPanel
    {
        private static ProtosDb? s_protosDb;

        private sealed class Bindings
        {
            public Func<IAreaManagingTower?> GetTower { get; }
            public Mafi.Unity.Ui.Library.Display AvoidInfertileDisplay { get; }
            public Mafi.Unity.Ui.Library.Display AvoidWithTreesDisplay { get; }
            public Mafi.Unity.Ui.Library.Display MaxTilesDisplay { get; }
            public Mafi.Unity.Ui.Library.Display MarkGrownDisplay { get; }

            public Bindings(
                Func<IAreaManagingTower?> getTower,
                Mafi.Unity.Ui.Library.Display avoidInfertileDisplay,
                Mafi.Unity.Ui.Library.Display avoidWithTreesDisplay,
                Mafi.Unity.Ui.Library.Display maxTilesDisplay,
                Mafi.Unity.Ui.Library.Display markGrownDisplay)
            {
                GetTower = getTower;
                AvoidInfertileDisplay = avoidInfertileDisplay;
                AvoidWithTreesDisplay = avoidWithTreesDisplay;
                MaxTilesDisplay = maxTilesDisplay;
                MarkGrownDisplay = markGrownDisplay;
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
            b.AvoidInfertileDisplay.SetValue(new LocStrFormatted(BoolText(AutoDepthDesignation.GetTowerAvoidInfertileTiles(tower))));
            b.AvoidWithTreesDisplay.SetValue(new LocStrFormatted(BoolText(AutoDepthDesignation.GetTowerAvoidTilesWithTrees(tower))));
            b.MaxTilesDisplay.SetValue(new LocStrFormatted(MaxTilesText(AutoDepthDesignation.GetTowerMaxTiles(tower))));
            b.MarkGrownDisplay.SetValue(new LocStrFormatted(BoolText(AutoDepthDesignation.GetTowerMarkFullyGrownForHarvest(tower))));
        }

        /// <summary>
        /// Builds the full "Terrain Designations" panel and returns it. Insert the result
        /// at any position in any inspector's <c>Column</c>.
        /// </summary>
        /// <param name="getTower">
        /// Delegate that returns the currently active tower, called lazily inside button
        /// handlers and display refresh. May return null between inspector activations.
        /// </param>
        /// <param name="key">
        /// Opaque key (typically the inspector instance) used to route
        /// <see cref="RefreshDisplays"/> calls back to this panel. Pass the same key to
        /// <see cref="AutoDepthDesignation.CreateDesignationsForTower(IAreaManagingTower, object?)"/>
        /// so the Ore Composition panel auto-refreshes after a scan.
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
                        AutoDepthDesignation.CreateDesignationsForTower(tower, key);
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
                        AutoDepthDesignation.ClearDesignationsForTower(tower);
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
                       new LocStrFormatted($"Create automatic forestry designations. [AutoForestryDesignations v{AutoForestryDesignationsMod.ModVersion}]"));
            panel.Collapsed(false);
            panel.BodyAdd(contentRow);

            var initialTower = getTower();

            // --- Avoid infertile tiles ---
            bool initAvoidInfertile = initialTower != null
                ? AutoDepthDesignation.GetTowerAvoidInfertileTiles(initialTower)
                : AutoForestryDesignationsMod.AvoidInfertileTiles;
            var avoidInfertileDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(BoolText(initAvoidInfertile)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildToggleRow(
                new LocStrFormatted("Avoid infertile tiles"),
                new LocStrFormatted("Skip tiles where the ground is not fertile for tree growth (e.g. rock, sand, ocean). Infertile tiles show as yellow designations."),
                avoidInfertileDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerAvoidInfertileTiles(tower, true);
                    avoidInfertileDisplay.SetValue(new LocStrFormatted(BoolText(true)));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerAvoidInfertileTiles(tower, false);
                    avoidInfertileDisplay.SetValue(new LocStrFormatted(BoolText(false)));
                }));

            // --- Avoid tiles with trees ---
            bool initAvoidTrees = initialTower != null
                ? AutoDepthDesignation.GetTowerAvoidTilesWithTrees(initialTower)
                : AutoForestryDesignationsMod.AvoidTilesWithTrees;
            var avoidWithTreesDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(BoolText(initAvoidTrees)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildToggleRow(
                new LocStrFormatted("Avoid tiles with trees"),
                new LocStrFormatted("Skip tiles that already contain a tree. When off, designations are placed on all fertile tiles including occupied ones (they show yellow until the tree is harvested)."),
                avoidWithTreesDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerAvoidTilesWithTrees(tower, true);
                    avoidWithTreesDisplay.SetValue(new LocStrFormatted(BoolText(true)));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerAvoidTilesWithTrees(tower, false);
                    avoidWithTreesDisplay.SetValue(new LocStrFormatted(BoolText(false)));
                }));

            // --- Max tiles ---
            int initMaxTiles = initialTower != null
                ? AutoDepthDesignation.GetTowerMaxTiles(initialTower)
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
                    int cur = AutoDepthDesignation.GetTowerMaxTiles(tower);
                    AutoDepthDesignation.SetTowerMaxTiles(tower, cur == 0 ? ModifierStepSize() : cur + ModifierStepSize());
                    maxTilesDisplay.SetValue(new LocStrFormatted(MaxTilesText(AutoDepthDesignation.GetTowerMaxTiles(tower))));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    int cur = AutoDepthDesignation.GetTowerMaxTiles(tower);
                    if (cur > 0)
                        AutoDepthDesignation.SetTowerMaxTiles(tower, Math.Max(0, cur - ModifierStepSize()));
                    maxTilesDisplay.SetValue(new LocStrFormatted(MaxTilesText(AutoDepthDesignation.GetTowerMaxTiles(tower))));
                }));

            // --- Mark fully grown for harvest ---
            bool initMarkGrown = initialTower != null
                ? AutoDepthDesignation.GetTowerMarkFullyGrownForHarvest(initialTower)
                : AutoForestryDesignationsMod.MarkFullyGrownForHarvest;
            var markGrownDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(BoolText(initMarkGrown)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildToggleRow(
                new LocStrFormatted("Mark grown for harvest"),
                new LocStrFormatted("When enabled, all fully grown trees in the tower area are marked for harvesting each time Create Designations is run."),
                markGrownDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerMarkFullyGrownForHarvest(tower, true);
                    markGrownDisplay.SetValue(new LocStrFormatted(BoolText(true)));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerMarkFullyGrownForHarvest(tower, false);
                    markGrownDisplay.SetValue(new LocStrFormatted(BoolText(false)));
                }));

            s_bindings[key] = new Bindings(getTower, avoidInfertileDisplay, avoidWithTreesDisplay, maxTilesDisplay, markGrownDisplay);
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
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 10;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 5;
            return 1;
        }

        private static string BoolText(bool value) => value ? "YES" : "NO";
        private static string MaxTilesText(int value) => value == 0 ? "\u221e" : value.ToString();
    }
}
