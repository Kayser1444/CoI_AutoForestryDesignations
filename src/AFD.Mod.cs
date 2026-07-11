// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System;
using System.IO;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Buildings.VehicleDepots;
using Mafi.Core.Entities;
using Mafi.Core.Game;
using Mafi.Core.GameLoop;
using Mafi.Core.Mods;
using Mafi.Core.Simulation;
using Mafi.Core.Prototypes;
using Mafi.Core.Console;
using Mafi.Core.PathFinding;
using Mafi.Core.SaveGame;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.World;
using Mafi.Unity.Terrain;
using Mafi.Unity.Trees;
using UnityEngine;
using CoI.AutoHelpers.Localization;
using CoI.AutoHelpers.Logging;
using CoI.AutoHelpers.Persistence;
using CoI.AutoHelpers.Settings;
using Mafi.Unity;
using Mafi.Unity.Ui.Hud;
using Mafi.Unity.UiToolkit;

namespace AutoForestryDesignations;

public sealed class AutoForestryDesignationsMod : IMod, IDisposable
{
    private Harmony? m_harmony;
    private readonly ModSaveLifecycle m_saveLifecycle = new ModSaveLifecycle();
    private IGameLoopEvents? m_gameLoopEvents;
    private ISimLoopEvents? m_simLoopEvents;
    private ISaveManager? m_saveManager;
    private IModStateJsonStore? m_towerSettingsStateStore;
    private IModStateJsonStore? m_preAllocationsStateStore;
    private IEntitiesManager? m_entitiesManager;

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
        PreAllocationPatches.Apply(m_harmony);
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

    /// <summary>Default collapsed state for the Forestry designations inspector panel.</summary>
    public static bool ForestryDesignationsPanelCollapsed { get; private set; } = false;
    public static void SetForestryDesignationsPanelCollapsed(bool value) => ForestryDesignationsPanelCollapsed = value;

    /// <summary>Default collapsed state for the Forestry information inspector panel.</summary>
    public static bool ForestryInformationPanelCollapsed { get; private set; } = false;
    public static void SetForestryInformationPanelCollapsed(bool value) => ForestryInformationPanelCollapsed = value;

    /// <summary>Resets all global defaults to their built-in values.</summary>
    public static void ResetGlobalDefaults()
    {
        SetOnlyFertileTiles(true);
        SetAvoidTilesWithTrees(false);
        SetAvoidMiningDesignations(true);
        SetOnlyReachableTiles(true);
        SetMaxTiles(0);
        SetMarkHarvestReadyForHarvest(false);
        SetForestryDesignationsPanelCollapsed(false);
        SetForestryInformationPanelCollapsed(false);
        AutoForestryDesignation.SetBatchSize(30);
    }

    public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
    {
        try
        {
            // Enable console logging for easier debugging (must precede any log output)
            AutoForestryDesignation.s_log.EnableConsoleLogging();
            m_gameLoopEvents = resolver.Resolve<IGameLoopEvents>();
            m_simLoopEvents = resolver.Resolve<ISimLoopEvents>();
            m_saveManager = resolver.Resolve<ISaveManager>();
            m_gameLoopEvents.Terminate.AddNonSaveable(this, onGameTerminated);
            m_simLoopEvents.BeforeSave.AddNonSaveable(this, beforeSave);
            m_saveManager.OnSaveDone += onSaveDone;

            AutoForestryDesignation.s_log.RegisterAutoConsoleMirroring(this, m_gameLoopEvents, resolver.Resolve<GameConsoleCommandsExecutor>());
            RegisterLocalizationLateApply(resolver);

            ITerrainDesignationsManager desigManager = resolver.Resolve<ITerrainDesignationsManager>();
            IVehiclePathFindingManager vehiclePathFindingManager = resolver.Resolve<IVehiclePathFindingManager>();
            ProtosDb protosDb = resolver.Resolve<ProtosDb>();
            IWorldMapManager worldMapManager = resolver.Resolve<IWorldMapManager>();
            IEntitiesManager entitiesManager = resolver.Resolve<IEntitiesManager>();
            AutoForestryDesignationsTicker ticker = new GameObject("AutoForestryDesignationsTicker").AddComponent<AutoForestryDesignationsTicker>();
            UnityEngine.Object.DontDestroyOnLoad(ticker.gameObject);
            m_entitiesManager = entitiesManager;
            m_entitiesManager.EntityRemoved.AddNonSaveable(this, onEntityRemoved);

            AutoForestryDesignation.SetModRootDirectoryPath(Manifest.RootDirectoryPath);
            AutoForestryDesignation.Initialize(desigManager, protosDb, worldMapManager, ticker, entitiesManager, m_simLoopEvents, vehiclePathFindingManager);
            m_towerSettingsStateStore = ModStateJsonStores.CreateDefault(JsonConfig, AutoForestryDesignation.TowerSettingsConfigKey);
            AutoForestryDesignation.LoadTowerSettingsFromJsonStore(m_towerSettingsStateStore);

            m_preAllocationsStateStore = ModStateJsonStores.CreateDefault(JsonConfig, "afdPendingVehicleAllocations");
            PendingVehicleAllocations.LoadFromJsonStore(m_preAllocationsStateStore);
            PendingVehicleAllocations.ReconcileQueues(entitiesManager);
        }
        catch (Exception ex)
        {
            unsubscribeWorldEvents();
            AutoForestryDesignation.s_log.Exception(ex, "AutoForestryDesignations init");
        }
    }

