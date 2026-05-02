// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
using System;
using Mafi;
using Mafi.Core.Buildings.Towers;
using Mafi.Unity.UiToolkit.Library;

namespace AutoForestryDesignations
{
    /// <summary>
    /// Public API for AutoForestryDesignations.
    /// External mods can use this class to trigger designation creation and clearing on any
    /// IAreaManagingTower implementation.
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
        public static bool IsInitialized => AutoForestryDesignation.IsInitialized;

        /// <summary>
        /// Scans the given tower's area and creates forestry designations.
        /// </summary>
        public static void CreateDesignationsForTower(IAreaManagingTower tower)
        {
            if (tower == null)
            {
                Log.Warning("[AFD API] CreateDesignationsForTower called with null tower.");
                return;
            }
            AutoForestryDesignation.CreateDesignationsForTower(tower);
        }

        /// <summary>
        /// Clears all forestry designations within the given tower's area.
        /// </summary>
        public static void ClearDesignationsForTower(IAreaManagingTower tower)
        {
            if (tower == null)
            {
                Log.Warning("[AFD API] ClearDesignationsForTower called with null tower.");
                return;
            }
            AutoForestryDesignation.ClearDesignationsForTower(tower);
        }

        // -----------------------------------------------------------------------
        // Panel builders
        // External mods can call these to embed AFD panels in their own inspectors.
        // -----------------------------------------------------------------------

        /// <summary>
        /// Builds the "Forestry Designations" panel and returns it.
        /// </summary>
        public static PanelWithHeader BuildDesignationPanel(Func<IAreaManagingTower?> getTower, object key)
            => DesignationPanel.Build(getTower, key);

        /// <summary>
        /// Refreshes the display values of a previously built Forestry Designations panel.
        /// </summary>
        public static void RefreshDesignationPanel(object key)
            => DesignationPanel.RefreshDisplays(key);
    }
}
