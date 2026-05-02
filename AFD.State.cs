// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Entities;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Simulation;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Terrain.Trees;
using Mafi.Core.World;
using UnityEngine;

namespace AutoForestryDesignations
{
    public static partial class AutoForestryDesignation
    {
        private static TerrainDesignationsManager? s_desigManager;
        private static TerrainDesignationProto? s_forestryProto;
        private static MonoBehaviour? s_coroutineHost;
        private static ProtosDb? s_protosDb;
        private static WorldMapManager? s_worldMapManager;
        private static IEntitiesManager? s_entitiesManager;
        private static ISimLoopEvents? s_simLoopEvents;
        private static string? s_modRootDirectoryPath;

        private const int BATCH_SIZE = 30;
        private const int MAX_BATCH_SIZE = 200;
        private const int PAUSED_BATCH_MULTIPLIER = 4;
        private static int s_batchSize = BATCH_SIZE;

        private sealed class AFDTowerSettings
        {
            public bool OnlyFertileTiles { get; private set; }
            public bool AvoidTilesWithTrees { get; private set; }
            public bool AvoidMiningDesignations { get; private set; }
            public int MaxTiles { get; private set; }
            public bool MarkHarvestReadyForHarvest { get; private set; }

            public AFDTowerSettings()
            {
                OnlyFertileTiles = AutoForestryDesignationsMod.OnlyFertileTiles;
                AvoidTilesWithTrees = AutoForestryDesignationsMod.AvoidTilesWithTrees;
                AvoidMiningDesignations = AutoForestryDesignationsMod.AvoidMiningDesignations;
                MaxTiles = AutoForestryDesignationsMod.MaxTiles;
                MarkHarvestReadyForHarvest = AutoForestryDesignationsMod.MarkHarvestReadyForHarvest;
            }

            public static AFDTowerSettings FromGlobalDefaults() => new AFDTowerSettings();

            public void SetOnlyFertileTiles(bool value) => OnlyFertileTiles = value;
            public void SetAvoidTilesWithTrees(bool value) => AvoidTilesWithTrees = value;
            public void SetAvoidMiningDesignations(bool value) => AvoidMiningDesignations = value;
            public void SetMaxTiles(int value) => MaxTiles = Math.Max(0, value);
            public void SetMarkHarvestReadyForHarvest(bool value) => MarkHarvestReadyForHarvest = value;
        }

        private static readonly Dictionary<EntityId, AFDTowerSettings> s_towerSettingsByEntityId =
            new Dictionary<EntityId, AFDTowerSettings>();
        [System.Diagnostics.Conditional("DEBUG")]
        private static void LogDebug(string message)
        {
            Log.Info(message);
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

        // --- Forestry per-tower settings accessors ---
        internal static bool GetTowerOnlyFertileTiles(IAreaManagingTower tower) => GetOrCreateTowerSettings(tower).OnlyFertileTiles;
        internal static void SetTowerOnlyFertileTiles(IAreaManagingTower tower, bool value) => GetOrCreateTowerSettings(tower).SetOnlyFertileTiles(value);

        internal static bool GetTowerAvoidTilesWithTrees(IAreaManagingTower tower) => GetOrCreateTowerSettings(tower).AvoidTilesWithTrees;
        internal static void SetTowerAvoidTilesWithTrees(IAreaManagingTower tower, bool value) => GetOrCreateTowerSettings(tower).SetAvoidTilesWithTrees(value);

        internal static bool GetTowerAvoidMiningDesignations(IAreaManagingTower tower) => GetOrCreateTowerSettings(tower).AvoidMiningDesignations;
        internal static void SetTowerAvoidMiningDesignations(IAreaManagingTower tower, bool value) => GetOrCreateTowerSettings(tower).SetAvoidMiningDesignations(value);

        internal static int GetTowerMaxTiles(IAreaManagingTower tower) => GetOrCreateTowerSettings(tower).MaxTiles;
        internal static void SetTowerMaxTiles(IAreaManagingTower tower, int value) => GetOrCreateTowerSettings(tower).SetMaxTiles(value);

        internal static bool GetTowerMarkHarvestReadyForHarvest(IAreaManagingTower tower) => GetOrCreateTowerSettings(tower).MarkHarvestReadyForHarvest;
        internal static void SetTowerMarkHarvestReadyForHarvest(IAreaManagingTower tower, bool value) => GetOrCreateTowerSettings(tower).SetMarkHarvestReadyForHarvest(value);

        public static void Initialize(
            ITerrainDesignationsManager desigManager,
            ProtosDb protosDb,
            IWorldMapManager worldMapManager,
            MonoBehaviour coroutineHost,
            IEntitiesManager entitiesManager,
            ISimLoopEvents? simLoopEvents = null)
        {
            // Load defaults after logging is initialized so diagnostics are visible.
            LoadSettingsFromJson();

            s_desigManager = desigManager as TerrainDesignationsManager;
            s_coroutineHost = coroutineHost;
            s_protosDb = protosDb;
            s_worldMapManager = worldMapManager as WorldMapManager;
            s_entitiesManager = entitiesManager;
            s_simLoopEvents = simLoopEvents;

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

    }
}
