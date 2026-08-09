// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
// Auto Forestry Designations - Forestry Tower Inspector Patching
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Base;
using Mafi.Collections;
using Mafi.Localization;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.Syncers;
using Mafi.Core.Vehicles;
using Mafi.Core.Vehicles.Jobs;
using Mafi.Core.Vehicles.TreeHarvesters;
using Mafi.Core.Vehicles.TreePlanters;
using Mafi.Core.Vehicles.Trucks;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.Ui.Library.Inspectors;
using UnityEngine;

namespace AutoForestryDesignations
{
    public static partial class AutoForestryDesignation
    {
        public static void Apply(Harmony harmony)
        {
            try
            {
                LogDebug("Apply() called");
                ApplyLoadedPlanterParkingPatch(harmony);
                
                var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;
                var inspectorType = assembly.GetType("Mafi.Unity.Ui.Inspectors.ForestryTowerInspector");
                if (inspectorType == null)
                {
                    Log.Warning("[AFD] ForestryTowerInspector type not found");
                    return;
                }

                LogDebug("Found ForestryTowerInspector type");

                var ctors = inspectorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                LogDebug($"Found {ctors.Length} constructors");
                
                if (ctors.Length > 0)
                {
                    harmony.Patch(ctors[0],
                        postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(InspectorCtorPostfix)));
                    LogDebug("Patched first constructor");
                }

                // Patch OnActivated() on ForestryTowerInspector
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
                    var onDeactivatedMethod = inspectorType.GetMethod("OnDeactivated",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (onDeactivatedMethod != null)
                        harmony.Patch(onDeactivatedMethod,
                            postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(InspectorDeactivatePostfix)));
                }
                catch (Exception ex3)
                {
                    Log.Warning($"[AFD] EXCEPTION patching OnDeactivated: {ex3}");
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

                // Patch ForestryTower vehicle assignment methods
                try
                {
                    var canVehicleBeAssignedMethod = typeof(ForestryTower).GetMethod(nameof(ForestryTower.CanVehicleBeAssigned),
                        BindingFlags.Instance | BindingFlags.Public);
                    if (canVehicleBeAssignedMethod != null)
                        harmony.Patch(canVehicleBeAssignedMethod,
                            prefix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(ForestryTowerCanVehicleBeAssignedPrefix)));

                    var assignVehicleMethod = typeof(ForestryTower).GetMethod(nameof(ForestryTower.AssignVehicle),
                        BindingFlags.Instance | BindingFlags.Public);
                    if (assignVehicleMethod != null)
                        harmony.Patch(assignVehicleMethod,
                            postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(ForestryTowerAssignVehiclePostfix)));

                    var unassignVehicleMethod = typeof(ForestryTower).GetMethod(nameof(ForestryTower.UnassignVehicle),
                        BindingFlags.Instance | BindingFlags.Public);
                    if (unassignVehicleMethod != null)
                        harmony.Patch(unassignVehicleMethod,
                            postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(ForestryTowerUnassignVehiclePostfix)));

                    var updateAssignedVehiclesMethod = typeof(ForestryTower).GetMethod("updateAssignedVehicles",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (updateAssignedVehiclesMethod != null)
                        harmony.Patch(updateAssignedVehiclesMethod,
                            postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(ForestryTowerUpdateAssignedVehiclesPostfix)));

                    var onDestroyMethod = typeof(ForestryTower).GetMethod("OnDestroy",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (onDestroyMethod != null)
                        harmony.Patch(onDestroyMethod,
                            prefix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(ForestryTowerOnDestroyPrefix)));

