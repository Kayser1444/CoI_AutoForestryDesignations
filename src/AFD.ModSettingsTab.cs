// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System;
using System.Collections.Generic;
using CoI.AutoHelpers.Settings;
using Mafi;
using Mafi.Localization;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using UnityEngine;
using Display = Mafi.Unity.Ui.Library.Display;
using Row = Mafi.Unity.UiToolkit.Library.Row;

namespace AutoForestryDesignations
{
    internal static class AfdModSettingsTab
    {
        private const string MOD_ID = "auto-forestry-designations";
        private const string MOD_ICON = "Assets/Unity/UserInterface/Toolbar/Forestry.svg";
        private const string DEFAULTS_ICON = "Assets/Unity/UserInterface/Toolbar/Copy.svg";
        private const string GAME_SETTINGS_ICON = "Assets/Unity/UserInterface/EntityIcons/Gears.png";

        internal static ModSettingsTab BuildDefaultsTab()
        {
            return new ModSettingsTab(
                MOD_ID,
                AfdLocalization.SettingsModName.AsFormatted,
                AfdLocalization.SettingsTabDefaults.AsFormatted,
                100,
                BuildDefaultsContent,
                DEFAULTS_ICON,
                MOD_ICON);
        }

        internal static ModSettingsTab BuildGameSettingsTab()
        {
            return new ModSettingsTab(
                MOD_ID,
                AfdLocalization.SettingsModName.AsFormatted,
                AfdLocalization.SettingsTabGameSettings.AsFormatted,
                110,
                BuildGameSettingsContent,
                GAME_SETTINGS_ICON);
        }

        private static UiComponent BuildDefaultsContent()
        {
            var refreshers = new List<Action>();
            var content = BuildSettingsColumn();

            AddDesignationDefaultsSection(content, refreshers);
            AddPanelDefaultsSection(content, refreshers);

            content.Add(BuildFooter(refreshers));

            return content;
        }

        private static UiComponent BuildGameSettingsContent()
        {
            var refreshers = new List<Action>();
            var content = BuildSettingsColumn();

            AddHarvestSection(content, refreshers);

            content.Add(BuildFooter(refreshers));

            return content;
        }

        private static Column BuildSettingsColumn()
        {
            return new Column(2.pt())
                .AlignItemsStretch()
                .PaddingLeft(1.pt())
                .PaddingRight(1.pt());
        }

        private static Title BuildSectionHeading(LocStrFormatted title)
        {
            return new Title(title)
                .Color(Theme.PrimaryColor)
                .MarginTop(2.pt())
                .MarginLeft(-1.pt());
        }

        private static void AddDesignationDefaultsSection(Column content, List<Action> refreshers)
        {
            content.Add(BuildSectionHeading(AfdLocalization.SettingsHeadingForestryTowerDefaults.AsFormatted));

            content.Add(BuildToggleRow(
                AfdLocalization.OnlyFertileTilesLabel.AsFormatted,
                AfdLocalization.OnlyFertileTilesTip.AsFormatted,
                () => AutoForestryDesignationsMod.OnlyFertileTiles,
                value => AutoForestryDesignationsMod.SetOnlyFertileTiles(value),
                refreshers));

            content.Add(BuildToggleRow(
                AfdLocalization.AvoidTerrainDesignationsLabel.AsFormatted,
                AfdLocalization.AvoidTerrainDesignationsTip.AsFormatted,
                () => AutoForestryDesignationsMod.AvoidMiningDesignations,
                value => AutoForestryDesignationsMod.SetAvoidMiningDesignations(value),
                refreshers));

            content.Add(BuildToggleRow(
                AfdLocalization.OnlyReachableTilesLabel.AsFormatted,
                AfdLocalization.OnlyReachableTilesTip.AsFormatted,
                () => AutoForestryDesignationsMod.OnlyReachableTiles,
                value => AutoForestryDesignationsMod.SetOnlyReachableTiles(value),
                refreshers));

            content.Add(BuildToggleRow(
                AfdLocalization.TruckPoolingLabel.AsFormatted,
                AfdLocalization.TruckPoolingTip.AsFormatted,
                () => AutoForestryDesignationsMod.TruckPoolingEnabled,
                value => AutoForestryDesignationsMod.SetTruckPoolingEnabled(value),
                refreshers));

            content.Add(BuildIntStepRow(
                AfdLocalization.MaxTilesLabel.AsFormatted,
                AfdLocalization.MaxTilesTip.AsFormatted,
                () => AutoForestryDesignationsMod.MaxTiles,
                value => AutoForestryDesignationsMod.SetMaxTiles(value),
                FormatNoLimitZero,
                refreshers));
        }

