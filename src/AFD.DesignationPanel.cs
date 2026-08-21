// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
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
using ClickEvent = UnityEngine.UIElements.ClickEvent;

namespace AutoForestryDesignations
{
    /// <summary>
    /// Builds the "Forestry designations" inspector panel independently of any specific
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
            public Mafi.Unity.Ui.Library.Display AvoidFlatDisplay { get; }
            public Mafi.Unity.Ui.Library.Display OnlyReachableDisplay { get; }
            public Mafi.Unity.Ui.Library.Display TargetYieldDisplay { get; }
            public Mafi.Unity.Ui.Library.Display MarkHarvestReadyDisplay { get; }
            public Mafi.Unity.Ui.Library.Display TruckPoolingDisplay { get; }
            public Action RefreshTargetYieldTooltip { get; }
            public PanelWithHeader Panel { get; }

            public Bindings(
                Func<IAreaManagingTower?> getTower,
                PanelWithHeader panel,
                Mafi.Unity.Ui.Library.Display onlyFertileDisplay,
                Mafi.Unity.Ui.Library.Display avoidWithTreesDisplay,
                Mafi.Unity.Ui.Library.Display avoidFlatDisplay,
                Mafi.Unity.Ui.Library.Display onlyReachableDisplay,
                Mafi.Unity.Ui.Library.Display targetYieldDisplay,
                Mafi.Unity.Ui.Library.Display markHarvestReadyDisplay,
                Mafi.Unity.Ui.Library.Display truckPoolingDisplay,
                Action refreshTargetYieldTooltip)
            {
                GetTower = getTower;
                Panel = panel;
                OnlyFertileDisplay = onlyFertileDisplay;
                AvoidWithTreesDisplay = avoidWithTreesDisplay;
                AvoidFlatDisplay = avoidFlatDisplay;
                OnlyReachableDisplay = onlyReachableDisplay;
                TargetYieldDisplay = targetYieldDisplay;
                MarkHarvestReadyDisplay = markHarvestReadyDisplay;
                TruckPoolingDisplay = truckPoolingDisplay;
                RefreshTargetYieldTooltip = refreshTargetYieldTooltip;
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
            b.OnlyFertileDisplay.SetValue(BoolText(AutoForestryDesignation.GetTowerOnlyFertileTiles(tower)));
            b.AvoidWithTreesDisplay.SetValue(BoolText(AutoForestryDesignation.GetTowerAvoidTilesWithTrees(tower)));
            b.AvoidFlatDisplay.SetValue(BoolText(AutoForestryDesignation.GetTowerAvoidFlatTiles(tower)));
            b.OnlyReachableDisplay.SetValue(BoolText(AutoForestryDesignation.GetTowerOnlyReachableTiles(tower)));
            b.TargetYieldDisplay.SetValue(new LocStrFormatted(TargetYieldText(AutoForestryDesignation.GetTowerTargetYield(tower))));
            b.RefreshTargetYieldTooltip();
            b.MarkHarvestReadyDisplay.SetValue(BoolText(AutoForestryDesignation.GetTowerMarkHarvestReadyForHarvest(tower)));
            b.TruckPoolingDisplay.SetValue(BoolText(AutoForestryDesignation.GetTowerTruckPoolingEnabled(tower)));
            b.Panel.Collapsed(AutoForestryDesignation.GetTowerForestryDesignationsPanelCollapsed(tower));
        }

