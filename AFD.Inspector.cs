// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
// Auto Forestry Designations - Forestry Tower Inspector Patching
using System;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Buildings.Towers;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using UnityEngine;

namespace AutoForestryDesignations
{
    public static partial class AutoForestryDesignation
    {
        public static void Apply(Harmony harmony)
        {
            try
            {
                LogDebug("[AFD] Apply() called");
                
                var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;
                var inspectorType = assembly.GetType("Mafi.Unity.Ui.Inspectors.ForestryTowerInspector");
                if (inspectorType == null)
                {
                    Log.Warning("[AFD] ForestryTowerInspector type not found");
                    return;
                }

                LogDebug("[AFD] Found ForestryTowerInspector type");

                var ctors = inspectorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                LogDebug($"[AFD] Found {ctors.Length} constructors");
                
                if (ctors.Length > 0)
                {
                    harmony.Patch(ctors[0],
                        postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(InspectorCtorPostfix)));
                    LogDebug("[AFD] Patched first constructor");
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
                            postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(InspectorActivatePostfix)));
                }
                catch (Exception ex2)
                {
                    Log.Warning($"[AFD] EXCEPTION patching OnActivated: {ex2}");
                }

                try
                {
                    var setCutAtPercentageMethod = typeof(ForestryTower).GetMethod(nameof(ForestryTower.SetCutAtPercentage),
                        BindingFlags.Instance | BindingFlags.Public);
                    if (setCutAtPercentageMethod != null)
                        harmony.Patch(setCutAtPercentageMethod,
                            postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(ForestryTowerSetCutAtPercentagePostfix)));
                }
                catch (Exception ex3)
                {
                    Log.Warning($"[AFD] EXCEPTION patching SetCutAtPercentage: {ex3}");
                }

            }
            catch (Exception ex)
            { 
                Log.Warning($"[AFD] Apply EXCEPTION: {ex}");
            }
        }

        public static void InspectorActivatePostfix(object __instance)
        {
            DesignationPanel.RefreshDisplays(__instance);
            ForestryInfoPanel.RefreshContent(__instance);
        }

        public static void ForestryTowerSetCutAtPercentagePostfix(ForestryTower __instance)
        {
            AutoForestryDesignationsTicker.QueueForestryInfoRefresh(__instance);
        }

        public static void InspectorCtorPostfix(object __instance)
        {
            try
            {
                if (!s_settingsLoadAttempted)
                    LoadSettingsFromJson();

                LogDebug("[AFD] InspectorCtorPostfix called");

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
                    Log.Warning("[AFD] Entity property not found on inspector");
                    return;
                }

                var inspector = __instance;
                Func<IAreaManagingTower?> getTower = () => entityProp.GetValue(inspector) as IAreaManagingTower;

                var afdPanel = DesignationPanel.Build(getTower, inspector);
                var forestryInfoPanel = ForestryInfoPanel.Build(getTower, inspector);

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
                        mainBody.InsertAt(0, afdPanel);
                        mainBody.InsertAt(1, forestryInfoPanel);
                        mainBody.Show();
                        LogDebug("[AFD] Forestry Designations panel inserted");
                    }
                    else
                    {
                        Log.Warning("[AFD] MainBody field is not a Column");
                    }
                }
                else
                {
                    Log.Warning("[AFD] MainBody field not found");
                }
            }
            catch (Exception ex) { Debug.Log($"[AFD] InspectorCtorPostfix EXCEPTION: {ex}"); }
        }
    }
}