                    var harvesterAssignVehicle = typeof(TreeHarvester).GetMethod(nameof(TreeHarvester.AssignVehicle),
                        BindingFlags.Instance | BindingFlags.Public);
                    if (harvesterAssignVehicle != null)
                        harmony.Patch(harvesterAssignVehicle,
                            postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(TreeHarvesterAssignVehiclePostfix)));

                    var harvesterUnassignVehicle = typeof(TreeHarvester).GetMethod(nameof(TreeHarvester.UnassignVehicle),
                        BindingFlags.Instance | BindingFlags.Public);
                    if (harvesterUnassignVehicle != null)
                        harmony.Patch(harvesterUnassignVehicle,
                            postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(TreeHarvesterUnassignVehiclePostfix)));


                    var forestryTowerOnEnabledChanged = typeof(ForestryTower).GetMethod("OnEnabledChanged",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (forestryTowerOnEnabledChanged != null)
                        harmony.Patch(forestryTowerOnEnabledChanged,
                            postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(ForestryTowerOnEnabledChangedPostfix)));

                    var vehicleOnEnabledChanged = typeof(Vehicle).GetMethod("OnEnabledChanged",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (vehicleOnEnabledChanged != null)
                        harmony.Patch(vehicleOnEnabledChanged,
                            postfix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(VehicleOnEnabledChangedPostfix)));
                }
                catch (Exception ex4)
                {
                    Log.Warning($"[AFD] EXCEPTION patching ForestryTower vehicle methods: {ex4}");
                }

            }
            catch (Exception ex)
            { 
                Log.Warning($"[AFD] Apply EXCEPTION: {ex}");
            }
        }

        private static void ApplyLoadedPlanterParkingPatch(Harmony harmony)
        {
            try
            {
                var parkingMethod = typeof(ParkAndWaitJobFactory).GetMethod(
                    nameof(ParkAndWaitJobFactory.TryEnqueueParkingJobIfNeeded),
                    BindingFlags.Instance | BindingFlags.Public);
                if (parkingMethod == null)
                {
                    Log.Warning("[AFD] ParkAndWaitJobFactory parking method not found");
                    return;
                }

                harmony.Patch(
                    parkingMethod,
                    prefix: new HarmonyMethod(typeof(AutoForestryDesignation), nameof(KeepLoadedPlanterOutOfTowerPrefix)));
                LogDebug("Patched loaded tree planter parking fallback");
            }
            catch (Exception ex)
            {
                Log.Warning($"[AFD] EXCEPTION patching loaded tree planter parking fallback: {ex}");
            }
        }

        public static bool KeepLoadedPlanterOutOfTowerPrefix(Vehicle vehicle, ref bool __result)
        {
            if (AutoForestryDesignationsMod.KeepLoadedPlantersInTheField
                && vehicle is TreePlanter planter
                && planter.IsNotEmpty
                && planter.ForestryTower.HasValue)
            {
                __result = false;
            LogDebug($"Keep loaded planters in the field invoked: prevented loaded tree planter {planter.Id} from returning to its forestry tower.");
                return false;
            }

            return true;
        }

        public static bool ForestryTowerCanVehicleBeAssignedPrefix(ForestryTower __instance, DynamicEntityProto vehicleProto, ref bool __result)
        {
            if (!GetTowerTruckPoolingEnabled(__instance)) return true;
            if (vehicleProto is TruckProto truckProto && s_protosDb != null)
            {
                bool isSupported = s_protosDb.All<TreeHarvesterProto>().Any(h => h.IsTruckSupported(truckProto));
                if (isSupported)
                {
                    __result = true;
                    return false; // Skip original method which returns false for TruckProto
                }
            }
            return true;
        }

        public static void ForestryTowerAssignVehiclePostfix(ForestryTower __instance, Vehicle vehicle, bool doNotCancelJobs)
        {
            if (vehicle is Truck truck)
            {
                TowerTruckAssignments.AssignTruckToTower(__instance, truck, s_entitiesManager);
                TowerTruckAssignments.UpdateAssignedVehiclesMethod?.Invoke(__instance, null);
            }
            else if (vehicle is TreeHarvester harvester && GetTowerTruckPoolingEnabled(__instance))
            {
                var harvesterTrucks = new List<Truck>();
                for (int i = 0; i < harvester.AllVehicles.Count; i++)
                {
                    if (harvester.AllVehicles[i] is Truck t)
                    {
                        harvesterTrucks.Add(t);
                    }
                }
                if (harvesterTrucks.Count > 0)
                {
                    TowerTruckAssignments.AssignTrucksToTower(__instance, harvesterTrucks, s_entitiesManager);
                }
                else
                {
                    TowerTruckAssignments.RebalanceTowerTrucks(__instance, s_entitiesManager);
                }
            }
        }

        public static void ForestryTowerUnassignVehiclePostfix(ForestryTower __instance, Vehicle vehicle, bool cancelJobs)
        {
            if (TowerTruckAssignments.IsRebalancing) return;
            if (vehicle is Truck truck)
            {
                bool playerInitiated = !__instance.IsDestroyed && TowerTruckAssignments.HasTrucksAssigned(__instance.Id);
                TowerTruckAssignments.UnassignTruckFromTower(__instance, truck, s_entitiesManager, playerInitiated: playerInitiated);
                TowerTruckAssignments.UpdateAssignedVehiclesMethod?.Invoke(__instance, null);
            }
            else if (vehicle is TreeHarvester harvester)
            {
                TowerTruckAssignments.OnHarvesterUnassignedFromTower(harvester, __instance, s_entitiesManager);
            }
        }

        public static void ForestryTowerUpdateAssignedVehiclesPostfix(ForestryTower __instance)
        {
            if (s_entitiesManager == null) return;
            var truckIds = TowerTruckAssignments.GetTruckIdsForTower(__instance.Id);
            if (truckIds.Count == 0) return;

            var m_allVehiclesField = typeof(ForestryTower).GetField("m_allVehicles", BindingFlags.Instance | BindingFlags.NonPublic);
            if (m_allVehiclesField?.GetValue(__instance) is Lyst<Vehicle> allVehicles)
            {
                foreach (var truckId in truckIds)
                {
                    if (s_entitiesManager.TryGetEntity<Truck>(truckId, out var truck) && !truck.IsDestroyed)
                    {
                        if (!allVehicles.Contains(truck))
                        {
                            allVehicles.Add(truck);
                        }
                    }
                }
            }
        }

        public static void ForestryTowerOnDestroyPrefix(ForestryTower __instance)
        {
            TowerTruckAssignments.OnTowerDestroyed(__instance.Id);
        }

        public static void TreeHarvesterAssignVehiclePostfix(TreeHarvester __instance, Vehicle vehicle)
        {
            if (vehicle is Truck truck && __instance.AssignedTo.ValueOrNull is ForestryTower tower)
            {
                if (GetTowerTruckPoolingEnabled(tower))
                {
                    TowerTruckAssignments.AssignTruckToTower(tower, truck, s_entitiesManager);
                }
            }
        }

        public static void TreeHarvesterUnassignVehiclePostfix(TreeHarvester __instance, Vehicle vehicle, bool cancelJobs)
        {
            if (TowerTruckAssignments.IsRebalancing) return;
            if (vehicle is Truck truck && __instance.AssignedTo.ValueOrNull is ForestryTower tower)
            {
                if (GetTowerTruckPoolingEnabled(tower))
                {
                    TowerTruckAssignments.UnassignTruckFromTower(tower, truck, s_entitiesManager, playerInitiated: true);
                }
            }
        }


        public static void ForestryTowerOnEnabledChangedPostfix(ForestryTower __instance)
        {
            TowerTruckAssignments.RebalanceTowerTrucks(__instance, s_entitiesManager);
        }

        public static void VehicleOnEnabledChangedPostfix(Vehicle __instance)
        {
            if (__instance is TreeHarvester harvester && harvester.AssignedTo.ValueOrNull is ForestryTower tower)
            {
                TowerTruckAssignments.RebalanceTowerTrucks(tower, s_entitiesManager);
            }
        }

        public static void InspectorActivatePostfix(object __instance)
        {
            DesignationPanel.RefreshDisplays(__instance);
            ForestryInfoPanel.RefreshContent(__instance);
        }

        public static void InspectorDeactivatePostfix(object __instance)
        {
            AutoForestryDesignation.DeactivateHarvestOverlay();
            ForestryInfoPanel.StopLiveTreeCount();
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

                LogDebug("InspectorCtorPostfix called");

                if (!(__instance is BaseInspector<ForestryTower> forestryInspector))
                {
                    return;
                }

                var panel = forestryInspector.AddVehicleAssigner<ForestryTower, TruckProto>(AfdLocalization.TruckPoolTitle, AfdLocalization.AssignedTrucksForestryTowerTooltip);
                forestryInspector.Observe(() => GetTowerTruckPoolingEnabled(forestryInspector.Entity))
                                 .Do(enabled => panel.Visible(enabled));

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
                        LogDebug("Forestry designations panel inserted");
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

        private static UiComponent? FindVehicleAssignerUi(UiComponent component)
        {
            if (component == null) return null;
            if (component.GetType().Name == "VehicleProtoAssignerUi")
            {
                return component;
            }
            for (int i = 0; i < component.ChildrenCount; i++)
            {
                var child = component.ChildAtOrDefault(i);
                if (child != null)
                {
                    var found = FindVehicleAssignerUi(child);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
