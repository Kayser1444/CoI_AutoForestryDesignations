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
            public Mafi.Unity.Ui.Library.Display RampWidthDisplay { get; }
            public Mafi.Unity.Ui.Library.Display MaxLayersDisplay { get; }
            public Mafi.Unity.Ui.Library.Display MinElevDisplay { get; }
            public Mafi.Unity.Ui.Library.Display OrePurityDisplay { get; }
            public Mafi.Unity.Ui.Library.Display ClearanceDisplay { get; }

            public Bindings(
                Func<IAreaManagingTower?> getTower,
                Mafi.Unity.Ui.Library.Display rampWidthDisplay,
                Mafi.Unity.Ui.Library.Display maxLayersDisplay,
                Mafi.Unity.Ui.Library.Display minElevDisplay,
                Mafi.Unity.Ui.Library.Display orePurityDisplay,
                Mafi.Unity.Ui.Library.Display clearanceDisplay)
            {
                GetTower = getTower;
                RampWidthDisplay = rampWidthDisplay;
                MaxLayersDisplay = maxLayersDisplay;
                MinElevDisplay = minElevDisplay;
                OrePurityDisplay = orePurityDisplay;
                ClearanceDisplay = clearanceDisplay;
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
            b.RampWidthDisplay.SetValue(new LocStrFormatted(RampWidthText(AutoDepthDesignation.GetTowerRampWidth(tower))));
            b.MaxLayersDisplay.SetValue(new LocStrFormatted(MaxLayersText(AutoDepthDesignation.GetTowerMaxLayersToExcavate(tower))));
            b.MinElevDisplay.SetValue(new LocStrFormatted(MinElevText(AutoDepthDesignation.GetTowerMaxDepthToDigTo(tower))));
            b.OrePurityDisplay.SetValue(new LocStrFormatted(OrePurityLevelText(AutoDepthDesignation.GetTowerOrePurityLevel(tower))));
            b.ClearanceDisplay.SetValue(new LocStrFormatted(ClearanceLevelText(AutoDepthDesignation.GetTowerCorridorClearance(tower))));
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
            // --- Ore picker ---
            SingleProductPickerUi? orePicker = null;
            if (s_protosDb != null)
            {
                try
                {
                    var minableProducts = s_protosDb.All<TerrainMaterialProto>()
                        .Select(m => (ProductProto)m.MinedProduct)
                        .Distinct()
                        .ToList();

                    orePicker = new SingleProductPickerUi(
                        () => minableProducts,
                        delegate(ProductProto selected)
                        {
                            var tower = getTower();
                            if (tower == null) return;
                            if (selected is LooseProductProto loose)
                                AutoDepthDesignation.SetSelectedOre(tower, loose);
                        },
                        () =>
                        {
                            var tower = getTower();
                            if (tower == null) return Option.None;
                            var sel = AutoDepthDesignation.GetSelectedOre(tower);
                            return sel != null ? Option.Some((ProductProto)sel) : Option.None;
                        },
                        delegate
                        {
                            var tower = getTower();
                            if (tower == null) return;
                            AutoDepthDesignation.SetSelectedOre(tower, null);
                        },
                        new LocStrFormatted("None (all useful products)"),
                        compact: true,
                        primaryButtonIfNoProtoSet: false
                    );
                }
                catch (Exception ex)
                {
                    Log.Warning($"[AFD] EXCEPTION creating ore picker in DesignationPanel: {ex}");
                }
            }

            // --- Dig button ---
            var digBtn = new ButtonIconText(
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
                    catch (Exception ex) { Debug.Log($"[AFD] Dig button click EXCEPTION: {ex}"); }
                });
            digBtn.Tooltip(new LocStrFormatted("Scan and place mining designations in this tower's area."));
            digBtn.Icon.Size(Px.Auto, 24.px());

            // --- Clear button ---
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
                    catch (Exception ex) { Debug.Log($"[AFD] Clear button click EXCEPTION: {ex}"); }
                })
                .Tooltip(new LocStrFormatted("Clear all mining designations in this tower's area."));

            digBtn.MarginTopBottom(1.pt());
            clearBtn.MarginTopBottom(1.pt()).AlignSelfEnd();

            var contentRow = new Row().Gap(3.pt()).AlignItemsEnd();
            contentRow.Add(new UiComponent().FlexGrow(1f));
            contentRow.Add(digBtn);
            contentRow.Add(new UiComponent().FlexGrow(1f));
            contentRow.Add(clearBtn);

            var panel = new PanelWithHeader()
                .Title(new LocStrFormatted("Terrain Designations"),
                       new LocStrFormatted($"Create automatic terrain designations for this tower. [Kayser's AutoForestryDesignations v{AutoForestryDesignationsMod.ModVersion}]"));
            panel.Collapsed(false);
            panel.BodyAdd(contentRow);

            // Read initial values from tower if available, fall back to globals
            var initialTower = getTower();

            // --- Ramp width ---
            int initRamp = initialTower != null
                ? AutoDepthDesignation.GetTowerRampWidth(initialTower)
                : AutoForestryDesignationsMod.RampWidth;
            var rampWidthDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(RampWidthText(initRamp)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildStepRow(
                new LocStrFormatted("Ramp width"),
                new LocStrFormatted("Width of generated access ramps (0 = disable ramp)."),
                rampWidthDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerRampWidth(tower, AutoDepthDesignation.GetTowerRampWidth(tower) + ModifierStepSize());
                    rampWidthDisplay.SetValue(new LocStrFormatted(RampWidthText(AutoDepthDesignation.GetTowerRampWidth(tower))));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerRampWidth(tower, AutoDepthDesignation.GetTowerRampWidth(tower) - ModifierStepSize());
                    rampWidthDisplay.SetValue(new LocStrFormatted(RampWidthText(AutoDepthDesignation.GetTowerRampWidth(tower))));
                }));

            // --- Max layers ---
            int initLayers = initialTower != null
                ? AutoDepthDesignation.GetTowerMaxLayersToExcavate(initialTower)
                : AutoForestryDesignationsMod.MaxLayersToExcavate;
            var maxLayersDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(MaxLayersText(initLayers)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildStepRow(
                new LocStrFormatted("Max layers to excavate"),
                new LocStrFormatted("Maximum layers to excavate from the surface. (\u221e = no limit.)"),
                maxLayersDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    int cur = AutoDepthDesignation.GetTowerMaxLayersToExcavate(tower);
                    int step = ModifierStepSize();
                    if (cur != 0)
                        AutoDepthDesignation.SetTowerMaxLayersToExcavate(tower, cur + step > 50 ? 0 : cur + step);
                    maxLayersDisplay.SetValue(new LocStrFormatted(MaxLayersText(AutoDepthDesignation.GetTowerMaxLayersToExcavate(tower))));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    int cur = AutoDepthDesignation.GetTowerMaxLayersToExcavate(tower);
                    AutoDepthDesignation.SetTowerMaxLayersToExcavate(tower, cur == 0 ? 50 : Math.Max(1, cur - ModifierStepSize()));
                    maxLayersDisplay.SetValue(new LocStrFormatted(MaxLayersText(AutoDepthDesignation.GetTowerMaxLayersToExcavate(tower))));
                }));

            // --- Elevation limit ---
            int? initElev = initialTower != null
                ? AutoDepthDesignation.GetTowerMaxDepthToDigTo(initialTower)
                : AutoForestryDesignationsMod.MaxDepthToDigTo;
            var minElevDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(MinElevText(initElev)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildStepRow(
                new LocStrFormatted("Elevation limit"),
                new LocStrFormatted("Maximum (absolute) excavation depth (-\u221e = no limit.)"),
                minElevDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    int? cur = AutoDepthDesignation.GetTowerMaxDepthToDigTo(tower);
                    AutoDepthDesignation.SetTowerMaxDepthToDigTo(tower, cur == null ? -50 : cur.Value + ModifierStepSize());
                    minElevDisplay.SetValue(new LocStrFormatted(MinElevText(AutoDepthDesignation.GetTowerMaxDepthToDigTo(tower))));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    int? cur = AutoDepthDesignation.GetTowerMaxDepthToDigTo(tower);
                    if (cur != null)
                    {
                        int next = cur.Value - ModifierStepSize();
                        AutoDepthDesignation.SetTowerMaxDepthToDigTo(tower, next < -50 ? (int?)null : next);
                    }
                    minElevDisplay.SetValue(new LocStrFormatted(MinElevText(AutoDepthDesignation.GetTowerMaxDepthToDigTo(tower))));
                }));

            // --- Ore purity ---
            int initPurity = initialTower != null
                ? AutoDepthDesignation.GetTowerOrePurityLevel(initialTower)
                : AutoForestryDesignationsMod.OrePurityLevel;
            var orePurityDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(OrePurityLevelText(initPurity)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildStepRow(
                new LocStrFormatted("Ore purity"),
                new LocStrFormatted(
                    "How strictly the scan filters for ore quality.\n" +
                    "Off: include all tiles, dig to full depth.\n" +
                    "Low: exclude very sparse tiles, trim thin trailing ore at the bottom.\n" +
                    "Med: moderate quality — skip tiles with heavy overburden or little ore.\n" +
                    "High: only rich tiles with a clean ore column.\n" +
                    "Max: near-pure ore only — strict on overburden, depth and ore density."),
                orePurityDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerOrePurityLevel(tower, AutoDepthDesignation.GetTowerOrePurityLevel(tower) + 1);
                    orePurityDisplay.SetValue(new LocStrFormatted(OrePurityLevelText(AutoDepthDesignation.GetTowerOrePurityLevel(tower))));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerOrePurityLevel(tower, AutoDepthDesignation.GetTowerOrePurityLevel(tower) - 1);
                    orePurityDisplay.SetValue(new LocStrFormatted(OrePurityLevelText(AutoDepthDesignation.GetTowerOrePurityLevel(tower))));
                }));

            // --- Corridor clearance ---
            int initClearance = initialTower != null
                ? AutoDepthDesignation.GetTowerCorridorClearance(initialTower)
                : AutoForestryDesignationsMod.MinCorridorClearance;
            var clearanceDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(ClearanceLevelText(initClearance)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildStepRow(
                new LocStrFormatted("Corridor clearance"),
                new LocStrFormatted(
                    "Minimum corridor width for connecting ore regions and enforcing passability.\n" +
                    "0 = disabled (regions left separate, no corridors or hole-filling).\n" +
                    "1 = 1-tile corridors (small and medium vehicles).\n" +
                    "2 = 2-tile corridors (mega vehicles).\n"),
                clearanceDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerCorridorClearance(tower, AutoDepthDesignation.GetTowerCorridorClearance(tower) + 1);
                    clearanceDisplay.SetValue(new LocStrFormatted(ClearanceLevelText(AutoDepthDesignation.GetTowerCorridorClearance(tower))));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoDepthDesignation.SetTowerCorridorClearance(tower, AutoDepthDesignation.GetTowerCorridorClearance(tower) - 1);
                    clearanceDisplay.SetValue(new LocStrFormatted(ClearanceLevelText(AutoDepthDesignation.GetTowerCorridorClearance(tower))));
                }));

            // --- Ore picker row ---
            if (orePicker != null)
            {
                var oreRow = new Row().MarginTop(1.pt());
                oreRow.Add(new Label(new LocStrFormatted("Target product:"))
                    .Tooltip(new LocStrFormatted("Force the scan to target a specific product (None = all useful products, excluding dirt/rock).")));
                oreRow.Add(new UiComponent().FlexGrow(1f));
                oreRow.Add(orePicker);
                panel.BodyAdd(oreRow);
            }

            s_bindings[key] = new Bindings(getTower, rampWidthDisplay, maxLayersDisplay, minElevDisplay, orePurityDisplay, clearanceDisplay);
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

        private static int ModifierStepSize()
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 10;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 5;
            return 1;
        }

        private static string RampWidthText(int value) => value.ToString();

        private static string MaxLayersText(int value) => value == 0 ? "\u221e" : value.ToString();

        private static string MinElevText(int? value)
        {
            if (value == null) return "-\u221e";
            return value.Value > 0 ? "+" + value.Value : value.Value.ToString();
        }

        private static string OrePurityLevelText(int value)
        {
            switch (value)
            {
                case 0: return "Off";
                case 1: return "Low";
                case 2: return "Med";
                case 3: return "High";
                case 4: return "Max";
                default: return value.ToString();
            }
        }

        private static string ClearanceLevelText(int value)
        {
            switch (value)
            {
                case 0: return "Off";
                case 1: return "1";
                case 2: return "2";
                default: return value.ToString();
            }
        }
    }
}