        /// <summary>
        /// Builds the full "Forestry designations" panel and returns it. Insert the result
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
                AfdLocalization.CreateDesignationsBtn)
                .OnClick((Action)delegate
                {
                    try
                    {
                        var tower = getTower();
                        if (tower == null) return;
                        AutoForestryDesignation.CreateDesignationsForTower(tower);
                    }
                    catch (Exception ex) { AutoForestryDesignation.s_log.Warning($"Create button exception: {ex}"); }
                });
            createBtn.Tooltip(AfdLocalization.CreateDesignationsBtnTip);
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
                    catch (Exception ex) { AutoForestryDesignation.s_log.Warning($"Clear button exception: {ex}"); }
                })
                .Tooltip(AfdLocalization.ClearDesignationsBtnTip);

            createBtn.MarginTopBottom(1.pt());
            clearBtn.MarginTopBottom(1.pt()).AlignSelfEnd();

            var contentRow = new Row().Gap(3.pt()).AlignItemsEnd();
            contentRow.Add(new UiComponent().FlexGrow(1f));
            contentRow.Add(createBtn);
            contentRow.Add(new UiComponent().FlexGrow(1f));
            contentRow.Add(clearBtn);

            var panel = new PanelWithHeader()
                .Title(AfdLocalization.ForestryDesignationsTitle,
                       new LocStrFormatted(string.Format(
                           AfdLocalization.ForestryDesignationsTitleTipFmt.TranslatedString,
                           AutoForestryDesignationsMod.ModVersion)));
            var initialTower = getTower();
            panel.Collapsed(initialTower != null
                ? AutoForestryDesignation.GetTowerForestryDesignationsPanelCollapsed(initialTower)
                : AutoForestryDesignationsMod.ForestryDesignationsPanelCollapsed);
            panel.Header.OnClick((ClickEvent evt) =>
            {
                var tower = getTower();
                if (tower != null)
                    AutoForestryDesignation.SetTowerForestryDesignationsPanelCollapsed(tower, panel.IsCollapsed);
            });
            panel.BodyAdd(contentRow);

            // --- Fertile tiles only ---
            bool initOnlyFertile = initialTower != null
                ? AutoForestryDesignation.GetTowerOnlyFertileTiles(initialTower)
                : AutoForestryDesignationsMod.OnlyFertileTiles;
            var onlyFertileDisplay = new Mafi.Unity.Ui.Library.Display(BoolText(initOnlyFertile))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildToggleRow(
                AfdLocalization.OnlyFertileTilesLabel,
                AfdLocalization.OnlyFertileTilesTip,
                onlyFertileDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerOnlyFertileTiles(tower, true);
                    onlyFertileDisplay.SetValue(BoolText(true));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerOnlyFertileTiles(tower, false);
                    onlyFertileDisplay.SetValue(BoolText(false));
                }));

            // --- Avoid tiles with trees ---
            bool initAvoidTrees = initialTower != null
                ? AutoForestryDesignation.GetTowerAvoidTilesWithTrees(initialTower)
                : AutoForestryDesignationsMod.AvoidTilesWithTrees;
            var avoidWithTreesDisplay = new Mafi.Unity.Ui.Library.Display(BoolText(initAvoidTrees))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());

            // --- Reachable tiles only ---
            bool initOnlyReachable = initialTower != null
                ? AutoForestryDesignation.GetTowerOnlyReachableTiles(initialTower)
                : AutoForestryDesignationsMod.OnlyReachableTiles;
            var onlyReachableDisplay = new Mafi.Unity.Ui.Library.Display(BoolText(initOnlyReachable))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildToggleRow(
                AfdLocalization.OnlyReachableTilesLabel,
                AfdLocalization.OnlyReachableTilesTip,
                onlyReachableDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerOnlyReachableTiles(tower, true);
                    onlyReachableDisplay.SetValue(BoolText(true));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerOnlyReachableTiles(tower, false);
                    onlyReachableDisplay.SetValue(BoolText(false));
                }));

            // --- Avoid flat tiles ---
            bool initAvoidFlat = initialTower != null
                ? AutoForestryDesignation.GetTowerAvoidFlatTiles(initialTower)
                : AutoForestryDesignationsMod.AvoidFlatTiles;
            var avoidFlatDisplay = new Mafi.Unity.Ui.Library.Display(BoolText(initAvoidFlat))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildToggleRow(
                AfdLocalization.AvoidFlatTilesLabel,
                AfdLocalization.AvoidFlatTilesTip,
                avoidFlatDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerAvoidFlatTiles(tower, true);
                    avoidFlatDisplay.SetValue(BoolText(true));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerAvoidFlatTiles(tower, false);
                    avoidFlatDisplay.SetValue(BoolText(false));
                }));

            // --- Target yield ---
            int initTargetYield = initialTower != null
                ? AutoForestryDesignation.GetTowerTargetYield(initialTower)
                : AutoForestryDesignationsMod.TargetYield;
            var targetYieldDisplay = new Mafi.Unity.Ui.Library.Display(new LocStrFormatted(TargetYieldText(initTargetYield)))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            Action refreshTargetYieldTooltip = () => { };
            panel.BodyAdd(BuildStepRow(
                AfdLocalization.TargetYieldLabel,
                TargetYieldTooltip(initialTower),
                targetYieldDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    int cur = AutoForestryDesignation.GetTowerTargetYield(tower);
                    int next = cur >= int.MaxValue - ModifierStepSize()
                        ? int.MaxValue
                        : cur + ModifierStepSize();
                    AutoForestryDesignation.SetTowerTargetYield(tower, next);
                    targetYieldDisplay.SetValue(new LocStrFormatted(TargetYieldText(AutoForestryDesignation.GetTowerTargetYield(tower))));
                    refreshTargetYieldTooltip();
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    int cur = AutoForestryDesignation.GetTowerTargetYield(tower);
                    AutoForestryDesignation.SetTowerTargetYield(tower, Math.Max(0, cur - ModifierStepSize()));
                    targetYieldDisplay.SetValue(new LocStrFormatted(TargetYieldText(AutoForestryDesignation.GetTowerTargetYield(tower))));
                    refreshTargetYieldTooltip();
                },
                label => refreshTargetYieldTooltip = () => label.Tooltip(TargetYieldTooltip(getTower()))));

            // --- Mark harvest-ready trees for harvest ---
            bool initMarkHarvestReady = initialTower != null
                ? AutoForestryDesignation.GetTowerMarkHarvestReadyForHarvest(initialTower)
                : AutoForestryDesignationsMod.MarkHarvestReadyForHarvest;
            var markHarvestReadyDisplay = new Mafi.Unity.Ui.Library.Display(BoolText(initMarkHarvestReady))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());

            // --- Truck pooling ---
            bool initTruckPooling = initialTower != null
                ? AutoForestryDesignation.GetTowerTruckPoolingEnabled(initialTower)
                : AutoForestryDesignationsMod.TruckPoolingEnabled;
            var truckPoolingDisplay = new Mafi.Unity.Ui.Library.Display(BoolText(initTruckPooling))
                .MinDigits(3).AlignSelfStretch().MarginTopBottom(2.px());
            panel.BodyAdd(BuildToggleRow(
                AfdLocalization.TruckPoolingLabel,
                AfdLocalization.TruckPoolingTip,
                truckPoolingDisplay,
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerTruckPoolingEnabled(tower, true);
                    truckPoolingDisplay.SetValue(BoolText(true));
                },
                (Action)delegate
                {
                    var tower = getTower(); if (tower == null) return;
                    AutoForestryDesignation.SetTowerTruckPoolingEnabled(tower, false);
                    truckPoolingDisplay.SetValue(BoolText(false));
                }));

            s_bindings[key] = new Bindings(getTower, panel, onlyFertileDisplay, avoidWithTreesDisplay, avoidFlatDisplay, onlyReachableDisplay, targetYieldDisplay, markHarvestReadyDisplay, truckPoolingDisplay, refreshTargetYieldTooltip);
            return panel;
        }

        private static Row BuildStepRow(
            LocStrFormatted label,
            LocStrFormatted tooltip,
            Mafi.Unity.Ui.Library.Display display,
            Action onPlus,
            Action onMinus,
            Action<Label>? configureLabel = null)
        {
            var plusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Plus128.png")
                .Compact().IconSize(14.px()).OnClick(onPlus, allowKeyPresses: true);
            var minusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Minus128.png")
                .Compact().IconSize(14.px()).OnClick(onMinus, allowKeyPresses: true);
            var row = new Row().MarginTop(1.pt());
            var labelControl = new Label(label).Tooltip(tooltip);
            configureLabel?.Invoke(labelControl);
            row.Add(labelControl);
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

        private static LocStrFormatted BoolText(bool value) => value ? AfdLocalization.BoolYes : AfdLocalization.BoolNo;
        private static string TargetYieldText(int value) => value == 0 ? "\u221e" : value.ToString();

        private static LocStrFormatted TargetYieldTooltip(IAreaManagingTower? tower)
        {
            int legacyMaxTiles = tower != null
                ? AutoForestryDesignation.GetTowerLegacyMaxTiles(tower)
                : AutoForestryDesignationsMod.MaxTiles;
            if (legacyMaxTiles > 0)
            {
                return new LocStrFormatted(string.Format(
                    "{0}\n{1}",
                    AfdLocalization.TargetYieldTip.TranslatedString,
                    string.Format(AfdLocalization.TargetYieldLegacyTipFmt.TranslatedString, legacyMaxTiles)));
            }

            return AfdLocalization.TargetYieldTip;
        }
    }
}
