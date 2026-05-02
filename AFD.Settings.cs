// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
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
                    s_loadedSettingsPath = null;
                    Log.Warning($"[AFD] {SETTINGS_FILE_NAME} not found next to mod assembly or parent mod folder; using built-in defaults.");
                    return;
                }

                string json = File.ReadAllText(settingsPath);
                ParseSettingsJson(json);
                s_loadedSettingsPath = settingsPath;
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

        private static void ParseSettingsJson(string json)
        {
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

                int? maxTiles = ParseInt(json, "maxTiles");
                if (maxTiles.HasValue)
                    AutoForestryDesignationsMod.SetMaxTiles(maxTiles.Value);

                bool? markHarvestReadyForHarvest = ParseBool(json, "markHarvestReadyForHarvest");
                if (markHarvestReadyForHarvest.HasValue)
                    AutoForestryDesignationsMod.SetMarkHarvestReadyForHarvest(markHarvestReadyForHarvest.Value);
            }
            catch (Exception ex)
            {
                Log.Warning($"[AFD] Error parsing {SETTINGS_FILE_NAME}: {ex.Message}");
            }
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
    }
}
