// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
using System;
using Mafi;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Products;
using Mafi.Unity.UiToolkit.Library;

namespace AutoForestryDesignations
{
    /// <summary>
    /// Public API for AutoForestryDesignations.
    /// External mods can use this class to trigger designation creation and clearing on any
    /// IAreaManagingTower implementation (vanilla MineTower or custom tower buildings).
    ///
    /// Requirements:
    ///  - AutoForestryDesignations must be loaded and initialized before calling these methods.
    /// </summary>
    public static class AutoForestryDesignationsApi
    {
        /// <summary>
        /// Returns true once AutoForestryDesignations has finished initializing.
        /// Check this before calling any other API method from early-init code.
        /// </summary>
        public static bool IsInitialized => AutoDepthDesignation.IsInitialized;
        /// <summary>
        /// Scans the given tower's area for ore resources and creates mining designations.
        /// Respects the per-tower ore selection and global ramp/slope settings.
        /// </summary>
        /// <param name="tower">Any IAreaManagingTower whose area should be designated.</param>
        public static void CreateDesignationsForTower(IAreaManagingTower tower)
        {
            if (tower == null)
            {
                Log.Warning("[ATD API] CreateDesignationsForTower called with null tower.");
                return;
            }
            AutoDepthDesignation.CreateDesignationsForTower(tower);
        }

        /// <summary>
        /// Clears all mining designations within the given tower's area.
        /// </summary>
        /// <param name="tower">Any IAreaManagingTower whose designations should be cleared.</param>
        public static void ClearDesignationsForTower(IAreaManagingTower tower)
        {
            if (tower == null)
            {
                Log.Warning("[ATD API] ClearDesignationsForTower called with null tower.");
                return;
            }
            AutoDepthDesignation.ClearDesignationsForTower(tower);
        }

        /// <summary>
        /// Sets the ore filter for the given tower.
        /// Pass null to use "Auto" mode, which scans for all non-rock ores.
        /// </summary>
        /// <param name="tower">The tower to set the ore filter for.</param>
        /// <param name="ore">The ore to filter for, or null for Auto mode.</param>
        public static void SetOreFilter(IAreaManagingTower tower, LooseProductProto? ore)
        {
            if (tower == null)
            {
                Log.Warning("[ATD API] SetOreFilter called with null tower.");
                return;
            }
            AutoDepthDesignation.SetSelectedOre(tower, ore);
        }

        /// <summary>
        /// Gets the currently selected ore filter for the given tower.
        /// Returns null if the tower is in "Auto" mode (all non-rock ores).
        /// </summary>
        /// <param name="tower">The tower to get the ore filter for.</param>
        public static LooseProductProto? GetOreFilter(IAreaManagingTower tower)
        {
            if (tower == null) return null;
            return AutoDepthDesignation.GetSelectedOre(tower) as LooseProductProto;
        }

        // -----------------------------------------------------------------------
        // Per-tower settings
        // All setters clamp to the same valid range used by the in-game inspector.
        // Changing a setting does not automatically re-run designations; call
        // CreateDesignationsForTower to apply the new values.
        // -----------------------------------------------------------------------

        /// <summary>Gets the maximum height difference (1–3) this tower will designate across.</summary>
        public static int GetMaxHeightDiff(IAreaManagingTower tower) =>
            tower != null ? AutoDepthDesignation.GetTowerMaxHeightDiff(tower) : AutoForestryDesignationsMod.MaxHeightDiff;

        /// <summary>Sets the maximum height difference (1–3) this tower will designate across.</summary>
        public static void SetMaxHeightDiff(IAreaManagingTower tower, int value)
        {
            if (tower == null) { Log.Warning("[ATD API] SetMaxHeightDiff called with null tower."); return; }
            AutoDepthDesignation.SetTowerMaxHeightDiff(tower, value);
        }

        /// <summary>Gets the ramp width (0–5) used when generating an access ramp. 0 disables ramps.</summary>
        public static int GetRampWidth(IAreaManagingTower tower) =>
            tower != null ? AutoDepthDesignation.GetTowerRampWidth(tower) : AutoForestryDesignationsMod.RampWidth;

        /// <summary>Sets the ramp width (0–5). Pass 0 to disable ramp generation for this tower.</summary>
        public static void SetRampWidth(IAreaManagingTower tower, int value)
        {
            if (tower == null) { Log.Warning("[ATD API] SetRampWidth called with null tower."); return; }
            AutoDepthDesignation.SetTowerRampWidth(tower, value);
        }

        /// <summary>Gets the maximum number of terrain layers this tower will excavate from the surface. 0 = no limit.</summary>
        public static int GetMaxLayersToExcavate(IAreaManagingTower tower) =>
            tower != null ? AutoDepthDesignation.GetTowerMaxLayersToExcavate(tower) : AutoForestryDesignationsMod.MaxLayersToExcavate;