    private void beforeSave()
    {
        IModStateJsonStore store = m_towerSettingsStateStore
            ?? ModStateJsonStores.CreateDefault(JsonConfig, AutoForestryDesignation.TowerSettingsConfigKey);
        m_towerSettingsStateStore = store;
        AutoForestryDesignation.SaveTowerSettingsToJsonStore(store);

        if (m_preAllocationsStateStore != null)
        {
            if (m_entitiesManager != null)
            {
                PendingVehicleAllocations.ReconcileQueues(m_entitiesManager);
            }
            PendingVehicleAllocations.SaveToJsonStore(m_preAllocationsStateStore);
        }

        m_saveLifecycle.BeforeVanillaSave();
    }

    private void onSaveDone(SaveResult result)
    {
        m_saveLifecycle.AfterVanillaSave();
    }

    private void onGameTerminated()
    {
        unsubscribeWorldEvents();
        m_saveLifecycle.DisposeRuntime();
        PendingVehicleAllocations.ClearAll();
    }

    private void unsubscribeWorldEvents()
    {
        if (m_gameLoopEvents != null)
        {
            try { m_gameLoopEvents.Terminate.RemoveNonSaveable(this, onGameTerminated); }
            catch { }
            m_gameLoopEvents = null;
        }

        if (m_simLoopEvents != null)
        {
            try { m_simLoopEvents.BeforeSave.RemoveNonSaveable(this, beforeSave); }
            catch { }
            m_simLoopEvents = null;
        }

        if (m_saveManager != null)
        {
            try { m_saveManager.OnSaveDone -= onSaveDone; }
            catch { }
            m_saveManager = null;
        }

        if (m_entitiesManager != null)
        {
            try { m_entitiesManager.EntityRemoved.RemoveNonSaveable(this, onEntityRemoved); }
            catch { }
            m_entitiesManager = null;
        }
    }

    private void onEntityRemoved(IEntity entity)
    {
        if (entity is IEntityAssignedWithVehicles)
        {
            PendingVehicleAllocations.OnTowerDestroyed(entity.Id);
        }
        else if (entity is VehicleDepotBase)
        {
            PendingVehicleAllocations.OnDepotDestroyed(entity.Id);
        }
    }

    private void RegisterLocalizationLateApply(DependencyResolver resolver)
    {
        IGameLoopEvents gameLoopEvents = resolver.Resolve<IGameLoopEvents>();
        gameLoopEvents.RegisterRendererInitState(this, () =>
        {
            AutoForestryDesignation.s_log.Info($"AutoForestryDesignations v{ModVersion} | dll: {ModLogger.GetDllBuildTimestamp(typeof(AutoForestryDesignationsMod).Assembly)}");
            AutoForestryDesignation.s_log.Info("Localization: late apply at renderer init state.");
            ApplyLocalizedTextIfPresent();
            RegisterSettingsTabs(resolver);
            try
            {
                AutoForestryDesignation.SetTreesRenderer(resolver.Resolve<TreesRenderer>());
                AutoForestryDesignation.s_log.Info("TreesRenderer resolved successfully.");
            }
            catch (Exception ex)
            {
                AutoForestryDesignation.s_log.Warning($"TreesRenderer not available at renderer init: {ex.Message}");
            }
            try
            {
                var audioDb = resolver.Resolve<Mafi.Unity.Audio.AudioDb>();
                var clickSound = audioDb.GetSharedAudioUi("Assets/Unity/UserInterface/Audio/ButtonClick.prefab");
                AutoForestryDesignation.SetClickSound(clickSound);
                AutoForestryDesignation.s_log.Info("ButtonClick audio source resolved successfully.");
            }
            catch (Exception ex)
            {
                AutoForestryDesignation.s_log.Warning($"ButtonClick audio source not available at renderer init: {ex.Message}");
            }
            try
            {
                AutoForestryDesignation.SetHarvestHighlightManager(resolver.Resolve<TreeHarvestingHighlightManager>());
                AutoForestryDesignation.s_log.Info("TreeHarvestingHighlightManager resolved successfully.");
            }
            catch (Exception ex)
            {
                AutoForestryDesignation.s_log.Warning($"TreeHarvestingHighlightManager not available at renderer init: {ex.Message}");
            }
        });
    }

