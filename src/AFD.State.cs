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
using System.Linq;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Entities;
using Mafi.Core.PathFinding;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Simulation;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Terrain.Trees;
using Mafi.Core.Vehicles.TreeHarvesters;
using Mafi.Core.Vehicles.TreePlanters;
using Mafi.Core.Vehicles.Trucks;
using Mafi.Core.World;
using Mafi.Unity.Terrain;
using Mafi.Unity.Trees;
using Mafi.Unity.Utils;
using UnityEngine;
using CoI.AutoHelpers.Logging;
using EntityId = Mafi.Core.EntityId;

namespace AutoForestryDesignations
{
    public static partial class AutoForestryDesignation
    {
        internal static readonly ModLogger s_log = new ModLogger("AFD");

        private static TerrainDesignationsManager? s_desigManager;
        private static TerrainDesignationProto? s_forestryProto;
        private static MonoBehaviour? s_coroutineHost;
        private static ProtosDb? s_protosDb;
        private static WorldMapManager? s_worldMapManager;
        private static IEntitiesManager? s_entitiesManager;
        private static ISimLoopEvents? s_simLoopEvents;
        internal static IVehiclePathFindingManager? s_vehiclePathFindingManager;
        internal static VehiclePathFindingParams? s_standardVehiclePathFindingParams;
        private static string? s_modRootDirectoryPath;

        private const int BATCH_SIZE = 30;
        private const int MAX_BATCH_SIZE = 200;
        private const int PAUSED_BATCH_MULTIPLIER = 4;
        private static int s_batchSize = BATCH_SIZE;

        /// <summary>Number of designations placed per coroutine frame. Clamped 1..200.</summary>
        public static int BatchSize => ClampBatchSize(s_batchSize);

        public static void SetBatchSize(int value) => s_batchSize = ClampBatchSize(value);

        private sealed class AFDTowerSettings
        {
            public bool OnlyFertileTiles { get; private set; }
            public bool AvoidTilesWithTrees { get; private set; }
            public bool AvoidMiningDesignations { get; private set; }
            public bool OnlyReachableTiles { get; private set; }
            public int MaxTiles { get; private set; }
            public bool MarkHarvestReadyForHarvest { get; private set; }
            public bool ForestryDesignationsPanelCollapsed { get; private set; }
            public bool ForestryInformationPanelCollapsed { get; private set; }
            public bool TruckPoolingEnabled { get; private set; }

            public AFDTowerSettings()
            {
                OnlyFertileTiles = AutoForestryDesignationsMod.OnlyFertileTiles;
                AvoidTilesWithTrees = AutoForestryDesignationsMod.AvoidTilesWithTrees;
                AvoidMiningDesignations = AutoForestryDesignationsMod.AvoidMiningDesignations;
                OnlyReachableTiles = AutoForestryDesignationsMod.OnlyReachableTiles;
                MaxTiles = AutoForestryDesignationsMod.MaxTiles;
                MarkHarvestReadyForHarvest = AutoForestryDesignationsMod.MarkHarvestReadyForHarvest;
                ForestryDesignationsPanelCollapsed = AutoForestryDesignationsMod.ForestryDesignationsPanelCollapsed;
                ForestryInformationPanelCollapsed = AutoForestryDesignationsMod.ForestryInformationPanelCollapsed;
                TruckPoolingEnabled = AutoForestryDesignationsMod.TruckPoolingEnabled;
            }

            public static AFDTowerSettings FromGlobalDefaults() => new AFDTowerSettings();

            public void SetOnlyFertileTiles(bool value) => OnlyFertileTiles = value;
            public void SetAvoidTilesWithTrees(bool value) => AvoidTilesWithTrees = value;
            public void SetAvoidMiningDesignations(bool value) => AvoidMiningDesignations = value;
            public void SetOnlyReachableTiles(bool value) => OnlyReachableTiles = value;
            public void SetMaxTiles(int value) => MaxTiles = Math.Max(0, value);
            public void SetMarkHarvestReadyForHarvest(bool value) => MarkHarvestReadyForHarvest = value;
            public void SetForestryDesignationsPanelCollapsed(bool value) => ForestryDesignationsPanelCollapsed = value;
            public void SetForestryInformationPanelCollapsed(bool value) => ForestryInformationPanelCollapsed = value;
            public void SetTruckPoolingEnabled(bool value) => TruckPoolingEnabled = value;

            public bool MatchesGlobalDefaults()
            {
                return OnlyFertileTiles == AutoForestryDesignationsMod.OnlyFertileTiles
                    && AvoidTilesWithTrees == AutoForestryDesignationsMod.AvoidTilesWithTrees
                    && AvoidMiningDesignations == AutoForestryDesignationsMod.AvoidMiningDesignations
                    && OnlyReachableTiles == AutoForestryDesignationsMod.OnlyReachableTiles
                    && MaxTiles == AutoForestryDesignationsMod.MaxTiles
                    && MarkHarvestReadyForHarvest == AutoForestryDesignationsMod.MarkHarvestReadyForHarvest
                    && ForestryDesignationsPanelCollapsed == AutoForestryDesignationsMod.ForestryDesignationsPanelCollapsed
                    && ForestryInformationPanelCollapsed == AutoForestryDesignationsMod.ForestryInformationPanelCollapsed
                    && TruckPoolingEnabled == AutoForestryDesignationsMod.TruckPoolingEnabled;
            }
        }

