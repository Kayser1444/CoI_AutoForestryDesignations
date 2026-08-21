// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
// Auto Forestry Designations - Settings Loading and Parsing
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Mafi;

namespace AutoForestryDesignations
{
    public static partial class AutoForestryDesignation
    {
        private const string SETTINGS_FILE_NAME = "AFDsettings.json";

        private static bool s_settingsLoadAttempted;
        private static string? s_loadedSettingsPath;

        private static void LoadSettingsFromJson()
        {
            s_settingsLoadAttempted = true;

            try
            {
                string? settingsPath = ResolveSettingsPath();
                if (string.IsNullOrWhiteSpace(settingsPath) || !File.Exists(settingsPath))
                {
                    string? genPath = SavedSettingsPath;
                    if (!string.IsNullOrWhiteSpace(genPath))
                    {
                        try
                        {
                            File.WriteAllText(genPath, BuildSettingsJson());
                            s_loadedSettingsPath = genPath;
                            s_log.Warning($"{SETTINGS_FILE_NAME} not found - defaults written to: {genPath}");
                        }
                        catch (Exception writeEx)
                        {
                            s_loadedSettingsPath = null;
                            s_log.Warning($"Could not write default {SETTINGS_FILE_NAME}: {writeEx.Message}");
                        }
                    }
                    else
                    {
                        s_loadedSettingsPath = null;
                        s_log.Warning($"{SETTINGS_FILE_NAME} not found and mod root path is unknown; using built-in defaults.");
                    }
                    return;
                }

                string json = File.ReadAllText(settingsPath);
                string? fileVersion = ParseSettingsJson(json);
                s_loadedSettingsPath = settingsPath;

                if (fileVersion != AutoForestryDesignationsMod.ModVersion)
                {
                    if (TrySaveSettings(out string migratedPath))
                        s_log.Warning($"{SETTINGS_FILE_NAME} migrated to version {AutoForestryDesignationsMod.ModVersion}: {migratedPath}");
                }
            }
            catch (Exception ex)
            {
                s_loadedSettingsPath = null;
                s_log.Warning($"Failed to load {SETTINGS_FILE_NAME}: {ex.Message}");
            }
        }

        private static string? ResolveSettingsPath()
        {
            var rootDirs = new List<string>();

            try { TryAddCandidateRoot(rootDirs, s_modRootDirectoryPath); } catch { }
            try { TryAddCandidateRoot(rootDirs, typeof(AutoForestryDesignation).Assembly.Location); } catch { }

            try
            {
                string? codeBase = typeof(AutoForestryDesignation).Assembly.CodeBase;
                if (!string.IsNullOrWhiteSpace(codeBase)
                    && Uri.TryCreate(codeBase, UriKind.Absolute, out Uri uri)
                    && uri.IsFile)
                {
                    TryAddCandidateRoot(rootDirs, uri.LocalPath);
                }
            }
            catch
            {
            }

            try { TryAddCandidateRoot(rootDirs, AppDomain.CurrentDomain.BaseDirectory); } catch { }
            try { TryAddCandidateRoot(rootDirs, Directory.GetCurrentDirectory()); } catch { }

            foreach (string root in rootDirs)
            {
                DirectoryInfo? dir;
                try
                {
                    dir = new DirectoryInfo(root);
                }
                catch
                {
                    continue;
                }

                for (int i = 0; i < 8 && dir != null; i++)
                {
                    string candidateSettings;
                    string candidateManifest;
                    try
                    {
                        candidateSettings = Path.Combine(dir.FullName, SETTINGS_FILE_NAME);
                        candidateManifest = Path.Combine(dir.FullName, "manifest.json");
                    }
                    catch
                    {
                        dir = dir.Parent;
                        continue;
                    }

                    if (File.Exists(candidateSettings) && File.Exists(candidateManifest))
                    {
                        return candidateSettings;
                    }

                    if (i == 0 && File.Exists(candidateSettings))
                    {
                        return candidateSettings;
                    }

                    dir = dir.Parent;
                }
            }

            return null;
        }

        private static void TryAddCandidateRoot(List<string> roots, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            string? directory;
            try
            {
                directory = Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
            }
            catch
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(directory) && !roots.Contains(directory))
            {
                roots.Add(directory);
            }
        }