    private static void RegisterSettingsTabs(DependencyResolver resolver)
    {
        try
        {
            ModSettings.EnsureInitialized(
                resolver.Resolve<HudController>(),
                resolver.Resolve<UiRoot>(),
                resolver.Resolve<IRootEscapeManager>());

            ModSettings.RegisterTab(AfdModSettingsTab.BuildDefaultsTab());
            ModSettings.RegisterTab(AfdModSettingsTab.BuildGameSettingsTab());
        }
        catch (Exception ex)
        {
            AutoForestryDesignation.s_log.Exception(ex, "AFD settings tab registration");
        }
    }

    private void ApplyLocalizedTextIfPresent()
    {
        string translationsDirectory = Path.Combine(Manifest.RootDirectoryPath, "translations");
        AutoForestryDesignation.s_log.Info($"Localization: probing directory '{translationsDirectory}'.");

        if (!Directory.Exists(translationsDirectory))
        {
            AutoForestryDesignation.s_log.Warning("Localization: translations directory does not exist; skipping.");
            return;
        }

        string[] jsonFiles = Directory.GetFiles(translationsDirectory, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);
        if (jsonFiles.Length == 0)
        {
            AutoForestryDesignation.s_log.Warning("Localization: no translation JSON files found.");
        }
        else
        {
            AutoForestryDesignation.s_log.Info($"Localization: discovered {jsonFiles.Length} file(s): {string.Join(", ", jsonFiles)}");
        }

        string currentCulture = ResolveCurrentCultureCodeForLogging() ?? "<null>";
        AutoForestryDesignation.s_log.Info($"Localization: current game culture before apply = '{currentCulture}'.");

        ModTranslationsApplyResult result = new ModTranslations().Apply(new ModTranslationsApplyOptions(
            translationsDirectory,
            typeof(AutoForestryDesignationsMod).Assembly,
            Array.Empty<string>()));

        AutoForestryDesignation.s_log.Info(
            $"Localization: applied locale='{result.AppliedLocaleCode}', upserted={result.UpsertedEntryCount}, scannedFields={result.ScannedFieldCount}, reboundFields={result.ReboundFieldCount}, readonlySkipped={result.SkippedReadonlyFieldCount}, missingTranslationSkipped={result.SkippedMissingTranslationFieldCount}, failedWrites={result.FailedFieldCount}, diagnostics={result.Diagnostics.Count}.");

        foreach (TranslationDiagnostic diagnostic in result.Diagnostics)
        {
            string itemInfo = diagnostic.ItemIndex.HasValue ? $", itemIndex={diagnostic.ItemIndex.Value}" : string.Empty;
            string message = $"Localization diagnostic [{diagnostic.Severity}] source='{diagnostic.SourcePath}'{itemInfo}: {diagnostic.Message}";
            if (diagnostic.Severity == TranslationDiagnosticSeverity.Info)
                AutoForestryDesignation.s_log.Info(message);
            else
                AutoForestryDesignation.s_log.Warning(message);
        }

        if (result.UpsertedEntryCount > 0 && result.ReboundFieldCount == 0)
        {
            AutoForestryDesignation.s_log.Warning("Localization: zero fields were rebound. Localized static LocStr fields may not have been discovered.");
        }

        if (result.SkippedReadonlyFieldCount > 0)
        {
            AutoForestryDesignation.s_log.Warning($"Localization: {result.SkippedReadonlyFieldCount} readonly field(s) could not be overwritten.");
        }

        if (result.SkippedMissingTranslationFieldCount > 0)
        {
            AutoForestryDesignation.s_log.Warning($"Localization: {result.SkippedMissingTranslationFieldCount} field(s) had no matching translation entry.");
        }

        if (result.FailedFieldCount > 0)
        {
            AutoForestryDesignation.s_log.Warning($"Localization: {result.FailedFieldCount} field write(s) failed unexpectedly.");
        }

        if (result.HasErrors)
        {
            AutoForestryDesignation.s_log.Warning($"Localization apply finished with {result.Diagnostics.Count} diagnostic(s).");
        }
    }

    private static string? ResolveCurrentCultureCodeForLogging()
    {
        Type? localizationManagerType = Type.GetType("Mafi.Localization.LocalizationManager, Mafi", throwOnError: false);
        if (localizationManagerType == null)
        {
            return null;
        }

        var currentLangInfoProperty = localizationManagerType.GetProperty(
            "CurrentLangInfo",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (currentLangInfoProperty == null)
        {
            return null;
        }

        object? currentLangInfo = currentLangInfoProperty.GetValue(null);
        if (currentLangInfo == null)
        {
            return null;
        }

        var cultureInfoIdField = currentLangInfo.GetType().GetField(
            "CultureInfoId",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (cultureInfoIdField == null)
        {
            return null;
        }

        return cultureInfoIdField.GetValue(currentLangInfo) as string;
    }

    public void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues)
    {
    }

    public void Dispose()
    {
        unsubscribeWorldEvents();
        m_saveLifecycle.DisposeRuntime();
        m_harmony?.UnpatchAll("com.auto-forestry-designations.mod");
    }
}

