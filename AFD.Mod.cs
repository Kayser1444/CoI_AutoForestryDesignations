// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
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
        AutoDepthDesignation.Apply(m_harmony);
    }

    public void RegisterDependencies(DependencyResolverBuilder depBuilder, ProtosDb protosDb, bool gameWasLoaded)
    {
    }

    public void EarlyInit(DependencyResolver resolver)
    {
    }

    public static int MaxHeightDiff { get; private set; } = 1;

    public static void SetMaxHeightDiff(int value)
    {
        MaxHeightDiff = Math.Max(1, Math.Min(3, value));
    }

    /// <summary>Ramp width in tiles. Allowed range: 0..5. 0 disables ramp generation.</summary>
    public static int RampWidth { get; private set; } = 2;

    public static void SetRampWidth(int value)
    {
        RampWidth = Math.Max(0, Math.Min(5, value));
    }

    /// <summary>Maximum number of layers to excavate from the surface. 0 = no limit.</summary>
    public static int MaxLayersToExcavate { get; private set; } = 30;

    public static void SetMaxLayersToExcavate(int value)
    {
        MaxLayersToExcavate = Math.Max(0, value);
    }

    /// <summary>Absolute minimum terrain elevation to excavate to. null = no limit.</summary>
    public static int? MaxDepthToDigTo { get; private set; } = null;

    public static void SetMaxDepthToDigTo(int? value)
    {
        MaxDepthToDigTo = value;
    }

    /// <summary>
    /// Ore purity threshold level (0=Off, 1=Low, 2=Medium, 3=High, 4=Max).
    /// Controls how aggressively poor-quality tiles and deep sparse ore are excluded.
    /// </summary>
    public static int OrePurityLevel { get; private set; } = 0;

    public static void SetOrePurityLevel(int value)
    {
        OrePurityLevel = Math.Max(0, Math.Min(4, value));
    }

    /// <summary>
    /// Minimum corridor clearance for designation connectivity.
    /// 0 = disabled — no corridors drawn, components left separate (for vehicle-less excavation);
    /// 1 = 1-tile corridors (small/medium vehicles);
    /// 2 = 2-tile corridors (mega vehicles, current default).
    /// </summary>
    public static int MinCorridorClearance { get; private set; } = 2;

    public static void SetMinCorridorClearance(int value)
    {
        MinCorridorClearance = Math.Max(0, Math.Min(2, value));
    }

    // --- Forestry-specific global defaults ---

    /// <summary>Skip tiles where the ground is not fertile for tree growth. Default: true.</summary>
    public static bool AvoidInfertileTiles { get; private set; } = true;
    public static void SetAvoidInfertileTiles(bool value) => AvoidInfertileTiles = value;

    /// <summary>Skip tiles that already have a tree. Default: false.</summary>
    public static bool AvoidTilesWithTrees { get; private set; } = false;
    public static void SetAvoidTilesWithTrees(bool value) => AvoidTilesWithTrees = value;

    /// <summary>Maximum number of forestry designation tiles to place per run. 0 = no limit.</summary>
    public static int MaxTiles { get; private set; } = 0;
    public static void SetMaxTiles(int value) => MaxTiles = Math.Max(0, value);

    /// <summary>After placing designations, mark all fully grown trees in the area for harvest.</summary>
    public static bool MarkFullyGrownForHarvest { get; private set; } = false;
    public static void SetMarkFullyGrownForHarvest(bool value) => MarkFullyGrownForHarvest = value;

    public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
    {
        try
        {
            // Enable console logging for easier debugging
            ConsoleLogger.Enable();

#if DEBUG
            // Auto-enable Mafi console mirroring in Debug builds so logs show up in-game
            // without requiring a manual `also_log_to_console` command each launch.
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
#endif

            ITerrainDesignationsManager desigManager = resolver.Resolve<ITerrainDesignationsManager>();
            ProtosDb protosDb = resolver.Resolve<ProtosDb>();
            IWorldMapManager worldMapManager = resolver.Resolve<IWorldMapManager>();
            IEntitiesManager entitiesManager = resolver.Resolve<IEntitiesManager>();
            AutoForestryDesignationsTicker ticker = new GameObject("AutoForestryDesignationsTicker").AddComponent<AutoForestryDesignationsTicker>();
            UnityEngine.Object.DontDestroyOnLoad(ticker.gameObject);
            ISimLoopEvents simLoopEvents = resolver.Resolve<ISimLoopEvents>();
            AutoDepthDesignation.SetModRootDirectoryPath(Manifest.RootDirectoryPath);
            AutoDepthDesignation.Initialize(desigManager, protosDb, worldMapManager, ticker, entitiesManager, simLoopEvents);
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

    public void Dispose()
    {
        m_harmony?.UnpatchAll("com.auto-forestry-designations.mod");
    }
}