        private static readonly Dictionary<EntityId, AFDTowerSettings> s_towerSettingsByEntityId =
            new Dictionary<EntityId, AFDTowerSettings>();
        [System.Diagnostics.Conditional("DEBUG")]
        internal static void LogDebug(string message)
        {
            s_log.Info(message);
        }

        private static bool TryGetTowerEntityId(IAreaManagingTower tower, out EntityId entityId)
        {
            entityId = EntityId.Invalid;
            if (tower is IEntity entity && entity.Id.IsValid)
            {
                entityId = entity.Id;
                return true;
            }

            return false;
        }

        private static AFDTowerSettings GetOrCreateTowerSettings(IAreaManagingTower tower)
        {
            if (TryGetTowerEntityId(tower, out EntityId entityId))
            {
                if (!s_towerSettingsByEntityId.TryGetValue(entityId, out AFDTowerSettings settings))
                {
                    settings = AFDTowerSettings.FromGlobalDefaults();
                    s_towerSettingsByEntityId[entityId] = settings;
                }

                return settings;
            }

            return AFDTowerSettings.FromGlobalDefaults();
        }

        private static AFDTowerSettings GetTowerSettingsOrDefaults(IAreaManagingTower tower)
        {
            if (TryGetTowerEntityId(tower, out EntityId entityId)
                && s_towerSettingsByEntityId.TryGetValue(entityId, out AFDTowerSettings settings))
            {
                return settings;
            }

            return AFDTowerSettings.FromGlobalDefaults();
        }

        private static void UpdateTowerSettings(IAreaManagingTower tower, Action<AFDTowerSettings> update)
        {
            if (!TryGetTowerEntityId(tower, out EntityId entityId))
            {
                return;
            }

            if (!s_towerSettingsByEntityId.TryGetValue(entityId, out AFDTowerSettings settings))
            {
                settings = AFDTowerSettings.FromGlobalDefaults();
                s_towerSettingsByEntityId[entityId] = settings;
            }

            update(settings);
            if (settings.MatchesGlobalDefaults())
            {
                s_towerSettingsByEntityId.Remove(entityId);
            }
        }

        // --- Forestry per-tower settings accessors ---
        internal static bool GetTowerOnlyFertileTiles(IAreaManagingTower tower) => GetTowerSettingsOrDefaults(tower).OnlyFertileTiles;
        internal static void SetTowerOnlyFertileTiles(IAreaManagingTower tower, bool value) => UpdateTowerSettings(tower, settings => settings.SetOnlyFertileTiles(value));

        internal static bool GetTowerAvoidTilesWithTrees(IAreaManagingTower tower) => GetTowerSettingsOrDefaults(tower).AvoidTilesWithTrees;
        internal static void SetTowerAvoidTilesWithTrees(IAreaManagingTower tower, bool value) => UpdateTowerSettings(tower, settings => settings.SetAvoidTilesWithTrees(value));

        internal static bool GetTowerAvoidMiningDesignations(IAreaManagingTower tower) => GetTowerSettingsOrDefaults(tower).AvoidMiningDesignations;
        internal static void SetTowerAvoidMiningDesignations(IAreaManagingTower tower, bool value) => UpdateTowerSettings(tower, settings => settings.SetAvoidMiningDesignations(value));

        internal static bool GetTowerOnlyReachableTiles(IAreaManagingTower tower) => GetTowerSettingsOrDefaults(tower).OnlyReachableTiles;
        internal static void SetTowerOnlyReachableTiles(IAreaManagingTower tower, bool value) => UpdateTowerSettings(tower, settings => settings.SetOnlyReachableTiles(value));

        internal static int GetTowerMaxTiles(IAreaManagingTower tower) => GetTowerSettingsOrDefaults(tower).MaxTiles;
        internal static void SetTowerMaxTiles(IAreaManagingTower tower, int value) => UpdateTowerSettings(tower, settings => settings.SetMaxTiles(value));

        internal static bool GetTowerMarkHarvestReadyForHarvest(IAreaManagingTower tower) => GetTowerSettingsOrDefaults(tower).MarkHarvestReadyForHarvest;
        internal static void SetTowerMarkHarvestReadyForHarvest(IAreaManagingTower tower, bool value) => UpdateTowerSettings(tower, settings => settings.SetMarkHarvestReadyForHarvest(value));

        internal static bool GetTowerForestryDesignationsPanelCollapsed(IAreaManagingTower tower) => GetTowerSettingsOrDefaults(tower).ForestryDesignationsPanelCollapsed;
        internal static void SetTowerForestryDesignationsPanelCollapsed(IAreaManagingTower tower, bool value) => UpdateTowerSettings(tower, settings => settings.SetForestryDesignationsPanelCollapsed(value));

