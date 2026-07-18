// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mafi;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.Buildings.VehicleDepots;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Vehicles;
using Mafi.Core.Vehicles.TreeHarvesters;
using Mafi.Core.Vehicles.TreePlanters;
using Mafi.Core.Vehicles.Commands;
using Mafi.Collections;
using Mafi.Core.Input;
using Mafi.Core.PathFinding;
using Mafi.Core.Syncers;
using Mafi.Localization;
using Mafi.Unity;
using Mafi.Unity.InputControl;
using Mafi.Unity.Ui;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.UiToolkit.Library.FloatingPanel;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.Ui.Library.Inspectors;
using UnityEngine;
using EntityId = Mafi.Core.EntityId;

namespace AutoForestryDesignations
{
    public static class PreAllocationPatches
    {
        private static FloatingColumn? s_activePopup;
        private sealed class OwnedQueueDecoration { }
        private static readonly ConditionalWeakTable<UiComponent, OwnedQueueDecoration> s_ownedQueueDecorations =
            new ConditionalWeakTable<UiComponent, OwnedQueueDecoration>();

        private static void MarkDecorationOwned(UiComponent component)
        {
            s_ownedQueueDecorations.Remove(component);
            s_ownedQueueDecorations.Add(component, new OwnedQueueDecoration());
        }

        private static bool ReleaseOwnedDecoration(UiComponent component)
            => s_ownedQueueDecorations.Remove(component);

