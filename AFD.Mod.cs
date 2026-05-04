// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Entities;
using Mafi.Core.Game;
using Mafi.Core.GameLoop;
using Mafi.Core.Mods;
using Mafi.Core.Simulation;
using Mafi.Core.Prototypes;
using Mafi.Core.Console;
using Mafi.Core.PathFinding;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.World;
using UnityEngine;

namespace AutoForestryDesignations;

public sealed class AutoForestryDesignationsMod : IMod, IDisposable
{
    private Harmony? m_harmony;

    public string Name => "Auto Forestry Designations";

    public int Version => 1;

    public bool IsUiOnly => false;

    public Option<IConfig> ModConfig { get; set; }

    public ModManifest Manifest { get; }

    public static string ModVersion { get; private set; } = "?";

    public ModJsonConfig JsonConfig { get; }

    public AutoForestryDesignationsMod(ModManifest manifest)
    {
        Manifest = manifest;
        ModVersion = manifest.Version.ToString();
        JsonConfig = new ModJsonConfig(this);
    }

    public void RegisterPrototypes(ProtoRegistrator registrator)
    {
        m_harmony = new Harmony("com.auto-forestry-designations.mod");
        AutoForestryDesignation.Apply(m_harmony);
    }

    public void RegisterDependencies(DependencyResolverBuilder depBuilder, ProtosDb protosDb, bool gameWasLoaded)
    {
    }

    public void EarlyInit(DependencyResolver resolver)
    {
    }

    public static bool OnlyFertileTiles { get; private set; } = true;
    public static void SetOnlyFertileTiles(bool value) => OnlyFertileTiles = value;

    /// <summary>Skip tiles that already have a tree. Default: false.</summary>
    public static bool AvoidTilesWithTrees { get; private set; } = false;
    public static void SetAvoidTilesWithTrees(bool value) => AvoidTilesWithTrees = value;

    /// <summary>Skip tiles that already have terrain designations. Default: true.</summary>
    public static bool AvoidMiningDesignations { get; private set; } = true;
    public static void SetAvoidMiningDesignations(bool value) => AvoidMiningDesignations = value;

    /// <summary>When enabled, create designations only on candidates reachable by vehicle pathability.</summary>
    public static bool OnlyReachableTiles { get; private set; } = true;
    public static void SetOnlyReachableTiles(bool value) => OnlyReachableTiles = value;

    /// <summary>
    /// Reachability target square size (n*n) used when matching candidate designation tiles
    /// to visited pathable tiles. Hidden tuning parameter; configurable via settings/console.
    /// </summary>
    public static int PathabilityTargetSize { get; private set; } = 3;
    public static void SetPathabilityTargetSize(int value) => PathabilityTargetSize = Math.Max(1, Math.Min(9, value));

    /// <summary>Maximum number of forestry designation tiles to place per run. 0 = no limit.</summary>
    public static int MaxTiles { get; private set; } = 0;
    public static void SetMaxTiles(int value) => MaxTiles = Math.Max(0, value);

    /// <summary>After placing designations, mark harvest-ready trees in the area for harvest.</summary>
    public static bool MarkHarvestReadyForHarvest { get; private set; } = false;
    public static void SetMarkHarvestReadyForHarvest(bool value) => MarkHarvestReadyForHarvest = value;

    /// <summary>Default collapsed state for the Forestry Designations inspector panel.</summary>
    public static bool ForestryDesignationsPanelCollapsed { get; private set; } = false;
    public static void SetForestryDesignationsPanelCollapsed(bool value) => ForestryDesignationsPanelCollapsed = value;

    /// <summary>Default collapsed state for the Forestry Information inspector panel.</summary>
    public static bool ForestryInformationPanelCollapsed { get; private set; } = false;
    public static void SetForestryInformationPanelCollapsed(bool value) => ForestryInformationPanelCollapsed = value;

    public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
    {
        try
        {
            // Enable console logging for easier debugging
            ConsoleLogger.Enable();
            RegisterDebugConsoleMirroring(resolver);

            ITerrainDesignationsManager desigManager = resolver.Resolve<ITerrainDesignationsManager>();
            IVehiclePathFindingManager vehiclePathFindingManager = resolver.Resolve<IVehiclePathFindingManager>();
            ProtosDb protosDb = resolver.Resolve<ProtosDb>();
            IWorldMapManager worldMapManager = resolver.Resolve<IWorldMapManager>();
            IEntitiesManager entitiesManager = resolver.Resolve<IEntitiesManager>();
            AutoForestryDesignationsTicker ticker = new GameObject("AutoForestryDesignationsTicker").AddComponent<AutoForestryDesignationsTicker>();
            UnityEngine.Object.DontDestroyOnLoad(ticker.gameObject);
            ISimLoopEvents simLoopEvents = resolver.Resolve<ISimLoopEvents>();
            AutoForestryDesignation.SetModRootDirectoryPath(Manifest.RootDirectoryPath);
            AutoForestryDesignation.Initialize(desigManager, protosDb, worldMapManager, ticker, entitiesManager, simLoopEvents, vehiclePathFindingManager);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AFD] AutoForestryDesignations init: " + ex.Message);
        }
    }

    public void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues)
    {
        savedValues.Clear();
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void RegisterDebugConsoleMirroring(DependencyResolver resolver)
    {
        var gameLoopEvents = resolver.Resolve<IGameLoopEvents>();
        var consoleCommands = resolver.Resolve<GameConsoleCommandsExecutor>();
        gameLoopEvents.RegisterRendererInitState(this, () =>
        {
            bool enabled = consoleCommands.ExecuteOrSchedule("also_log_to_console true");
            if (enabled)
                Debug.Log("[AFD] Debug build: auto-executed also_log_to_console.");
            else
                Debug.LogWarning("[AFD] Debug build: failed to auto-execute also_log_to_console.");
        });
    }

    public void Dispose()
    {
        m_harmony?.UnpatchAll("com.auto-forestry-designations.mod");
    }
}