        internal static bool GetTowerForestryInformationPanelCollapsed(IAreaManagingTower tower) => GetTowerSettingsOrDefaults(tower).ForestryInformationPanelCollapsed;
        internal static void SetTowerForestryInformationPanelCollapsed(IAreaManagingTower tower, bool value) => UpdateTowerSettings(tower, settings => settings.SetForestryInformationPanelCollapsed(value));

        internal static bool GetTowerTruckPoolingEnabled(IAreaManagingTower tower) => (tower != null && !tower.IsDestroyed) ? GetTowerSettingsOrDefaults(tower).TruckPoolingEnabled : false;
        internal static void SetTowerTruckPoolingEnabled(IAreaManagingTower tower, bool value)
        {
            UpdateTowerSettings(tower, settings => settings.SetTruckPoolingEnabled(value));
            if (tower is ForestryTower ft)
            {
                if (value)
                {
                    var trucksToPool = new List<Truck>();
                    for (int i = 0; i < ft.AllVehicles.Count; i++)
                    {
                        if (ft.AllVehicles[i] is TreeHarvester harvester)
                        {
                            for (int j = 0; j < harvester.AllVehicles.Count; j++)
                            {
                                if (harvester.AllVehicles[j] is Truck truck)
                                {
                                    trucksToPool.Add(truck);
                                }
                            }
                        }
                    }
                    TowerTruckAssignments.AssignTrucksToTower(ft, trucksToPool, s_entitiesManager);
                }
                else
                {
                    TowerTruckAssignments.SetTruckIdsForTower(ft.Id, Array.Empty<EntityId>());
                    TowerTruckAssignments.RefreshTowerVehicleState(ft);
                }
            }
        }

        public static void Initialize(
            ITerrainDesignationsManager desigManager,
            ProtosDb protosDb,
            IWorldMapManager worldMapManager,
            MonoBehaviour coroutineHost,
            IEntitiesManager entitiesManager,
            ISimLoopEvents? simLoopEvents = null,
            IVehiclePathFindingManager? vehiclePathFindingManager = null)
        {
            // Load defaults after logging is initialized so diagnostics are visible.
            LoadSettingsFromJson();

            s_desigManager = desigManager as TerrainDesignationsManager;
            s_coroutineHost = coroutineHost;
            s_protosDb = protosDb;
            s_worldMapManager = worldMapManager as WorldMapManager;
            s_entitiesManager = entitiesManager;
            s_simLoopEvents = simLoopEvents;
            s_vehiclePathFindingManager = vehiclePathFindingManager;
            s_standardVehiclePathFindingParams = FindStandardVehiclePathFindingParams(protosDb);

            if (protosDb.TryGetProto(new Proto.ID("ForestryDesignator"), out TerrainDesignationProto proto))
                s_forestryProto = proto;
            else
                UnityEngine.Debug.Log("[AFD] ForestryDesignator proto not found");

            DesignationPanel.Initialize(s_protosDb);
        }

        public static void SetModRootDirectoryPath(string? modRootDirectoryPath)
        {
            s_modRootDirectoryPath = modRootDirectoryPath;
        }

        /// <summary>Returns true once Initialize has completed successfully.</summary>
        internal static bool IsInitialized => s_desigManager != null && s_coroutineHost != null;

        private static TreesRenderer? s_treesRenderer;
        internal static void SetTreesRenderer(TreesRenderer? renderer) => s_treesRenderer = renderer;
        internal static TreesRenderer? GetTreesRenderer() => s_treesRenderer;

        private static AudioSource? s_clickSound;
        internal static void SetClickSound(AudioSource? clickSound) => s_clickSound = clickSound;
        internal static void PlayClickSound()
        {
            if (s_clickSound != null)
            {
                try { s_clickSound.Play(); } catch { }
            }
        }

        internal static TreesManager? GetTreesManager() => s_desigManager?.TreesManager;

        private static IActivator? s_harvestOverlayActivator;
        internal static void SetHarvestHighlightManager(TreeHarvestingHighlightManager? manager)
            => s_harvestOverlayActivator = manager?.CreateActivator();
        internal static void ActivateHarvestOverlayIfNeeded()
            => s_harvestOverlayActivator?.ActivateIfNotActive();
        internal static void DeactivateHarvestOverlay()
            => s_harvestOverlayActivator?.DeactivateIfActive();

        internal static UnityEngine.Coroutine? StartCoroutine(System.Collections.IEnumerator routine)
            => s_coroutineHost?.StartCoroutine(routine);
        internal static void StopCoroutine(UnityEngine.Coroutine? coroutine)
        {
            if (coroutine != null) s_coroutineHost?.StopCoroutine(coroutine);
        }

        internal static SimStep? GetCurrentSimStep() => s_simLoopEvents?.CurrentStep;

        private static VehiclePathFindingParams FindStandardVehiclePathFindingParams(ProtosDb protosDb)
        {
            foreach (TreePlanterProto proto in protosDb.All<TreePlanterProto>())
                return proto.PathFindingParams;
            foreach (TreeHarvesterProto proto in protosDb.All<TreeHarvesterProto>())
                return proto.PathFindingParams;
            return VehiclePathFindingParams.DEFAULT;
        }

    }
}
