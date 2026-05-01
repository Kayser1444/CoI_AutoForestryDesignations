// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
// Auto Forestry Designations - In-Game Console Commands
using System.Text;
using Mafi;
using Mafi.Core.Console;

namespace AutoForestryDesignations;

/// <summary>
/// Registers AFD console commands. Automatically discovered via [GlobalDependency] scanning.
/// Command names are derived from method names using camelCase tokenization (e.g. atdSetRampWidth -> atd_set_ramp_width).
/// </summary>
[GlobalDependency(RegistrationMode.AsSelf, false, false)]
public sealed class AtdConsoleCommands
{
    [ConsoleCommand(false, false, "Prints all current AFD global settings.", null)]
    private string atdGetSettings()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[AFD] Current settings:");
        sb.AppendLine($"  MaxHeightDiff         = {AutoForestryDesignationsMod.MaxHeightDiff}");
        sb.AppendLine($"  RampWidth             = {AutoForestryDesignationsMod.RampWidth}");
        sb.AppendLine($"  MaxLayersToExcavate   = {AutoForestryDesignationsMod.MaxLayersToExcavate}");
        sb.AppendLine($"  MaxDepthToDigTo       = {AutoForestryDesignationsMod.MaxDepthToDigTo?.ToString() ?? "-"}");
        sb.AppendLine($"  OrePurityLevel        = {AutoForestryDesignationsMod.OrePurityLevel}");
        sb.AppendLine($"  MinCorridorClearance  = {AutoForestryDesignationsMod.MinCorridorClearance}");
        sb.Append(AutoDepthDesignation.FormatPurityArrays());
        return sb.ToString();
    }

    [ConsoleCommand(false, false, "Sets the global default max height diff (1-3).", null)]
    private string atdSetMaxHeightDiff(int value)
    {
        AutoForestryDesignationsMod.SetMaxHeightDiff(value);
        return $"[AFD] MaxHeightDiff set to {AutoForestryDesignationsMod.MaxHeightDiff}.";
    }

    [ConsoleCommand(false, false, "Sets the global default ramp width (0-5). 0 disables ramp generation.", null)]
    private string atdSetRampWidth(int value)
    {
        AutoForestryDesignationsMod.SetRampWidth(value);
        return $"[AFD] RampWidth set to {AutoForestryDesignationsMod.RampWidth}.";
    }

    [ConsoleCommand(false, false, "Sets the global default max layers to excavate from the surface. 0 = no limit.", null)]
    private string atdSetMaxLayersToExcavate(int value)
    {
        AutoForestryDesignationsMod.SetMaxLayersToExcavate(value);
        return $"[AFD] MaxLayersToExcavate set to {AutoForestryDesignationsMod.MaxLayersToExcavate}.";
    }

    [ConsoleCommand(false, false, "Sets the global default ore purity level (0=Off, 1=Low, 2=Medium, 3=High, 4=Max).", null)]
    private string atdSetOrePurityLevel(int value)
    {
        AutoForestryDesignationsMod.SetOrePurityLevel(value);
        return $"[AFD] OrePurityLevel set to {AutoForestryDesignationsMod.OrePurityLevel}.";
    }

    [ConsoleCommand(false, false, "Sets the global default max depth to dig to (absolute elevation). Use '-' for no limit.", null)]
    private string atdSetMaxDepthToDigTo(string value)
    {
        if (value == "-")
        {
            AutoForestryDesignationsMod.SetMaxDepthToDigTo(null);
            return "[AFD] MaxDepthToDigTo set to no limit.";
        }
        if (int.TryParse(value, out int parsed))
        {
            AutoForestryDesignationsMod.SetMaxDepthToDigTo(parsed);
            return $"[AFD] MaxDepthToDigTo set to {AutoForestryDesignationsMod.MaxDepthToDigTo}.";
        }
        return $"[AFD] Invalid value '{value}'. Use an integer elevation or '-' for no limit.";
    }

    [ConsoleCommand(false, false, "Sets minOreHeight for a purity level (0-4). E.g. atd_set_min_ore_height 2 1.0", null)]
    private string atdSetMinOreHeight(int level, float value)
    {
        if (!AutoDepthDesignation.TrySetMinOreHeightForLevel(level, value))
            return $"[AFD] Level {level} out of range (0-{AutoDepthDesignation.PurityLevelCount - 1}).";
        return $"[AFD] minOreHeight[{level}] set to {value}.";
    }

    [ConsoleCommand(false, false, "Sets the global default corridor clearance (0=none, 1=small+med vehicles, 2=mega vehicles). Per-tower override available in the mine tower inspector.", null)]
    private string atdSetMinCorridorClearance(int value)
    {
        AutoForestryDesignationsMod.SetMinCorridorClearance(value);
        return $"[AFD] MinCorridorClearance set to {AutoForestryDesignationsMod.MinCorridorClearance}.";
    }

    [ConsoleCommand(false, false, "Sets minBottomOreDensity for a purity level (0-4), clamped 0-1. Minimum ore/(ore+waste) ratio a zone must have to be included. E.g. atd_set_min_bottom_ore_density 2 0.25", null)]
    private string atdSetMinBottomOreDensity(int level, float value)
    {
        if (!AutoDepthDesignation.TrySetMinBottomOreDensityForLevel(level, value))
            return $"[AFD] Level {level} out of range (0-{AutoDepthDesignation.PurityLevelCount - 1}).";
        return $"[AFD] minBottomOreDensity[{level}] set to {value}.";
    }

    [ConsoleCommand(false, false, "Sets minOrePurity ratio for a purity level (0-4), clamped 0-1. E.g. atd_set_min_ore_purity 2 0.25", null)]
    private string atdSetMinOrePurity(int level, float value)
    {
        if (!AutoDepthDesignation.TrySetMinOrePurityForLevel(level, value))
            return $"[AFD] Level {level} out of range (0-{AutoDepthDesignation.PurityLevelCount - 1}).";
        return $"[AFD] minOrePurity[{level}] set to {value}.";
    }

    [ConsoleCommand(false, false, "Sets minComponentSize for a purity level (0-4). E.g. atd_set_min_component_size 2 8", null)]
    private string atdSetMinComponentSize(int level, int value)
    {
        if (!AutoDepthDesignation.TrySetMinComponentSizeForLevel(level, value))
            return $"[AFD] Level {level} out of range (0-{AutoDepthDesignation.PurityLevelCount - 1}).";
        return $"[AFD] minComponentSize[{level}] set to {value}.";
    }
}