        /// <summary>Sets the maximum number of terrain layers to excavate from the surface. 0 = no limit.</summary>
        public static void SetMaxLayersToExcavate(IAreaManagingTower tower, int value)
        {
            if (tower == null) { Log.Warning("[ATD API] SetMaxLayersToExcavate called with null tower."); return; }
            AutoDepthDesignation.SetTowerMaxLayersToExcavate(tower, value);
        }

        /// <summary>Gets the absolute minimum terrain elevation this tower will create designations to. null = no limit.</summary>
        public static int? GetMaxDepthToDigTo(IAreaManagingTower tower) =>
            tower != null ? AutoDepthDesignation.GetTowerMaxDepthToDigTo(tower) : AutoForestryDesignationsMod.MaxDepthToDigTo;

        /// <summary>Sets the absolute minimum terrain elevation this tower will create designations to. Pass null to remove the limit.</summary>
        public static void SetMaxDepthToDigTo(IAreaManagingTower tower, int? value)
        {
            if (tower == null) { Log.Warning("[ATD API] SetMaxDepthToDigTo called with null tower."); return; }
            AutoDepthDesignation.SetTowerMaxDepthToDigTo(tower, value);
        }

        /// <summary>Gets the ore purity threshold level (0=Off … 4=Max) for this tower.</summary>
        public static int GetOrePurityLevel(IAreaManagingTower tower) =>
            tower != null ? AutoDepthDesignation.GetTowerOrePurityLevel(tower) : AutoForestryDesignationsMod.OrePurityLevel;

        /// <summary>Sets the ore purity threshold level (0=Off … 4=Max) for this tower.</summary>
        public static void SetOrePurityLevel(IAreaManagingTower tower, int value)
        {
            if (tower == null) { Log.Warning("[ATD API] SetOrePurityLevel called with null tower."); return; }
            AutoDepthDesignation.SetTowerOrePurityLevel(tower, value);
        }

        /// <summary>Gets the corridor clearance (0–2) for designation connectivity for this tower.</summary>
        public static int GetCorridorClearance(IAreaManagingTower tower) =>
            tower != null ? AutoDepthDesignation.GetTowerCorridorClearance(tower) : AutoForestryDesignationsMod.MinCorridorClearance;

        /// <summary>Sets the corridor clearance (0–2). 0 = no corridors (vehicle-less excavation).</summary>
        public static void SetCorridorClearance(IAreaManagingTower tower, int value)
        {
            if (tower == null) { Log.Warning("[ATD API] SetCorridorClearance called with null tower."); return; }
            AutoDepthDesignation.SetTowerCorridorClearance(tower, value);
        }

        // -----------------------------------------------------------------------
        // Panel builders
        // External mods can call these to embed AFD panels in their own inspectors.
        // Use the same key for both panels and for CreateDesignationsForTower so that
        // the Ore Composition panel auto-refreshes when a scan completes.
        // -----------------------------------------------------------------------

        /// <summary>
        /// Builds the "Terrain Designations" panel and returns it. Insert the result at any
        /// position in your inspector's Column layout.
        /// </summary>
        /// <param name="getTower">
        /// Delegate returning the currently active tower. Called lazily inside button handlers.
        /// May return null between inspector activations.
        /// </param>
        /// <param name="key">
        /// Opaque key (typically your inspector instance) used to route
        /// <see cref="RefreshDesignationPanel"/> calls back to this panel. Pass the same key to
        /// ensure display values stay in sync when the inspector switches towers.
        /// </param>
        public static PanelWithHeader BuildDesignationPanel(Func<IAreaManagingTower?> getTower, object key)
            => DesignationPanel.Build(getTower, key);

        /// <summary>
        /// Refreshes the display values of a previously built Terrain Designations panel.
        /// Call this when your inspector activates / switches to a different tower.
        /// </summary>
        public static void RefreshDesignationPanel(object key)
            => DesignationPanel.RefreshDisplays(key);

        /// <summary>
        /// Builds the "Ore Composition" panel and returns it. Insert the result at any position
        /// in your inspector's Column layout. The panel auto-refreshes after a scan when you pass
        /// the same key to <see cref="BuildDesignationPanel"/> (the dig button triggers the refresh).
        /// </summary>
        /// <param name="getTower">Delegate returning the currently active tower.</param>
        /// <param name="key">Opaque key matching the one passed to <see cref="BuildDesignationPanel"/>.</param>
        public static PanelWithHeader BuildOreCompositionPanel(Func<IAreaManagingTower?> getTower, object key)
            => OreCompositionPanel.Build(getTower, key);
    }
}
