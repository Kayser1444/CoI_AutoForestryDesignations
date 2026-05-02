// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
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
        sb.AppendLine($"  AvoidInfertileTiles      = {AutoForestryDesignationsMod.AvoidInfertileTiles}");
        sb.AppendLine($"  AvoidTilesWithTrees      = {AutoForestryDesignationsMod.AvoidTilesWithTrees}");
        sb.AppendLine($"  AvoidMiningDesignations  = {AutoForestryDesignationsMod.AvoidMiningDesignations}");
        sb.AppendLine($"  MaxTiles                 = {AutoForestryDesignationsMod.MaxTiles} (0 = no limit)");
        sb.Append(    $"  MarkFullyGrownForHarvest = {AutoForestryDesignationsMod.MarkFullyGrownForHarvest}");
        return sb.ToString();
    }

    [ConsoleCommand(false, false, "Sets whether fertile-only tiles are targeted (true/false).", null)]
    private string afdSetAvoidInfertile(bool value)
    {
        AutoForestryDesignationsMod.SetAvoidInfertileTiles(value);
        return $"[AFD] AvoidInfertileTiles set to {AutoForestryDesignationsMod.AvoidInfertileTiles}.";
    }

    [ConsoleCommand(false, false, "Sets whether tiles that already have a tree are skipped (true/false).", null)]
    private string afdSetAvoidTrees(bool value)
    {
        AutoForestryDesignationsMod.SetAvoidTilesWithTrees(value);
        return $"[AFD] AvoidTilesWithTrees set to {AutoForestryDesignationsMod.AvoidTilesWithTrees}.";
    }

    [ConsoleCommand(false, false, "Sets whether existing mining and level designations are skipped (true/false).", null)]
    private string afdSetAvoidMiningDesignations(bool value)
    {
        AutoForestryDesignationsMod.SetAvoidMiningDesignations(value);
        return $"[AFD] AvoidMiningDesignations set to {AutoForestryDesignationsMod.AvoidMiningDesignations}.";
    }

    [ConsoleCommand(false, false, "Sets the global default max designation tiles per run (0 = no limit).", null)]
    private string afdSetMaxTiles(int value)
    {
        AutoForestryDesignationsMod.SetMaxTiles(value);
        return $"[AFD] MaxTiles set to {AutoForestryDesignationsMod.MaxTiles}.";
    }

    [ConsoleCommand(false, false, "Sets whether fully grown trees in the area are marked for harvest after scanning (true/false).", null)]
    private string afdSetMarkGrownForHarvest(bool value)
    {
        AutoForestryDesignationsMod.SetMarkFullyGrownForHarvest(value);
        return $"[AFD] MarkFullyGrownForHarvest set to {AutoForestryDesignationsMod.MarkFullyGrownForHarvest}.";
    }
}
