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
using CoI.AutoHelpers.Localization;
using CoI.AutoHelpers.Logging;

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
            // Enable console logging for easier debugging (must precede any log output)
            AutoForestryDesignation.s_log.EnableConsoleLogging();
            AutoForestryDesignation.s_log.RegisterAutoConsoleMirroring(this, resolver.Resolve<IGameLoopEvents>(), resolver.Resolve<GameConsoleCommandsExecutor>());
            RegisterLocalizationLateApply(resolver);

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
            AutoForestryDesignation.s_log.Exception(ex, "AutoForestryDesignations init");
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
        });
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
        savedValues.Clear();
    }

    public void Dispose()
    {
        m_harmony?.UnpatchAll("com.auto-forestry-designations.mod");
    }
}