        private static string? ParseSettingsJson(string json)
        {
            string? parsedVersion = null;
            try
            {
                string? diagnosticLevel = ParseString(json, "diagnosticLevel");
                if (diagnosticLevel != null
                    && !AfdDiagnostics.TryApplyConfiguredLevel(
                        diagnosticLevel,
                        out string diagnosticLevelError))
                {
                    s_log.Warning(
                        $"Invalid diagnosticLevel '{diagnosticLevel}' in {SETTINGS_FILE_NAME}. "
                        + diagnosticLevelError + $" Using {AfdDiagnostics.Level}.");
                }

                s_batchSize = ClampBatchSize(ParseInt(json, "batchSize") ?? s_batchSize);

                bool? onlyFertileTiles = ParseBool(json, "onlyFertileTiles");
                if (onlyFertileTiles.HasValue)
                    AutoForestryDesignationsMod.SetOnlyFertileTiles(onlyFertileTiles.Value);

                bool? avoidTilesWithTrees = ParseBool(json, "avoidTilesWithTrees");
                if (avoidTilesWithTrees.HasValue)
                    AutoForestryDesignationsMod.SetAvoidTilesWithTrees(avoidTilesWithTrees.Value);

                bool? overrideTerrainDesignations = ParseBool(json, "overrideTerrainDesignations");
                if (overrideTerrainDesignations.HasValue)
                    AutoForestryDesignationsMod.SetOverrideTerrainDesignations(overrideTerrainDesignations.Value);
                else
                {
                    bool? legacyAvoidMiningDesignations = ParseBool(json, "avoidMiningDesignations");
                    if (legacyAvoidMiningDesignations.HasValue)
                        AutoForestryDesignationsMod.SetOverrideTerrainDesignations(!legacyAvoidMiningDesignations.Value);
                }

                bool? avoidFlatTiles = ParseBool(json, "avoidFlatTiles");
                if (avoidFlatTiles.HasValue)
                    AutoForestryDesignationsMod.SetAvoidFlatTiles(avoidFlatTiles.Value);

                bool? onlyReachableTiles = ParseBool(json, "onlyReachableTiles");
                if (onlyReachableTiles.HasValue)
                    AutoForestryDesignationsMod.SetOnlyReachableTiles(onlyReachableTiles.Value);

                int? pathabilityTargetSize = ParseInt(json, "pathabilityTargetSize");
                if (pathabilityTargetSize.HasValue)
                    AutoForestryDesignationsMod.SetPathabilityTargetSize(pathabilityTargetSize.Value);

                int? maxTiles = ParseInt(json, "maxTiles");
                if (maxTiles.HasValue)
                    AutoForestryDesignationsMod.SetMaxTiles(maxTiles.Value);

                int? targetYield = ParseInt(json, "targetYield");
                if (targetYield.HasValue)
                {
                    // An old settings file may be rewritten with the new field
                    // while retaining its legacy cap. Preserve that cap until
                    // the player explicitly changes Target yield in-game.
                    bool preserveLegacyMaxTiles = targetYield.Value == 0 && maxTiles.HasValue && maxTiles.Value > 0;
                    AutoForestryDesignationsMod.LoadTargetYield(targetYield.Value, preserveLegacyMaxTiles);
                }

                bool? markHarvestReadyForHarvest = ParseBool(json, "markHarvestReadyForHarvest");
                if (markHarvestReadyForHarvest.HasValue)
                    AutoForestryDesignationsMod.SetMarkHarvestReadyForHarvest(markHarvestReadyForHarvest.Value);

                bool? forestryDesignationsPanelCollapsed = ParseBool(json, "forestryDesignationsPanelCollapsed");
                if (forestryDesignationsPanelCollapsed.HasValue)
                    AutoForestryDesignationsMod.SetForestryDesignationsPanelCollapsed(forestryDesignationsPanelCollapsed.Value);

                bool? forestryInformationPanelCollapsed = ParseBool(json, "forestryInformationPanelCollapsed");
                if (forestryInformationPanelCollapsed.HasValue)
                    AutoForestryDesignationsMod.SetForestryInformationPanelCollapsed(forestryInformationPanelCollapsed.Value);

                bool? truckPoolingEnabled = ParseBool(json, "truckPoolingEnabled");
                if (truckPoolingEnabled.HasValue)
                    AutoForestryDesignationsMod.SetTruckPoolingEnabled(truckPoolingEnabled.Value);

                bool? forestryVehicleOptimizations = ParseBool(json, "forestryVehicleOptimizations");
                if (forestryVehicleOptimizations.HasValue)
                    AutoForestryDesignationsMod.SetForestryVehicleOptimizations(forestryVehicleOptimizations.Value);

                parsedVersion = ParseString(json, "settingsVersion");
            }
            catch (Exception ex)
            {
                s_log.Warning($"Error parsing {SETTINGS_FILE_NAME}: {ex.Message}");
            }
            return parsedVersion;
        }

