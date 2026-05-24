// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
// Auto Forestry Designations - In-Game Console Commands
using System.Text;
using Mafi;
using Mafi.Core.Console;

namespace AutoForestryDesignations;

/// <summary>
/// Registers AFD console commands. Automatically discovered via [GlobalDependency] scanning.
/// Command names are derived from method names using camelCase tokenization (e.g. afdSetMaxTiles -> afd_set_max_tiles).
/// </summary>
[GlobalDependency(RegistrationMode.AsSelf, false, false)]
public sealed class AfdConsoleCommands
{
    [ConsoleCommand(false, false, "Prints all current AFD global settings.", null)]
    private string afdGetSettings()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[AFD] Current settings:");
        sb.AppendLine($"  OnlyFertileTiles         = {AutoForestryDesignationsMod.OnlyFertileTiles}");
        sb.AppendLine($"  AvoidTilesWithTrees      = {AutoForestryDesignationsMod.AvoidTilesWithTrees}");
        sb.AppendLine($"  AvoidMiningDesignations  = {AutoForestryDesignationsMod.AvoidMiningDesignations}");
        sb.AppendLine($"  OnlyReachableTiles       = {AutoForestryDesignationsMod.OnlyReachableTiles}");
        sb.AppendLine($"  PathabilityTargetSize    = {AutoForestryDesignationsMod.PathabilityTargetSize} (n*n, clamped 1..9)");
        sb.AppendLine($"  MaxTiles                 = {AutoForestryDesignationsMod.MaxTiles} (0 = no limit)");
        sb.AppendLine($"  MarkHarvestReadyForHarvest = {AutoForestryDesignationsMod.MarkHarvestReadyForHarvest}");
        sb.AppendLine($"  ForestryPanelCollapsed   = {AutoForestryDesignationsMod.ForestryDesignationsPanelCollapsed}");
        sb.Append(    $"  ForestryInfoPanelCollapsed = {AutoForestryDesignationsMod.ForestryInformationPanelCollapsed}");
        return sb.ToString();
    }

    [ConsoleCommand(false, false, "Sets whether fertile-only tiles are targeted (true/false).", null)]
    private string afdSetOnlyFertileTiles(bool value)
    {
        AutoForestryDesignationsMod.SetOnlyFertileTiles(value);
        return $"[AFD] OnlyFertileTiles set to {AutoForestryDesignationsMod.OnlyFertileTiles}.";
    }

    [ConsoleCommand(false, false, "Sets whether tiles that already have a tree are skipped (true/false).", null)]
    private string afdSetAvoidTrees(bool value)
    {
        AutoForestryDesignationsMod.SetAvoidTilesWithTrees(value);
        return $"[AFD] AvoidTilesWithTrees set to {AutoForestryDesignationsMod.AvoidTilesWithTrees}.";
    }

    [ConsoleCommand(false, false, "Sets whether existing terrain designations are skipped (true/false).", null)]
    private string afdSetAvoidMiningDesignations(bool value)
    {
        AutoForestryDesignationsMod.SetAvoidMiningDesignations(value);
        return $"[AFD] AvoidMiningDesignations set to {AutoForestryDesignationsMod.AvoidMiningDesignations}.";
    }

    [ConsoleCommand(false, false, "Sets whether unreachable tiles are skipped by vehicle pathability (true/false).", null)]
    private string afdSetOnlyReachableTiles(bool value)
    {
        AutoForestryDesignationsMod.SetOnlyReachableTiles(value);
        return $"[AFD] OnlyReachableTiles set to {AutoForestryDesignationsMod.OnlyReachableTiles}.";
    }

    [ConsoleCommand(false, false, "Sets the global default max designation tiles per run (0 = no limit).", null)]
    private string afdSetMaxTiles(int value)
    {
        AutoForestryDesignationsMod.SetMaxTiles(value);
        return $"[AFD] MaxTiles set to {AutoForestryDesignationsMod.MaxTiles}.";
    }

    [ConsoleCommand(false, false, "Sets hidden reachability target square size n (n*n, clamped 1..9).", null)]
    private string afdSetPathabilityTargetSize(int value)
    {
        AutoForestryDesignationsMod.SetPathabilityTargetSize(value);
        return $"[AFD] PathabilityTargetSize set to {AutoForestryDesignationsMod.PathabilityTargetSize} (n*n).";
    }

    [ConsoleCommand(false, false, "Sets whether harvest-ready trees in the area are marked for harvest after scanning (true/false).", null)]
    private string afdSetMarkReadyForHarvest(bool value)
    {
        AutoForestryDesignationsMod.SetMarkHarvestReadyForHarvest(value);
        return $"[AFD] MarkHarvestReadyForHarvest set to {AutoForestryDesignationsMod.MarkHarvestReadyForHarvest}.";
    }

    [ConsoleCommand(false, false, "Sets whether the Forestry designations panel starts collapsed by default (true/false).", null)]
    private string afdSetForestryDesignationsPanelCollapsed(bool value)
    {
        AutoForestryDesignationsMod.SetForestryDesignationsPanelCollapsed(value);
        return $"[AFD] ForestryDesignationsPanelCollapsed set to {AutoForestryDesignationsMod.ForestryDesignationsPanelCollapsed}.";
    }

    [ConsoleCommand(false, false, "Sets whether the Forestry information panel starts collapsed by default (true/false).", null)]
    private string afdSetForestryInformationPanelCollapsed(bool value)
    {
        AutoForestryDesignationsMod.SetForestryInformationPanelCollapsed(value);
        return $"[AFD] ForestryInformationPanelCollapsed set to {AutoForestryDesignationsMod.ForestryInformationPanelCollapsed}.";
    }

    [ConsoleCommand(false, false, "Saves current AFD global settings to AFDsettings.json in the mod folder.", null)]
    private string afdSaveSettings()
    {
        if (AutoForestryDesignation.TrySaveSettings(out string path))
            return $"[AFD] Settings saved to: {path}";
        return "[AFD] Failed to save settings. Check the log for details.";
    }
}
