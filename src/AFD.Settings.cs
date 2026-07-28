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
                            Log.Warning($"[AFD] {SETTINGS_FILE_NAME} not found - defaults written to: {genPath}");
                        }
                        catch (Exception writeEx)
                        {
                            s_loadedSettingsPath = null;
                            Log.Warning($"[AFD] Could not write default {SETTINGS_FILE_NAME}: {writeEx.Message}");
                        }
                    }
                    else
                    {
                        s_loadedSettingsPath = null;
                        Log.Warning($"[AFD] {SETTINGS_FILE_NAME} not found and mod root path is unknown; using built-in defaults.");
                    }
                    return;
                }

                string json = File.ReadAllText(settingsPath);
                string? fileVersion = ParseSettingsJson(json);
                s_loadedSettingsPath = settingsPath;

                if (fileVersion != AutoForestryDesignationsMod.ModVersion)
                {
                    if (TrySaveSettings(out string migratedPath))
                        Log.Warning($"[AFD] {SETTINGS_FILE_NAME} migrated to version {AutoForestryDesignationsMod.ModVersion}: {migratedPath}");
                }
            }
            catch (Exception ex)
            {
                s_loadedSettingsPath = null;
                Log.Warning($"[AFD] Failed to load {SETTINGS_FILE_NAME}: {ex.Message}");
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
                s_batchSize = ClampBatchSize(ParseInt(json, "batchSize") ?? s_batchSize);

                bool? onlyFertileTiles = ParseBool(json, "onlyFertileTiles");
                if (onlyFertileTiles.HasValue)
                    AutoForestryDesignationsMod.SetOnlyFertileTiles(onlyFertileTiles.Value);

                bool? avoidTilesWithTrees = ParseBool(json, "avoidTilesWithTrees");
                if (avoidTilesWithTrees.HasValue)
                    AutoForestryDesignationsMod.SetAvoidTilesWithTrees(avoidTilesWithTrees.Value);

                bool? avoidMiningDesignations = ParseBool(json, "avoidMiningDesignations");
                if (avoidMiningDesignations.HasValue)
                    AutoForestryDesignationsMod.SetAvoidMiningDesignations(avoidMiningDesignations.Value);

                bool? onlyReachableTiles = ParseBool(json, "onlyReachableTiles");
                if (onlyReachableTiles.HasValue)
                    AutoForestryDesignationsMod.SetOnlyReachableTiles(onlyReachableTiles.Value);

                int? pathabilityTargetSize = ParseInt(json, "pathabilityTargetSize");
                if (pathabilityTargetSize.HasValue)
                    AutoForestryDesignationsMod.SetPathabilityTargetSize(pathabilityTargetSize.Value);

                int? maxTiles = ParseInt(json, "maxTiles");
                if (maxTiles.HasValue)
                    AutoForestryDesignationsMod.SetMaxTiles(maxTiles.Value);

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

                parsedVersion = ParseString(json, "settingsVersion");
            }
            catch (Exception ex)
            {
                Log.Warning($"[AFD] Error parsing {SETTINGS_FILE_NAME}: {ex.Message}");
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
            sb.AppendLine("  \"_comment\": \"AutoForestryDesignations settings. These values set the defaults loaded at game start. Most settings can also be changed per forestry tower in-game via the tower inspector.\",");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_batchSize\": \"How many designations are placed per coroutine frame before yielding to the game. Lower values keep the game more responsive during large scans; higher values complete scans faster. While paused, the effective batch size is boosted by x4 and clamped. Absolute max: 200. Default: 30.\",");
            sb.AppendLine($"  \"batchSize\": {s_batchSize},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_onlyFertileTiles\": \"Default for new tower panels. When true, Create designations places designations only where the ground is fertile for tree growth. Default: true.\",");
            sb.AppendLine($"  \"onlyFertileTiles\": {BoolToJsonStr(AutoForestryDesignationsMod.OnlyFertileTiles)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_avoidTilesWithTrees\": \"Default for new tower panels. When true, Create designations skips tiles that already contain a tree. Default: false.\",");
            sb.AppendLine($"  \"avoidTilesWithTrees\": {BoolToJsonStr(AutoForestryDesignationsMod.AvoidTilesWithTrees)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_avoidMiningDesignations\": \"Default for new tower panels. When true, Create designations skips tiles that already contain any terrain designation, including mining, dumping, or leveling. Default: true.\",");
            sb.AppendLine($"  \"avoidMiningDesignations\": {BoolToJsonStr(AutoForestryDesignationsMod.AvoidMiningDesignations)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_onlyReachableTiles\": \"Default for new tower panels. When true, Create designations skips candidate tiles that are not reachable by vehicle pathability from the tower area. Default: true.\",");
            sb.AppendLine($"  \"onlyReachableTiles\": {BoolToJsonStr(AutoForestryDesignationsMod.OnlyReachableTiles)},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_pathabilityTargetSize\": \"Hidden tuning parameter for reachability matching. Interpreted as n*n area around each candidate center (for example 3 = 3x3). Larger values are more permissive and reduce holes; smaller values are stricter. Clamped to 1..9. Default: 3.\",");
            sb.AppendLine($"  \"pathabilityTargetSize\": {AutoForestryDesignationsMod.PathabilityTargetSize},");
            sb.AppendLine();
            sb.AppendLine("  \"_comment_maxTiles\": \"Default maximum number of forestry designation tiles to place per run. 0 = no limit. Default: 0.\",");
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
            sb.AppendLine($"  \"truckPoolingEnabled\": {BoolToJsonStr(AutoForestryDesignationsMod.TruckPoolingEnabled)}");
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
                Log.Warning($"[AFD] Cannot save {SETTINGS_FILE_NAME}: mod root path is unknown.");
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
                Log.Warning($"[AFD] Failed to save {SETTINGS_FILE_NAME}: {ex.Message}");
                return false;
            }
        }
    }
}