        public static void Apply(Harmony harmony)
        {
            try
            {
                AutoForestryDesignation.LogDebug("Applying pre-allocation patches...");

                var assembly = typeof(Mafi.Unity.Entities.EntityMb).Assembly;

                // Patch VehicleProtoAssignerUi constructor
                var assignerUiType = assembly.GetType("Mafi.Unity.Ui.Library.Inspectors.VehicleProtoAssignerUi");
                var assignerCtor = assignerUiType?.GetConstructor(new[] {
                    typeof(UiComponent),
                    typeof(DrivingEntityProto),
                    typeof(UiContext),
                    typeof(Func<IEntityAssignedWithVehicles>)
                });
                if (assignerCtor != null)
                {
                    harmony.Patch(assignerCtor,
                        postfix: new HarmonyMethod(typeof(PreAllocationPatches), nameof(VehicleProtoAssignerUi_Ctor_Postfix)));
                    AutoForestryDesignation.LogDebug("Patched VehicleProtoAssignerUi constructor");
                }
                else
                {
                    Log.Warning("[AFD] VehicleProtoAssignerUi constructor not found.");
                }

                // Patch concrete closed VehicleDepotInspector constructor
                var depotInspectorType = assembly.GetType("Mafi.Unity.Ui.Inspectors.VehicleDepotInspector");
                var ctor = depotInspectorType?.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();
                if (ctor != null)
                {
                    harmony.Patch(ctor,
                        postfix: new HarmonyMethod(typeof(PreAllocationPatches), nameof(VehicleDepotInspector_Ctor_Postfix)));
                    AutoForestryDesignation.LogDebug("Patched VehicleDepotInspector constructor");
                }
                else
                {
                    Log.Warning("[AFD] VehicleDepotInspector constructor not found.");
                }

                // Patch VehicleDepotBase.AddVehicleToBuildQueue
                var addQueueMethod = typeof(VehicleDepotBase).GetMethod("AddVehicleToBuildQueue",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (addQueueMethod != null)
                {
                    harmony.Patch(addQueueMethod,
                        postfix: new HarmonyMethod(typeof(PreAllocationPatches), nameof(VehicleDepotBase_AddVehicleToBuildQueue_Postfix)));
                    AutoForestryDesignation.LogDebug("Patched VehicleDepotBase.AddVehicleToBuildQueue");
                }

                // Patch VehicleDepotBase.TryBuildVehicle (both Prefix and Postfix with state variable to track build type)
                var tryBuildVehicleMethod = typeof(VehicleDepotBase).GetMethod("TryBuildVehicle",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (tryBuildVehicleMethod != null)
                {
                    harmony.Patch(tryBuildVehicleMethod,
                        prefix: new HarmonyMethod(typeof(PreAllocationPatches), nameof(VehicleDepotBase_TryBuildVehicle_Prefix)),
                        postfix: new HarmonyMethod(typeof(PreAllocationPatches), nameof(VehicleDepotBase_TryBuildVehicle_Postfix)));
                    AutoForestryDesignation.LogDebug("Patched VehicleDepotBase.TryBuildVehicle");
                }

                // Patch VehicleDepotBase.RemoveVehicleFromBuildOrReplaceQueue
                var removeQueueMethod = typeof(VehicleDepotBase).GetMethod("RemoveVehicleFromBuildOrReplaceQueue",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (removeQueueMethod != null)
                {
                    harmony.Patch(removeQueueMethod,
                        prefix: new HarmonyMethod(typeof(PreAllocationPatches), nameof(VehicleDepotBase_RemoveVehicleFromBuildOrReplaceQueue_Prefix)));
                    AutoForestryDesignation.LogDebug("Patched VehicleDepotBase.RemoveVehicleFromBuildOrReplaceQueue");
                }
            }
            catch (Exception ex)
            {
                Log.Error("[AFD] Exception applying pre-allocation patches: " + ex);
            }
        }



        // Postfix for VehicleProtoAssignerUi constructor
        public static void VehicleProtoAssignerUi_Ctor_Postfix(UiComponent __instance, DrivingEntityProto proto, UiContext context, Func<IEntityAssignedWithVehicles> entityProvider)
        {
            try
            {
                if (!(proto is TreeHarvesterProto) && !(proto is TreePlanterProto)) return;
                // Find the column inside row
                var col = __instance.AllChildren.OfType<Column>().FirstOrDefault();
                if (col == null) return;

                var buttons = col.AllChildren.OfType<ButtonIcon>().ToList();
                if (buttons.Count < 2) return;

                var plusBtn = buttons[0];

                // Retrieve original click action
                var mOnClickField = typeof(Mafi.Unity.UiToolkit.Library.Button).GetField("m_onClick", BindingFlags.Instance | BindingFlags.NonPublic);
                if (mOnClickField == null) return;

                var originalOnClick = ((Option<Action>)mOnClickField.GetValue(plusBtn)).ValueOrNull;

                // Bind click handler: assigns if assignable vehicles exist; pops confirmation to build new otherwise
                plusBtn.OnClick(() =>
                {
                    var entity = entityProvider();
                    var stats = context.VehiclesManager.GetStats(proto, entity.ZoneMask);
                    if (stats.Assignable > 0)
                    {
                        originalOnClick?.Invoke();
                    }
                    else
                    {
                        int buildCount = NotMappedShortcuts.GetBuildCount();
                        ShowEnqueueConfirmation(context, proto, entity, buildCount, plusBtn);
                    }
                }, true);

                // Register MouseUpEvent callback directly to capture Shift-Alt click (bypassing Clickable manipulator filters)
                plusBtn.ButtonElement.RegisterCallback<UnityEngine.UIElements.MouseUpEvent>(evt =>
                {
                    if (evt.button == 0 && evt.shiftKey && evt.altKey)
                    {
                        var entity = entityProvider();
                        EnqueueAtNearestDepot(context, proto, entity);
                        evt.StopPropagation();
                    }
                });

                var minusBtn = buttons[1];
                var originalOnMinusClick = ((Option<Action>)mOnClickField.GetValue(minusBtn)).ValueOrNull;

                // Bind click handler: unassigns if assigned vehicles exist; cancels enqueued orders otherwise
                minusBtn.OnClick(() =>
                {
                    var entity = entityProvider();
                    var assignedVehicles = entity.AllVehiclesWithProto(proto);
                    if (assignedVehicles.Count > 0)
                    {
                        originalOnMinusClick?.Invoke();
                    }
                    else
                    {
                        int cancelCount = NotMappedShortcuts.GetBuildCount();
                        CancelEnqueuedVehicles(context, proto, entity, cancelCount);
                    }
                }, true);

                // Register MouseUpEvent callback directly to capture Shift-Alt click (bypassing Clickable manipulator filters)
                minusBtn.ButtonElement.RegisterCallback<UnityEngine.UIElements.MouseUpEvent>(evt =>
                {
                    if (evt.button == 0 && evt.shiftKey && evt.altKey)
                    {
                        var entity = entityProvider();
                        CancelEnqueuedVehicles(context, proto, entity, 1);
                        evt.StopPropagation();
                    }
                });

                // Setup unified observer to update assigned display count and plus button enabled state/tooltip safely.
                // We observe everything the base game observes + our additions, so we always run last and overwrite correctly.
                var assignedDisplay = __instance.AllChildren.OfType<Mafi.Unity.Ui.Library.Display>().FirstOrDefault();
                if (assignedDisplay != null)
                {
                    __instance.ObserveIndexable(() => entityProvider().AllVehiclesWithProto(proto))
                        .Observe(() => context.VehiclesManager.GetStats(proto, entityProvider().ZoneMask))
                        .Observe(() => entityProvider().CanVehicleBeAssigned(proto))
                        .Observe(() => context.UnlockedProtosDbForUi.IsUnlocked(proto))
                        .Observe(() => PendingVehicleAllocations.GetQueuedCountForTower(entityProvider().Id, proto.Id))
                        .Do(delegate(Lyst<Vehicle> assignedVehicles, VehicleStats stats, bool canBeAssigned, bool isUnlocked, int queuedCount)
                        {
                            int assignedCount = assignedVehicles.Count;
                            if (queuedCount > 0)
                            {
                                assignedDisplay.SetValue(new LocStrFormatted($"{assignedCount} (+{queuedCount})"));
                                assignedDisplay.State(DisplayState.Important);
                            }
                            else
                            {
                                assignedDisplay.SetValue(new LocStrFormatted(assignedCount.ToString()));
                                assignedDisplay.State((assignedCount <= 0) ? DisplayState.Inactive : DisplayState.Important);
                            }
                            
                            bool enabled = stats.Assignable > 0 || (canBeAssigned && isUnlocked);
                            plusBtn.Enabled(enabled);
                            __instance.Visible(assignedCount > 0 || queuedCount > 0 || (canBeAssigned && isUnlocked));
                            
                            bool minusEnabled = assignedCount > 0 || queuedCount > 0;
                            minusBtn.Enabled(minusEnabled);
                        });
                }
            }
            catch (Exception ex)
            {
                Log.Error("[AFD] Error in VehicleProtoAssignerUi constructor postfix: " + ex);
            }
        }
        private static VehicleDepotBase? FindClosestDepot(UiContext context, DrivingEntityProto proto, IEntityAssignedWithVehicles tower)
        {
            var depots = context.EntitiesManager.GetAllEntitiesOfType<VehicleDepotBase>();
            VehicleDepotBase? closestDepot = null;
            float minDistanceSqr = float.MaxValue;
            int eligibleCount = 0;
            foreach (var depot in depots)
            {
                if (depot.CanWork && depot.Prototype.BuildableEntities.Contains(proto))
                {
                    eligibleCount++;
                    float distSqr = tower.Position2f.DistanceSqrTo(depot.Position2f).ToFloat();
                    if (distSqr < minDistanceSqr)
                    {
                        minDistanceSqr = distSqr;
                        closestDepot = depot;
                    }
                }
            }
            AutoForestryDesignation.s_log.Info($"Vehicle order depot selection: tower={tower.Id.Value} proto={proto.Id.Value} eligible={eligibleCount} method=StraightLine result={(closestDepot == null ? "None" : closestDepot.Id.Value.ToString())} distanceSqr={(closestDepot == null ? "n/a" : minDistanceSqr.ToString("F3"))}");
            return closestDepot;
        }

        private static void EnqueueAtNearestDepot(UiContext context, DrivingEntityProto proto, IEntityAssignedWithVehicles tower)
        {
            VehicleDepotBase? closestDepot = FindClosestDepot(context, proto, tower);
            if (tower.IsDestroyed) return;
            if (closestDepot != null)
            {
                context.InputScheduler.ScheduleInputCmd(new AddVehicleToBuildQueueCmd(proto, closestDepot, 1));
                PendingVehicleAllocations.Enqueue(closestDepot.Id, tower.Id, proto.Id);
                AutoForestryDesignation.PlayClickSound();
            }
            else
                PlayInvalidOpSound(context);
        }

        private static void ShowEnqueueConfirmation(UiContext context, DrivingEntityProto proto, IEntityAssignedWithVehicles tower, int count, Button plusBtn)
        {
            VehicleDepotBase? closestDepot = FindClosestDepot(context, proto, tower);

            if (closestDepot == null || tower.IsDestroyed)
            {
                PlayInvalidOpSound(context);
                return;
            }

            if (s_activePopup != null)
            {
                try { s_activePopup.Close(); } catch { }
                s_activePopup = null;
            }

            string vehicleDesc = $"<b>{proto.Strings.Name}</b>";
            string depotDesc = $"<b>{PendingVehicleAllocations.GetEntityDescription(closestDepot)}</b>";
            string towerDesc = $"<b>{PendingVehicleAllocations.GetEntityDescription(tower)}</b>";
            string promptText;
            if (count == 1)
            {
                promptText = string.Format(AfdLocalization.EnqueueConfirmPromptSingular.TranslatedString, vehicleDesc, depotDesc, towerDesc);
            }
            else
            {
                promptText = string.Format(AfdLocalization.EnqueueConfirmPromptPlural.TranslatedString, $"<b>{count}</b>", vehicleDesc, depotDesc, towerDesc);
            }

            var policy = new DropdownPositionPolicy();
            var popup = new FloatingColumn(policy, keepOpenOnHover: false, openAfterDelay: false, closeOnClickOutside: true);
            s_activePopup = popup;

            var panel = new Panel(noBolts: true)
                .ClassRoot(Cls.floater, Cls.interactive)
                .BrightText();

            var row = new Row();
            
            var leftCol = new Column { (Action<Column>)delegate(Column c) { c.Padding(2.pt()); } };
            leftCol.Add(new Label(new LocStrFormatted(promptText)).TextCenterTop().MarginBottom(2.pt()));
            
            var buttonsRow = new Row { (Action<Row>)delegate(Row r) { r.AlignItemsCenter().JustifyItemsCenter(); } }.AlignSelfCenter();

            var confirmBtn = new ButtonText(
                Mafi.Unity.UiToolkit.Library.Button.Primary,
                new LocStrFormatted(AfdLocalization.EnqueueConfirmBtnText.TranslatedString),
                delegate
                {
                    if (closestDepot.CanWork)
                    {
                        context.InputScheduler.ScheduleInputCmd(new AddVehicleToBuildQueueCmd(proto.Id, closestDepot.Id, count));
                        for (int i = 0; i < count; i++)
                        {
                            PendingVehicleAllocations.Enqueue(closestDepot.Id, tower.Id, proto.Id);
                        }
                        AutoForestryDesignation.PlayClickSound();
                        AutoForestryDesignation.LogDebug($"Queued {count} vehicle(s) {proto.Id.Value} at depot {closestDepot.Id.Value} for tower {tower.Id.Value}");
                    }
                    else
                    {
                        PlayInvalidOpSound(context);
                    }
                    popup.Close();
                }
            ).AlignSelfCenter();
            buttonsRow.Add(confirmBtn);

            var zoomBtn = new ButtonIcon(
                Mafi.Unity.UiToolkit.Library.Button.General,
                "Assets/Unity/UserInterface/Toolbar/MapPin.svg",
                delegate
                {
                    context.CameraController.PanTo(closestDepot.Position2f);
                }
            ).IconSize(16.px()).Padding(2.pt()).MarginLeft(6.pt());

            string zoomTooltip = string.Format(AfdLocalization.ZoomToDepotTooltip.TranslatedString, depotDesc);
            zoomBtn.Tooltip(new LocStrFormatted(zoomTooltip));
            buttonsRow.Add(zoomBtn);

            leftCol.Add(buttonsRow);

            var rightCol = new Column { (Action<Column>)delegate(Column c) { c.AlignSelfStretch().AlignItemsCenter().BorderLeft(2.px(), Theme.BorderColor).Background(Theme.BackgroundDark); } };
            var closeBtn = new ButtonIcon(
                Mafi.Unity.UiToolkit.Library.Button.IconOnly,
                "Assets/Unity/UserInterface/General/CloseThin.svg",
                delegate { popup.Close(); }
            ).IconSize(16.px()).FillRow().PaddingLeftRight(2.pt()).Fill();
            rightCol.Add(closeBtn);

            row.Add(leftCol);
            row.Add(rightCol);

            panel.BodyAdd(delegate(Column c) { c.Padding(0.pt()); }, row);
            popup.Add(panel);
            popup.MaxWidth(450.px());

            popup.OnCloseDone += delegate
            {
                if (s_activePopup == popup) s_activePopup = null;
            };

            popup.Open(plusBtn);
        }

        private static void CancelEnqueuedVehicles(UiContext context, DrivingEntityProto proto, IEntityAssignedWithVehicles tower, int cancelCount)
        {
            int cancelled = PendingVehicleAllocations.CancelPendingTickets(tower.Id, proto.Id, cancelCount);

            if (cancelled < cancelCount)
            {
                var enqueuedItems = PendingVehicleAllocations.GetEnqueuedItemsForTowerAndProto(tower.Id, proto.Id);
                for (int i = enqueuedItems.Count - 1; i >= 0 && cancelled < cancelCount; i--)
                {
                    var itemInfo = enqueuedItems[i];
                    if (context.EntitiesManager.TryGetEntity<VehicleDepotBase>(itemInfo.DepotId, out var depot) && !depot.IsDestroyed)
                    {
                        if (PendingVehicleAllocations.TryGetBuildIndexForItem(depot.Id, itemInfo.Item, out int buildIndex))
                        {
                            context.InputScheduler.ScheduleInputCmd(new RemoveVehicleFromBuildQueueCmd(buildIndex, depot));
                            itemInfo.Item.TowerId = EntityId.Invalid;
                            cancelled++;
                            AutoForestryDesignation.PlayClickSound();
                        }
                    }
                }
            }

            if (cancelled > 0)
            {
                AutoForestryDesignation.LogDebug($"Cancelled {cancelled} enqueued vehicle(s) for tower {tower.Id.Value}");
            }
            else
            {
                PlayInvalidOpSound(context);
            }
        }

        private static void PlayInvalidOpSound(UiContext context)
        {
            var invalidOpSoundField = typeof(UiRoot).GetField("InvalidOpSound", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var invalidOpSound = (AudioSource?)invalidOpSoundField?.GetValue(context.UiRoot);
            invalidOpSound?.Play();
        }

        private static void FindQueueItems(UiComponent parent, List<UiComponent> results)
        {
            foreach (var child in parent.AllChildren)
            {
                if (child.GetType().Name == "QueueItemUi")
                {
                    results.Add(child);
                }
                FindQueueItems(child, results);
            }
        }

        // Postfix for VehicleDepotInspector constructor to register tooltip updating observer
        public static void VehicleDepotInspector_Ctor_Postfix(UiComponent __instance)
        {
            try
            {
                var entityProp = __instance.GetType().GetProperty("Entity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (entityProp == null) return;

                var contextProp = __instance.GetType().GetProperty("Context", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (contextProp == null) return;

                var context = (UiContext)contextProp.GetValue(__instance);
                if (context == null) return;

                __instance.Observe(() => {
                        var d = (VehicleDepotBase)entityProp.GetValue(__instance);
                        return d?.ReplaceQueue.Count ?? 0;
                    })
                    .Observe(() => {
                        var d = (VehicleDepotBase)entityProp.GetValue(__instance);
                        return d?.BuildQueue.Count ?? 0;
                    })
                    .Observe(() => {
                        var d = (VehicleDepotBase)entityProp.GetValue(__instance);
                        return d == null ? 0 : PendingVehicleAllocations.GetQueuedCountForDepot(d.Id);
                    })
                    .Do(delegate(int replaceCount, int buildCount, int totalQueued)
                    {
                        var depot = (VehicleDepotBase)entityProp.GetValue(__instance);
                        if (depot == null) return;

                        var currentItems = new List<UiComponent>();
                        FindQueueItems(__instance, currentItems);

                        for (int i = 0; i < currentItems.Count; i++)
                        {
                            var queueItem = currentItems[i];
                            int buildIndex = i - replaceCount;

                            var existing = queueItem.ExistingTooltip.ValueOrNull;
                            if (existing != null && !(existing is SimpleTooltipPromise))
                            {
                                ((IUiComponent)queueItem).SetTooltip(Option<ITooltipPromise>.None);
                            }

                            if (buildIndex >= 0 && buildIndex < buildCount)
                            {
                                if (PendingVehicleAllocations.TryGetTowerForBuildIndex(context.EntitiesManager, depot.Id, buildIndex, out string towerDesc))
                                {
                                    string formattedTip = string.Format(AfdLocalization.PreAssignedTooltipFmt.TranslatedString, $"<b>{towerDesc}</b>");
                                    queueItem.Tooltip(new LocStrFormatted(formattedTip));
                                    MarkDecorationOwned(queueItem);

                                    // Set tooltip on the delete overlay button inside QueueItemUi so it's not hidden on hover
                                    var deleteOverlayField = queueItem.GetType().GetField("m_deleteOverlay", BindingFlags.Instance | BindingFlags.NonPublic);
                                    var deleteOverlay = (ButtonIcon?)deleteOverlayField?.GetValue(queueItem);
                                    if (deleteOverlay != null)
                                    {
                                        var existingDel = deleteOverlay.ExistingTooltip.ValueOrNull;
                                        if (existingDel != null && !(existingDel is SimpleTooltipPromise))
                                        {
                                            ((IUiComponent)deleteOverlay).SetTooltip(Option<ITooltipPromise>.None);
                                        }
                                        deleteOverlay.Tooltip(new LocStrFormatted(formattedTip));
                                        MarkDecorationOwned(deleteOverlay);
                                    }

                                    // Highlight pre-assigned items with a yellow/gold border (width 2px, radius 4)
                                    queueItem.Border(2.px(), Theme.PrimaryColor.SetA(120), 4);
                                }
                                else
                                {
                                    if (ReleaseOwnedDecoration(queueItem))
                                    {
                                        queueItem.Tooltip(LocStrFormatted.Empty);
                                        queueItem.Border(1.px(), ColorRgba.Empty, 4);
                                    }
                                }
                            }
                            else
                            {
                                if (ReleaseOwnedDecoration(queueItem))
                                {
                                    queueItem.Tooltip(LocStrFormatted.Empty);
                                    queueItem.Border(1.px(), ColorRgba.Empty, 4);
                                }
                            }
                        }
                    });

                // Observe construction progress to override the first building item border color
                __instance.Observe(() => {
                        var d = (VehicleDepotBase)entityProp.GetValue(__instance);
                        return d?.VehicleConstructionProgress ?? Option<Mafi.Core.Entities.Static.IConstructionProgress>.None;
                    })
                    .Do(delegate(Option<Mafi.Core.Entities.Static.IConstructionProgress> progress)
                    {
                        var depot = (VehicleDepotBase)entityProp.GetValue(__instance);
                        if (depot == null) return;

                        var currentItems = new List<UiComponent>();
                        FindQueueItems(__instance, currentItems);
                        var replaceCount = depot.ReplaceQueue.Count;
                        if (currentItems.Count > replaceCount)
                        {
                            var firstBuildItem = currentItems[replaceCount];
                            if (PendingVehicleAllocations.TryGetTowerForBuildIndex(context.EntitiesManager, depot.Id, 0, out string towerDesc))
                            {
                                firstBuildItem.Border(2.px(), Theme.PrimaryColor.SetA(120), 4);
                            }
                        }
                    });
            }
            catch (Exception ex)
            {
                Log.Error("[AFD] Error in VehicleDepotInspector constructor postfix: " + ex);
            }
        }

        // Postfix for VehicleDepotBase.AddVehicleToBuildQueue to transition pending tickets to depot matched queues
        public static void VehicleDepotBase_AddVehicleToBuildQueue_Postfix(VehicleDepotBase __instance, DrivingEntityProto vehicleProto, bool __result)
        {
            try
            {
                if (__result && vehicleProto != null)
                {
                    PendingVehicleAllocations.OnVehicleAddedToQueue(__instance.Id, vehicleProto.Id);
                }
                else if (!__result && vehicleProto != null)
                {
                    PendingVehicleAllocations.OnVehicleAddFailed(__instance.Id, vehicleProto.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[AFD] Error in VehicleDepotBase.AddVehicleToBuildQueue postfix: " + ex);
            }
        }

        // Prefix for VehicleDepotBase.TryBuildVehicle to capture BuildQueue count before dequeueing
        public static void VehicleDepotBase_TryBuildVehicle_Prefix(VehicleDepotBase __instance, out int __state)
        {
            __state = __instance.BuildQueue.Count;
        }

        // Postfix for VehicleDepotBase.TryBuildVehicle to assign the built vehicle to the pre-allocated tower
        public static void VehicleDepotBase_TryBuildVehicle_Postfix(VehicleDepotBase __instance, bool __result, ref Vehicle vehicle, int __state)
        {
            try
            {
                if (__result && vehicle != null && __instance.BuildQueue.Count < __state)
                {
                    if (PendingVehicleAllocations.TryDequeueCompleted(__instance.Id, vehicle.Prototype.Id, out var towerId))
                    {
                        var entitiesManager = __instance.Context.EntitiesManager;
                        if (entitiesManager.TryGetEntity<IEntityAssignedWithVehicles>(towerId, out var tower))
                        {
                            if (!tower.IsDestroyed && tower.CanVehicleBeAssigned(vehicle.Prototype))
                            {
                                tower.AssignVehicle(vehicle);
                                AutoForestryDesignation.s_log.Info($"Assigned newly built vehicle {vehicle.Id.Value} ({vehicle.Prototype.Id.Value}) to tower {tower.Id.Value}.");
                            }
                            else
                            {
                                AutoForestryDesignation.s_log.Warning($"Could not assign newly built vehicle {vehicle.Id.Value} ({vehicle.Prototype.Id.Value}) to tower {tower.Id.Value} (IsDestroyed: {tower.IsDestroyed}, CanBeAssigned: {tower.CanVehicleBeAssigned(vehicle.Prototype)}). Vehicle left unassigned.");
                            }
                        }
                        else
                        {
                            AutoForestryDesignation.s_log.Warning($"Could not find target tower {towerId.Value} for newly built vehicle {vehicle.Id.Value} ({vehicle.Prototype.Id.Value}). Vehicle left unassigned.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[AFD] Error in VehicleDepotBase.TryBuildVehicle postfix: " + ex);
            }
        }

        // Prefix for VehicleDepotBase.RemoveVehicleFromBuildOrReplaceQueue to remove pending allocation when order is cancelled
        public static void VehicleDepotBase_RemoveVehicleFromBuildOrReplaceQueue_Prefix(VehicleDepotBase __instance, int index)
        {
            try
            {
                int buildIndex = index - __instance.ReplaceQueue.Count;
                if (buildIndex >= 0)
                {
                    PendingVehicleAllocations.OnVehicleRemovedFromQueue(__instance.Id, buildIndex);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[AFD] Error in VehicleDepotBase.RemoveVehicleFromBuildOrReplaceQueue prefix: " + ex);
            }
        }
    }
}
