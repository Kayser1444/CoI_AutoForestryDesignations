// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using System;
using System.Globalization;
using System.IO;
using System.Text;
using CoI.AutoHelpers.Persistence;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Serialization;

namespace AutoForestryDesignations
{
    public static partial class AutoForestryDesignation
    {
        internal const string TowerSettingsConfigKey = "afdTowerSettingsStateJson";

        private const int TowerSettingsConfigSchemaVersion = 1;

        internal static void LoadTowerSettingsFromJsonStore(IModStateJsonStore store)
        {
            string json = store.LoadJson();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                if (TryApplyTowerSettingsStateJson(json, out int loadedCount))
                {
                    s_log.Info($"Persistence: loaded {loadedCount} tower setting record(s) from {store.StorageKind}.");
                }
            }
            catch (Exception ex)
            {
                s_log.Warning($"Persistence: failed to load tower settings from {store.StorageKind}: {ex.Message}");
            }
        }

        internal static void SaveTowerSettingsToJsonStore(IModStateJsonStore store)
        {
            string json = BuildTowerSettingsStateJsonForConfig();
            ModStateJsonSaveResult result = store.SaveJson(json);
            if (!result.Succeeded)
            {
                s_log.Warning($"Persistence: failed to update {result.StorageKind} value '{result.StateKey}': {result.ErrorMessage}");
                return;
            }

            s_log.Info($"Persistence: staged {s_towerSettingsByEntityId.Count} tower setting record(s) in {store.StorageKind}.");
        }

        internal static string BuildTowerSettingsStateJsonForConfig()
        {
            var sb = new StringBuilder();
            sb.Append("{\"schemaVersion\":");
            sb.Append(TowerSettingsConfigSchemaVersion.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"towerSettings\":[");

            bool first = true;
            foreach (var pair in s_towerSettingsByEntityId)
            {
                EntityId entityId = pair.Key;
                if (!entityId.IsValid)
                {
                    continue;
                }

                AFDTowerSettings settings = pair.Value;
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                sb.Append("{\"entityId\":");
                sb.Append(entityId.Value.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"onlyFertileTiles\":");
                AppendJsonBool(sb, settings.OnlyFertileTiles);
                sb.Append(",\"avoidTilesWithTrees\":");
                AppendJsonBool(sb, settings.AvoidTilesWithTrees);
                sb.Append(",\"avoidMiningDesignations\":");
                AppendJsonBool(sb, settings.AvoidMiningDesignations);
                sb.Append(",\"onlyReachableTiles\":");
                AppendJsonBool(sb, settings.OnlyReachableTiles);
                sb.Append(",\"maxTiles\":");
                sb.Append(settings.MaxTiles.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"markHarvestReadyForHarvest\":");
                AppendJsonBool(sb, settings.MarkHarvestReadyForHarvest);
                sb.Append(",\"forestryDesignationsPanelCollapsed\":");
                AppendJsonBool(sb, settings.ForestryDesignationsPanelCollapsed);
                sb.Append(",\"forestryInformationPanelCollapsed\":");
                AppendJsonBool(sb, settings.ForestryInformationPanelCollapsed);
                sb.Append('}');
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static bool TryApplyTowerSettingsStateJson(string json, out int loadedCount)
        {
            loadedCount = 0;
            object parsed = new JsonParser().Parse(new StringReader(json));
            if (!(parsed is Dict<string, object> root))
            {
                return false;
            }

            if (!TryGetInt(root, "schemaVersion", out int schemaVersion)
                || schemaVersion != TowerSettingsConfigSchemaVersion)
            {
                s_log.Warning($"Persistence: unsupported tower settings schema version '{schemaVersion}'.");
                return false;
            }

            if (!root.TryGetValue("towerSettings", out object rawEntries)
                || !(rawEntries is object[] entries))
            {
                return false;
            }

            s_towerSettingsByEntityId.Clear();
            foreach (object rawEntry in entries)
            {
                if (!(rawEntry is Dict<string, object> entry)
                    || !TryGetInt(entry, "entityId", out int entityIdValue)
                    || entityIdValue <= 0)
                {
                    continue;
                }

                var settings = AFDTowerSettings.FromGlobalDefaults();
                if (TryGetBool(entry, "onlyFertileTiles", out bool onlyFertileTiles))
                    settings.SetOnlyFertileTiles(onlyFertileTiles);
                if (TryGetBool(entry, "avoidTilesWithTrees", out bool avoidTilesWithTrees))
                    settings.SetAvoidTilesWithTrees(avoidTilesWithTrees);
                if (TryGetBool(entry, "avoidMiningDesignations", out bool avoidMiningDesignations))
                    settings.SetAvoidMiningDesignations(avoidMiningDesignations);
                if (TryGetBool(entry, "onlyReachableTiles", out bool onlyReachableTiles))
                    settings.SetOnlyReachableTiles(onlyReachableTiles);
                if (TryGetInt(entry, "maxTiles", out int maxTiles))
                    settings.SetMaxTiles(maxTiles);
                if (TryGetBool(entry, "markHarvestReadyForHarvest", out bool markHarvestReadyForHarvest))
                    settings.SetMarkHarvestReadyForHarvest(markHarvestReadyForHarvest);
                if (TryGetBool(entry, "forestryDesignationsPanelCollapsed", out bool forestryDesignationsPanelCollapsed))
                    settings.SetForestryDesignationsPanelCollapsed(forestryDesignationsPanelCollapsed);
                if (TryGetBool(entry, "forestryInformationPanelCollapsed", out bool forestryInformationPanelCollapsed))
                    settings.SetForestryInformationPanelCollapsed(forestryInformationPanelCollapsed);

                s_towerSettingsByEntityId[new EntityId(entityIdValue)] = settings;
                loadedCount++;
            }

            return true;
        }

        private static void AppendJsonBool(StringBuilder sb, bool value)
        {
            sb.Append(value ? "true" : "false");
        }

        private static bool TryGetBool(Dict<string, object> dict, string key, out bool value)
        {
            value = false;
            if (dict.TryGetValue(key, out object rawValue) && rawValue is bool boolValue)
            {
                value = boolValue;
                return true;
            }

            return false;
        }

        private static bool TryGetInt(Dict<string, object> dict, string key, out int value)
        {
            value = 0;
            if (dict.TryGetValue(key, out object rawValue))
            {
                if (rawValue is int intValue)
                {
                    value = intValue;
                    return true;
                }

                if (rawValue is double doubleValue)
                {
                    value = (int)doubleValue;
                    return Math.Abs(value - doubleValue) < 0.0001d;
                }
            }

            return false;
        }
    }
}
