// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
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
        /// Builds the "Forestry designations" panel and returns it.
        /// </summary>
        public static PanelWithHeader BuildDesignationPanel(Func<IAreaManagingTower?> getTower, object key)
            => DesignationPanel.Build(getTower, key);

        /// <summary>
        /// Refreshes the display values of a previously built Forestry designations panel.
        /// </summary>
        public static void RefreshDesignationPanel(object key)
            => DesignationPanel.RefreshDisplays(key);

        /// <summary>
        /// Builds the "Forestry information" panel and returns it.
        /// </summary>
        public static PanelWithHeader BuildForestryInfoPanel(Func<IAreaManagingTower?> getTower, object key)
            => ForestryInfoPanel.Build(getTower, key);

        /// <summary>
        /// Refreshes the display values of a previously built Forestry information panel.
        /// </summary>
        public static void RefreshForestryInfoPanel(object key)
            => ForestryInfoPanel.RefreshContent(key);
    }
}