        private static void AddHarvestSection(Column content, List<Action> refreshers)
        {
            content.Add(BuildSectionHeading(AfdLocalization.SettingsHeadingDesignationDefaults.AsFormatted));

            content.Add(BuildToggleRow(
                AfdLocalization.SettingsAvoidTilesWithTreesLabel.AsFormatted,
                AfdLocalization.SettingsAvoidTilesWithTreesTip.AsFormatted,
                () => AutoForestryDesignationsMod.AvoidTilesWithTrees,
                value => AutoForestryDesignationsMod.SetAvoidTilesWithTrees(value),
                refreshers));

            content.Add(BuildToggleRow(
                AfdLocalization.SettingsMarkHarvestReadyLabel.AsFormatted,
                AfdLocalization.SettingsMarkHarvestReadyTip.AsFormatted,
                () => AutoForestryDesignationsMod.MarkHarvestReadyForHarvest,
                value => AutoForestryDesignationsMod.SetMarkHarvestReadyForHarvest(value),
                refreshers));
        }


        private static void AddPanelDefaultsSection(Column content, List<Action> refreshers)
        {
            content.Add(BuildSectionHeading(AfdLocalization.SettingsHeadingPanelDefaults.AsFormatted));

            content.Add(BuildToggleRow(
                AfdLocalization.SettingsForestryPanelCollapsedLabel.AsFormatted,
                AfdLocalization.SettingsForestryPanelCollapsedTip.AsFormatted,
                () => AutoForestryDesignationsMod.ForestryDesignationsPanelCollapsed,
                value => AutoForestryDesignationsMod.SetForestryDesignationsPanelCollapsed(value),
                refreshers));

            content.Add(BuildToggleRow(
                AfdLocalization.SettingsInfoPanelCollapsedLabel.AsFormatted,
                AfdLocalization.SettingsInfoPanelCollapsedTip.AsFormatted,
                () => AutoForestryDesignationsMod.ForestryInformationPanelCollapsed,
                value => AutoForestryDesignationsMod.SetForestryInformationPanelCollapsed(value),
                refreshers));
        }

        private static Row BuildIntStepRow(
            LocStrFormatted label,
            LocStrFormatted tooltip,
            Func<int> getValue,
            Action<int> setValue,
            Func<int, string> format,
            List<Action> refreshers)
        {
            var display = new Display(L(format(getValue()))).MinDigits(4).AlignSelfStretch().MarginTopBottom(2.px());
            void Refresh() => display.SetValue(L(format(getValue())));
            refreshers.Add(Refresh);

            return BuildStepRow(
                label,
                tooltip,
                display,
                () => { setValue(getValue() + ModifierStepSize()); Refresh(); },
                () => { setValue(Math.Max(0, getValue() - ModifierStepSize())); Refresh(); });
        }

        private static Row BuildToggleRow(
            LocStrFormatted label,
            LocStrFormatted tooltip,
            Func<bool> getValue,
            Action<bool> setValue,
            List<Action> refreshers)
        {
            var toggle = new Toggle(standalone: true)
                .Label(label)
                .Value(getValue())
                .OnValueChanged(value => setValue(value))
                .Tooltip(tooltip);
            refreshers.Add(() => toggle.Value(getValue()));
            var row = new Row().MarginTop(1.pt()).AlignItemsCenter();
            row.Add(toggle);
            return row;
        }

        private static Row BuildStepRow(
            LocStrFormatted label,
            LocStrFormatted tooltip,
            Display display,
            Action onPlus,
            Action onMinus)
        {
            var plusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Plus128.png")
                .Compact().IconSize(14.px()).OnClick(onPlus, allowKeyPresses: true);
            var minusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Minus128.png")
                .Compact().IconSize(14.px()).OnClick(onMinus, allowKeyPresses: true);
            var row = new Row().MarginTop(1.pt()).AlignItemsCenter();
            row.Add(new Label(label).Tooltip(tooltip));
            row.Add(new UiComponent().FlexGrow(1f));
            row.Add(minusBtn);
            row.Add(display);
            row.Add(plusBtn);
            return row;
        }

        private static PanelFooterRow BuildFooter(List<Action> refreshers)
        {
            var status = new Label(L(string.Empty)).MarginTopBottom(1.pt());

            var save = new ButtonText(Button.Primary, AfdLocalization.SettingsSaveAsGlobal.AsFormatted, () =>
            {
                if (AutoForestryDesignation.TrySaveSettings(out string _))
                    status.Value(AfdLocalization.SettingsSavedToFile.AsFormatted);
                else
                    status.Value(AfdLocalization.SettingsSaveFailed.AsFormatted);
            }).Tooltip(AfdLocalization.SettingsSaveAsGlobalTooltip.AsFormatted);

            var reset = new ButtonText(Button.General, AfdLocalization.SettingsRestoreDefaults.AsFormatted, () =>
            {
                AutoForestryDesignationsMod.ResetGlobalDefaults();
                foreach (Action refresh in refreshers)
                    refresh();
                status.Value(AfdLocalization.SettingsRestoredDefaults.AsFormatted);
            }).Tooltip(AfdLocalization.SettingsRestoreDefaultsTooltip.AsFormatted);

            return new PanelFooterRow().BodyAdd(
                row => row.Gap(2.pt()).AlignItemsCenter(),
                status,
                new UiComponent().FlexGrow(1f),
                reset,
                save);
        }

        private static int ModifierStepSize()
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 10;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 5;
            return 1;
        }

        private static string FormatNoLimitZero(int value)
            => value <= 0 ? "\u221e" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static LocStrFormatted L(string text)
            => new LocStrFormatted(text ?? string.Empty);
    }
}
