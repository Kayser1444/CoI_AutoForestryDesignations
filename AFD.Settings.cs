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
    public static partial class AutoDepthDesignation
    {
        private const float MIN_ORE_HEIGHT_THRESHOLD = 1.0f;

        private static float[] s_minOreHeightByLevel;
        private static float[] s_minBottomOreDensityByLevel;
        private static float[] s_minOrePurityByLevel;
        private static int[] s_minComponentSizeByLevel;
        private static bool s_settingsLoadAttempted;
        private static string? s_loadedSettingsPath;

        static AutoDepthDesignation()
        {
            // Initialize with defaults
            s_minOreHeightByLevel         = new float[] { 0f, 0.5f, 1.0f, 2.0f, 3.0f };
            s_minBottomOreDensityByLevel  = new float[] { 0f, 0.10f, 0.25f, 0.50f, 0.75f };
            s_minOrePurityByLevel         = new float[] { 0f, 0.10f, 0.25f, 0.50f, 0.75f };
            s_minComponentSizeByLevel     = new int[] { 0, 3, 8, 20, 40 };
        }

        internal static bool TrySetMinOreHeightForLevel(int level, float value)
        {
            if (level < 0 || level >= s_minOreHeightByLevel.Length) return false;
            s_minOreHeightByLevel[level] = value;
            return true;
        }

        internal static bool TrySetMinBottomOreDensityForLevel(int level, float value)
        {
            if (level < 0 || level >= s_minBottomOreDensityByLevel.Length) return false;
            s_minBottomOreDensityByLevel[level] = Math.Max(0f, Math.Min(1f, value));
            return true;
        }

        internal static bool TrySetMinOrePurityForLevel(int level, float value)
        {
            if (level < 0 || level >= s_minOrePurityByLevel.Length) return false;
            s_minOrePurityByLevel[level] = Math.Max(0f, Math.Min(1f, value));
            return true;
        }

        internal static bool TrySetMinComponentSizeForLevel(int level, int value)
        {
            if (level < 0 || level >= s_minComponentSizeByLevel.Length) return false;
            s_minComponentSizeByLevel[level] = Math.Max(0, value);
            return true;
        }

        internal static int PurityLevelCount => s_minOreHeightByLevel.Length;

        internal static string FormatPurityArrays()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("  Purity arrays (index = level 0-4):");
            sb.Append("    minOreHeight        = [");
            for (int i = 0; i < s_minOreHeightByLevel.Length; i++)
                sb.Append((i > 0 ? ", " : "") + s_minOreHeightByLevel[i].ToString("G", System.Globalization.CultureInfo.InvariantCulture));
            sb.AppendLine("]");
            sb.Append("    minBottomOreDensity = [");
            for (int i = 0; i < s_minBottomOreDensityByLevel.Length; i++)
                sb.Append((i > 0 ? ", " : "") + s_minBottomOreDensityByLevel[i].ToString("G", System.Globalization.CultureInfo.InvariantCulture));
            sb.AppendLine("]");
            sb.Append("    minOrePurity        = [");
            for (int i = 0; i < s_minOrePurityByLevel.Length; i++)
                sb.Append((i > 0 ? ", " : "") + s_minOrePurityByLevel[i].ToString("G", System.Globalization.CultureInfo.InvariantCulture));
            sb.AppendLine("]");
            sb.Append("    minComponentSize    = [");
            for (int i = 0; i < s_minComponentSizeByLevel.Length; i++)
                sb.Append((i > 0 ? ", " : "") + s_minComponentSizeByLevel[i]);
            sb.AppendLine("]");
            return sb.ToString();
        }

        private static void LoadSettingsFromJson()
        {
            s_settingsLoadAttempted = true;

            try
            {
                string? settingsPath = ResolveSettingsPath();
                if (string.IsNullOrWhiteSpace(settingsPath) || !File.Exists(settingsPath))
                {
                    s_loadedSettingsPath = null;
                    Log.Warning("[AFD] settings.json not found next to mod assembly or parent mod folder; using built-in defaults.");
                    return;
                }

                string json = File.ReadAllText(settingsPath);
                ParseSettingsJson(json);
                s_loadedSettingsPath = settingsPath;
            }
            catch (Exception ex)
            {
                s_loadedSettingsPath = null;
                Log.Warning($"[AFD] Failed to load settings.json: {ex.Message}");
            }
        }

        private static string? ResolveSettingsPath()
        {
            var rootDirs = new List<string>();

            try
            {
                TryAddCandidateRoot(rootDirs, s_modRootDirectoryPath);
            }
            catch
            {
            }

            try
            {
                TryAddCandidateRoot(rootDirs, typeof(AutoDepthDesignation).Assembly.Location);
            }
            catch
            {
            }

            try
            {
                string? codeBase = typeof(AutoDepthDesignation).Assembly.CodeBase;
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

            try
            {
                TryAddCandidateRoot(rootDirs, AppDomain.CurrentDomain.BaseDirectory);
            }
            catch
            {
            }

            try
            {
                TryAddCandidateRoot(rootDirs, Directory.GetCurrentDirectory());
            }
            catch
            {
            }

            foreach (string root in rootDirs)
            {
                // Prefer directories that look like an actual mod root (manifest + settings),
                // but still allow direct sibling settings.json next to a loaded DLL path.
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
                        candidateSettings = Path.Combine(dir.FullName, "settings.json");
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
                if (Directory.Exists(fullPath))
                {
                    directory = fullPath;
                }
                else
                {
                    directory = Path.GetDirectoryName(fullPath);
                }
            }
            catch
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            if (!roots.Contains(directory))
            {
                roots.Add(directory);
            }
        }

        private static void ParseSettingsJson(string json)
        {
            // Simple JSON parser for our specific structure
            try
            {
                // Extract purityLevels object
                int start = json.IndexOf("\"purityLevels\":");
                if (start >= 0)
                {
                    start = json.IndexOf('{', start);
                    int depth = 0, end = start;
                    for (int i = start; i < json.Length; i++)
                    {
                        if (json[i] == '{') depth++;
                        else if (json[i] == '}') depth--;
                        if (depth == 0) { end = i + 1; break; }
                    }

                    string purityObj = json.Substring(start, end - start);

                    // Parse each array
                    s_minOreHeightByLevel = ParseFloatArray(purityObj, "minOreHeightByLevel") ?? s_minOreHeightByLevel;
                    s_minBottomOreDensityByLevel = ParseFloatArray(purityObj, "minBottomOreDensityByLevel") ?? s_minBottomOreDensityByLevel;
                    s_minOrePurityByLevel = ParseFloatArray(purityObj, "minOrePurityRatioByLevel") ?? s_minOrePurityByLevel;
                    s_minComponentSizeByLevel = ParseIntArray(purityObj, "minComponentSizeByLevel") ?? s_minComponentSizeByLevel;
                }

                // Top-level scalar settings
                s_batchSize = ClampBatchSize(ParseInt(json, "batchSize") ?? s_batchSize);
                int? slopeDefault = ParseInt(json, "maxSlopeHeightDiff");
                if (slopeDefault.HasValue)
                    AutoForestryDesignationsMod.SetMaxHeightDiff(slopeDefault.Value);

                int? rampWidth = ParseInt(json, "rampWidth");
                if (rampWidth.HasValue)
                    AutoForestryDesignationsMod.SetRampWidth(rampWidth.Value);

                int? maxLayers = ParseInt(json, "maxLayersToExcavate");
                if (maxLayers.HasValue)
                    AutoForestryDesignationsMod.SetMaxLayersToExcavate(maxLayers.Value);

                var (foundDepth, depthVal) = TryParseNullableInt(json, "maxDepthToDigTo");
                if (foundDepth)
                    AutoForestryDesignationsMod.SetMaxDepthToDigTo(depthVal);

                int? purityLevel = ParseInt(json, "orePurityLevel");
                if (purityLevel.HasValue)
                    AutoForestryDesignationsMod.SetOrePurityLevel(purityLevel.Value);

                int? corridorClearance = ParseInt(json, "minCorridorClearance");
                if (corridorClearance.HasValue)
                    AutoForestryDesignationsMod.SetMinCorridorClearance(corridorClearance.Value);
            }
            catch (Exception ex)
            {
                Log.Warning($"[AFD] Error parsing settings.json: {ex.Message}");
            }
        }

        private static float[]? ParseFloatArray(string json, string key)
        {
            try
            {
                int idx = json.IndexOf($"\"{key}\":");
                if (idx < 0) return null;
                idx = json.IndexOf('[', idx);
                int end = json.IndexOf(']', idx);
                if (idx < 0 || end < 0) return null;

                string arrayStr = json.Substring(idx + 1, end - idx - 1);
                var parts = arrayStr.Split(',');
                var result = new float[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                        return null;
                    result[i] = val;
                }
                return result;
            }
            catch { return null; }
        }

        private static int[]? ParseIntArray(string json, string key)
        {
            try
            {
                int idx = json.IndexOf($"\"{key}\":");
                if (idx < 0) return null;
                idx = json.IndexOf('[', idx);
                int end = json.IndexOf(']', idx);
                if (idx < 0 || end < 0) return null;

                string arrayStr = json.Substring(idx + 1, end - idx - 1);
                var parts = arrayStr.Split(',');
                var result = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
                        return null;
                    result[i] = val;
                }
                return result;
            }
            catch { return null; }
        }

        private static int? ParseInt(string json, string key)
        {
            try
            {
                int idx = json.IndexOf($"\"{key}\":");
                if (idx < 0) return null;
                // Skip past the colon and whitespace
                int valStart = idx + key.Length + 3;
                while (valStart < json.Length && (json[valStart] == ' ' || json[valStart] == '\t')) valStart++;
                int valEnd = valStart;
                while (valEnd < json.Length && (char.IsDigit(json[valEnd]) || json[valEnd] == '-')) valEnd++;
                if (valEnd == valStart) return null;
                if (int.TryParse(json.Substring(valStart, valEnd - valStart), out int result))
                    return result;
                return null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Parses a nullable int value from JSON. Returns (true, value) if the key exists
        /// (value is null when the JSON value is literally null), or (false, null) if the key
        /// is absent.
        /// </summary>
        private static (bool found, int? value) TryParseNullableInt(string json, string key)
        {
            try
            {
                int idx = json.IndexOf($"\"{key}\":");
                if (idx < 0) return (false, null);
                int valStart = json.IndexOf(':', idx) + 1;
                while (valStart < json.Length && (json[valStart] == ' ' || json[valStart] == '\t' || json[valStart] == '\r' || json[valStart] == '\n')) valStart++;
                if (valStart + 4 <= json.Length && json.Substring(valStart, 4) == "null")
                    return (true, null);
                int valEnd = valStart;
                while (valEnd < json.Length && (char.IsDigit(json[valEnd]) || json[valEnd] == '-')) valEnd++;
                if (valEnd > valStart && int.TryParse(json.Substring(valStart, valEnd - valStart), out int val))
                    return (true, val);
                return (false, null);
            }
            catch { return (false, null); }
        }
    }
}
