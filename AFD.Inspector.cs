// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
// Auto Forestry Designations - Mine Tower Inspector Patching
using System;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Core.Buildings.Towers;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using UnityEngine;

namespace AutoForestryDesignations
{
    public static partial class AutoDepthDesignation
    {
        public static void Apply(Harmony harmony)
        {
            try
            {
                LogDebug("[AutoDepth] Apply() called");
                
                var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;
                var inspectorType = assembly.GetType("Mafi.Unity.Ui.Inspectors.ForestryTowerInspector");
                if (inspectorType == null)
                {
                    Log.Warning("[AutoDepth] ForestryTowerInspector type not found");
                    return;
                }

                LogDebug("[AutoDepth] Found ForestryTowerInspector type");

                var ctors = inspectorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                LogDebug($"[AutoDepth] Found {ctors.Length} constructors");
                
                if (ctors.Length > 0)
                {
                    harmony.Patch(ctors[0],
                        postfix: new HarmonyMethod(typeof(AutoDepthDesignation), nameof(InspectorCtorPostfix)));
                    LogDebug("[AutoDepth] Patched first constructor");
                }

                // Patch OnActivated() on ForestryTowerInspector (DeclaredOnly — safe, does not affect
                // other inspector types). Only resets the status panel to its prompt;
                // no scan is triggered so there are no timing issues.
                try
                {
                    var onActivatedMethod = inspectorType.GetMethod("OnActivated",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (onActivatedMethod != null)
                        harmony.Patch(onActivatedMethod,
                            postfix: new HarmonyMethod(typeof(AutoDepthDesignation), nameof(InspectorActivatePostfix)));
                }
                catch (Exception ex2)
                {
                    Log.Warning($"[AutoDepth] EXCEPTION patching OnActivated: {ex2}");
                }

            }
            catch (Exception ex)
            { 
                Log.Warning($"AutoDepth.Apply EXCEPTION: {ex}");
            }
        }

        public static void InspectorActivatePostfix(object __instance)
        {
            DesignationPanel.RefreshDisplays(__instance);
        }

        public static void InspectorCtorPostfix(object __instance)
        {
            try
            {
                if (!s_settingsLoadAttempted)
                    LoadSettingsFromJson();

                LogDebug("[AutoDepth] InspectorCtorPostfix called");

                var inspectorType = __instance.GetType();
                var baseType = inspectorType;
                PropertyInfo? entityProp = null;

                while (baseType != null)
                {
                    if (entityProp == null)
                        entityProp = baseType.GetProperty("Entity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    baseType = baseType.BaseType;
                }

                if (entityProp == null)
                {
                    Log.Warning("[AutoDepth] Entity property not found on inspector");
                    return;
                }

                var inspector = __instance;
                Func<IAreaManagingTower?> getTower = () => entityProp.GetValue(inspector) as IAreaManagingTower;

                var atdPanel = DesignationPanel.Build(getTower, inspector);

                FieldInfo? mainBodyField = null;
                var searchType = inspectorType;
                while (searchType != null && mainBodyField == null)
                {
                    mainBodyField = searchType.GetField("MainBody", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    searchType = searchType.BaseType;
                }

                if (mainBodyField != null)
                {
                    var mainBody = mainBodyField.GetValue(__instance) as Column;
                    if (mainBody != null)
                    {
                        mainBody.InsertAt(0, atdPanel);
                        mainBody.Show();
                        LogDebug("[AutoDepth] AFD panel and Ore Composition panel inserted");
                    }
                    else
                    {
                        Log.Warning("[AutoDepth] MainBody field is not a Column");
                    }
                }
                else
                {
                    Log.Warning("[AutoDepth] MainBody field not found");
                }
            }
            catch (Exception ex) { Debug.Log($"AutoDepth InspectorCtorPostfix EXCEPTION: {ex}"); }
        }
    }
}