        private static bool? ParseBool(string json, string key)
        {
            try
            {
                int idx = json.IndexOf($"\"{key}\":", StringComparison.Ordinal);
                if (idx < 0) return null;
                int valStart = json.IndexOf(':', idx) + 1;
                while (valStart < json.Length && char.IsWhiteSpace(json[valStart])) valStart++;

                if (json.Substring(valStart).StartsWith("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (json.Substring(valStart).StartsWith("false", StringComparison.OrdinalIgnoreCase)) return false;
                return null;
            }
            catch { return null; }
        }

        private static int? ParseInt(string json, string key)
        {
            try
            {
                int idx = json.IndexOf($"\"{key}\":", StringComparison.Ordinal);
                if (idx < 0) return null;
                int valStart = idx + key.Length + 3;
                while (valStart < json.Length && char.IsWhiteSpace(json[valStart])) valStart++;
                int valEnd = valStart;
                while (valEnd < json.Length && (char.IsDigit(json[valEnd]) || json[valEnd] == '-')) valEnd++;
                if (valEnd == valStart) return null;
                if (int.TryParse(json.Substring(valStart, valEnd - valStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                    return result;
                return null;
            }
            catch { return null; }
        }

        private static string? ParseString(string json, string key)
        {
            try
            {
                int idx = json.IndexOf($"\"{key}\":", StringComparison.Ordinal);
                if (idx < 0) return null;
                int valStart = json.IndexOf('"', idx + key.Length + 3);
                if (valStart < 0) return null;
                valStart++;
                int valEnd = json.IndexOf('"', valStart);
                if (valEnd < 0) return null;
                return json.Substring(valStart, valEnd - valStart);
            }
            catch { return null; }
        }

        private static string BoolToJsonStr(bool value) => value ? "true" : "false";

        internal static string BuildSettingsJson()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"settingsVersion\": \"{AutoForestryDesignationsMod.ModVersion}\",");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_diagnosticLevel\": \"Controls AFD diagnostic output. Default selects Debug in Debug builds and Info in Release builds. Warning keeps only warnings/errors; Info adds concise operational messages; Debug adds scan summaries and calculations; Trace adds per-vehicle claim and staging details. The afd_diagnostic_level command overrides this for the current session. Allowed: Default, Warning, Info, Debug, Trace.\",");
            sb.AppendLine($"  \"diagnosticLevel\": \"{AfdDiagnostics.ConfiguredLevel}\",");
            sb.AppendLine();
            sb.AppendLine("  \"_comment\": \"AutoForestryDesignations settings. These values set the world defaults loaded at game start. Tower-specific settings are saved in the mod cache.\",");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_batchSize\": \"Legacy compatibility setting. Scans now use 10 ms play / 30 ms paused planning budgets and commit with the game's bulk designation command.\",");
            sb.AppendLine($"  \"batchSize\": {s_batchSize},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_onlyFertileTiles\": \"Default for new tower panels. When true, Create designations places designations only where the ground is fertile for tree growth. Default: true.\",");
            sb.AppendLine($"  \"onlyFertileTiles\": {BoolToJsonStr(AutoForestryDesignationsMod.OnlyFertileTiles)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_avoidTilesWithTrees\": \"Default for new tower panels. When true, Create designations skips tiles that already contain a tree. Default: false.\",");
            sb.AppendLine($"  \"avoidTilesWithTrees\": {BoolToJsonStr(AutoForestryDesignationsMod.AvoidTilesWithTrees)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_overrideTerrainDesignations\": \"World setting. When true, Create designations may replace existing terrain designations, including mining, dumping, or leveling. Default: false.\",");
            sb.AppendLine($"  \"overrideTerrainDesignations\": {BoolToJsonStr(AutoForestryDesignationsMod.OverrideTerrainDesignations)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_avoidFlatTiles\": \"Default for new tower panels. When true, Create designations skips flat 4x4 tiles whose four corner HeightTilesF values are within the game's 0.0625-tile surface-height tolerance of the same integer height. Default: false.\",");
            sb.AppendLine($"  \"avoidFlatTiles\": {BoolToJsonStr(AutoForestryDesignationsMod.AvoidFlatTiles)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_onlyReachableTiles\": \"Default for new tower panels. When true, Create designations skips candidate tiles that are not reachable by vehicle pathability from the tower area. Default: true.\",");
            sb.AppendLine($"  \"onlyReachableTiles\": {BoolToJsonStr(AutoForestryDesignationsMod.OnlyReachableTiles)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_pathabilityTargetSize\": \"Hidden tuning parameter for reachability matching. Interpreted as n*n area around each candidate center (for example 3 = 3x3). Larger values are more permissive and reduce holes; smaller values are stricter. Clamped to 1..9. Default: 3.\",");
            sb.AppendLine($"  \"pathabilityTargetSize\": {AutoForestryDesignationsMod.PathabilityTargetSize},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_targetYield\": \"Default sustainable wood production target per in-game month for new tower panels. 0 = no target (∞). Default: 0.\",");
            sb.AppendLine($"  \"targetYield\": {AutoForestryDesignationsMod.TargetYield},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_maxTiles\": \"Legacy maximum number of forestry designation tiles to place per run. Retained for old settings and hidden once Target yield is adopted. 0 = no limit.\",");
            sb.AppendLine($"  \"maxTiles\": {AutoForestryDesignationsMod.MaxTiles},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_markHarvestReadyForHarvest\": \"Default for new tower panels. When true, trees that match the tower's harvesting options are marked for harvest after creating designations. Default: false.\",");
            sb.AppendLine($"  \"markHarvestReadyForHarvest\": {BoolToJsonStr(AutoForestryDesignationsMod.MarkHarvestReadyForHarvest)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_forestryDesignationsPanelCollapsed\": \"Default collapsed state for the Forestry designations panel when a forestry tower inspector is created. false = expanded by default, true = collapsed by default. Default: false.\",");
            sb.AppendLine($"  \"forestryDesignationsPanelCollapsed\": {BoolToJsonStr(AutoForestryDesignationsMod.ForestryDesignationsPanelCollapsed)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_forestryInformationPanelCollapsed\": \"Default collapsed state for the Forestry information panel when a forestry tower inspector is created. false = expanded by default, true = collapsed by default. Default: false.\",");
            sb.AppendLine($"  \"forestryInformationPanelCollapsed\": {BoolToJsonStr(AutoForestryDesignationsMod.ForestryInformationPanelCollapsed)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_truckPoolingEnabled\": \"Default for new tower panels. When true, trucks assigned to forestry towers are pooled and automatically distributed among its active tree harvesters. Default: true.\",");
            sb.AppendLine($"  \"truckPoolingEnabled\": {BoolToJsonStr(AutoForestryDesignationsMod.TruckPoolingEnabled)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_forestryVehicleOptimizations\": \"World-level setting. When true, assigned forestry planters and harvesters stay in the field, coordinate future work, and do not return to their tower merely to wait. Default: true.\",");
            sb.AppendLine($"  \"forestryVehicleOptimizations\": {BoolToJsonStr(AutoForestryDesignationsMod.ForestryVehicleOptimizations)}");
            sb.Append("}");
            return sb.ToString();
        }

        internal static string? SavedSettingsPath
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(s_loadedSettingsPath))
                    return s_loadedSettingsPath;
                if (!string.IsNullOrWhiteSpace(s_modRootDirectoryPath))
                    return Path.Combine(s_modRootDirectoryPath, SETTINGS_FILE_NAME);
                return null;
            }
        }

        internal static bool TrySaveSettings(out string savedPath)
        {
            string? target = SavedSettingsPath;
            if (target == null || target.Trim().Length == 0)
            {
                savedPath = string.Empty;
                s_log.Warning($"Cannot save {SETTINGS_FILE_NAME}: mod root path is unknown.");
                return false;
            }
            string targetPath = target;

            try
            {
                File.WriteAllText(targetPath, BuildSettingsJson());
                s_loadedSettingsPath = targetPath;
                savedPath = targetPath;
                return true;
            }
            catch (Exception ex)
            {
                savedPath = string.Empty;
                s_log.Warning($"Failed to save {SETTINGS_FILE_NAME}: {ex.Message}");
                return false;
            }
        }
    }
}
