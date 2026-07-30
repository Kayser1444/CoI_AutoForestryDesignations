# Order and Pre-Allocate Vehicles from Tower Panel

## Overview
This feature allows players to order the construction of a vehicle directly from the control tower panel (e.g., Forestry Control Tower or Mine Tower) when the tower has vacant vehicle slots. The ordered vehicle will be constructed in the nearest eligible vehicle depot and automatically assigned to the tower upon completion.

---

## User Interaction & Modifiers
In the base game, clicking the `+` button on a vehicle assigner assigns an idle, unassigned vehicle of that type to the tower. 
* **Key Combo**: **`Shift + Alt + Click`** on the `+` button to order construction and pre-assign the vehicle to this tower.
* **UI Feedback**:
  * The `+` button should remain enabled even if no free vehicles of that prototype are currently available (`stats.Assignable <= 0`), provided there is at least one active, eligible depot that can build the vehicle.
  * A tooltip override should display: `"Shift+Alt+Click to order construction at the nearest eligible depot."`

---

## Technical Design & Architecture

### 1. Finding the Nearest Eligible Depot
When the `Shift-Alt-Click` is intercepted:
1. Retrieve the requested vehicle prototype (`DrivingEntityProto`).
2. Scan all entities of type `VehicleDepotBase` in the current world state.
3. Filter depots that satisfy the following criteria:
   * The depot is fully constructed and operational (`depot.CanWork` is true).
   * The depot's prototype lists the requested vehicle prototype as buildable:
     ```csharp
     depot.Prototype.BuildableEntities.Contains(vehicleProto)
     ```
4. Find the depot closest to the tower:
   ```csharp
   float minDistanceSqr = float.MaxValue;
   VehicleDepotBase targetDepot = null;
   foreach (var depot in eligibleDepots) {
       float distSqr = tower.Position2f.DistanceSqrTo(depot.Position2f);
       if (distSqr < minDistanceSqr) {
           minDistanceSqr = distSqr;
           targetDepot = depot;
       }
   }
   ```

### 2. Queuing Construction
Once the closest eligible depot is found, queue the construction by scheduling the game's standard input command:
```csharp
context.InputScheduler.ScheduleInputCmd(
    new AddVehicleToBuildQueueCmd(vehicleProto, targetDepot, count: 1)
);
```

### 3. Tracking Pre-Allocations (Depot-Tower Queues)
To track assignments on a per depot-tower pair basis, we maintain:
1. A mapping of `(DepotId, TowerId)` pairs to their specific queue of ordered vehicle prototypes.
2. A global sequence of orders for each depot to map build slots to target towers.

```csharp
internal static class PendingVehicleAllocations
{
    public struct OrderRecord {
        public readonly EntityId TowerId;
        public readonly DynamicEntityProto.ID ProtoId;
        
        public OrderRecord(EntityId towerId, DynamicEntityProto.ID protoId) {
            TowerId = towerId;
            ProtoId = protoId;
        }
    }

    // 1. One queue for each Depot-Tower pair
    private static readonly Dictionary<(EntityId DepotId, EntityId TowerId), Queue<DynamicEntityProto.ID>> s_queues = 
        new Dictionary<(EntityId DepotId, EntityId TowerId), Queue<DynamicEntityProto.ID>>();

    // 2. Global FIFO order of queued vehicles per depot
    private static readonly Dictionary<EntityId, List<OrderRecord>> s_globalDepotOrders = 
        new Dictionary<EntityId, List<OrderRecord>>();

    public static void Enqueue(EntityId depotId, EntityId towerId, DynamicEntityProto.ID protoId) {
        var key = (depotId, towerId);
        if (!s_queues.TryGetValue(key, out var queue)) {
            queue = new Queue<DynamicEntityProto.ID>();
            s_queues[key] = queue;
        }
        queue.Enqueue(protoId);

        if (!s_globalDepotOrders.TryGetValue(depotId, out var globalList)) {
            globalList = new List<OrderRecord>();
            s_globalDepotOrders[depotId] = globalList;
        }
        globalList.Add(new OrderRecord(towerId, protoId));
    }

    public static bool TryDequeueCompleted(EntityId depotId, DynamicEntityProto.ID protoId, out EntityId towerId) {
        towerId = EntityId.Invalid;
        if (!s_globalDepotOrders.TryGetValue(depotId, out var globalList)) return false;

        // Find the oldest build record at this depot matching the finished prototype
        int matchIndex = globalList.FindIndex(o => o.ProtoId == protoId);
        if (matchIndex == -1) return false;

        var record = globalList[matchIndex];
        globalList.RemoveAt(matchIndex);

        // Dequeue from the specific depot-tower queue
        var key = (depotId, record.TowerId);
        if (s_queues.TryGetValue(key, out var queue) && queue.Count > 0 && queue.Peek() == protoId) {
            queue.Dequeue();
        }

        towerId = record.TowerId;
        return true;
    }

    public static int GetQueuedCountForTower(EntityId towerId, DynamicEntityProto.ID protoId) {
        int count = 0;
        foreach (var kvp in s_queues) {
            if (kvp.Key.TowerId == towerId) {
                foreach (var pId in kvp.Value) {
                    if (pId == protoId) count++;
                }
            }
        }
        return count;
    }

    public static bool TryGetTowerForBuildIndex(EntityId depotId, int buildIndex, out string towerDescription) {
        towerDescription = "";
        if (s_globalDepotOrders.TryGetValue(depotId, out var globalList) && buildIndex >= 0 && buildIndex < globalList.Count) {
            var towerId = globalList[buildIndex].TowerId;
            // Lookup entity in EntitiesManager to resolve its user-facing name
            // (e.g. "Forestry Tower #4" or "Mine Tower #1")
            towerDescription = $"Tower #{towerId.Value}"; 
            return true;
        }
        return false;
    }

    public static void RemoveAt(EntityId depotId, int buildIndex) {
        if (s_globalDepotOrders.TryGetValue(depotId, out var globalList) && buildIndex >= 0 && buildIndex < globalList.Count) {
            var record = globalList[buildIndex];
            globalList.RemoveAt(buildIndex);
            
            // Reconstruct the depot-tower queue to remove the cancelled order instance
            var key = (depotId, record.TowerId);
            if (s_queues.TryGetValue(key, out var queue)) {
                var temp = new Queue<DynamicEntityProto.ID>();
                bool removed = false;
                while (queue.Count > 0) {
                    var item = queue.Dequeue();
                    if (!removed && item == record.ProtoId) {
                        removed = true;
                    } else {
                        temp.Enqueue(item);
                    }
                }
                s_queues[key] = temp;
            }
        }
    }
}
```

### 4. Hover Tooltip in Depot Queue
To show `"Pre-assigned to {towerDescription}"` when hovering over items in the depot build queue, we can patch `QueueItemUi`:
1. Use `ConditionalWeakTable` to map `QueueItemUi` instances to their `index` and `depot` context during construction.
2. Patch `QueueItemUi.Value(...)` using Harmony to check if the item is in the build queue, find its tower assignment, and update the component's tooltip.

```csharp
internal class QueueItemUiData {
    public int Index;
    public Func<VehicleDepotBase> DepotProvider;
}

[HarmonyPatch(typeof(QueueItemUi), MethodType.Constructor, new Type[] { typeof(IInputScheduler), typeof(int), typeof(Func<VehicleDepotBase>) })]
internal static class QueueItemUi_Ctor_Patch {
    public static void Postfix(object __instance, int index, Func<VehicleDepotBase> depot) {
        QueueItemUiRegistry.Table.Add(__instance, new QueueItemUiData { Index = index, DepotProvider = depot });
    }
}

[HarmonyPatch(typeof(QueueItemUi), "Value")]
internal static class QueueItemUi_Value_Patch {
    public static void Postfix(object __instance, Option<DrivingEntityProto> vehicle, Option<Vehicle> replacement) {
        if (QueueItemUiRegistry.Table.TryGetValue(__instance, out var data) && vehicle.HasValue && !replacement.HasValue) {
            var depot = data.DepotProvider();
            int buildIndex = data.Index - depot.ReplaceQueue.Count;
            if (buildIndex >= 0 && PendingVehicleAllocations.TryGetTowerForBuildIndex(depot.Id, buildIndex, out string towerDesc)) {
                ((UiComponent)__instance).Tooltip($"Pre-assigned to {towerDesc}".AsLoc());
            }
        }
    }
}
```

### 5. Queued Vehicles Indicator in Tower Panel
To indicate queued vehicles in the tower UI, we patch the constructor of `VehicleProtoAssignerUi` and append a custom observer to the assigned vehicle count display.

```csharp
[HarmonyPatch(typeof(VehicleProtoAssignerUi), MethodType.Constructor, new Type[] { typeof(UiComponent), typeof(DrivingEntityProto), typeof(UiContext), typeof(Func<IEntityAssignedWithVehicles>) })]
internal static class VehicleProtoAssignerUi_Ctor_Patch {
    public static void Postfix(VehicleProtoAssignerUi __instance, DrivingEntityProto proto, UiContext context, Func<IEntityAssignedWithVehicles> entityProvider) {
        var assignedDisplay = __instance.AllChildren.OfType<Display>().FirstOrDefault();
        if (assignedDisplay != null) {
            __instance.Observe(() => PendingVehicleAllocations.GetQueuedCountForTower(entityProvider().Id, proto.Id))
                .Do(queuedCount => {
                    int assignedCount = entityProvider().AllVehiclesWithProto(proto).Count;
                    if (queuedCount > 0) {
                        assignedDisplay.SetValue($"{assignedCount} (+{queuedCount})".AsLoc());
                    } else {
                        assignedDisplay.SetValue(assignedCount.ToString().AsLoc());
                    }
                });
        }
    }
}
```

### 6. Vehicle Spawn Interception
We intercept vehicle completion at the depot to perform the assignment.

```csharp
[HarmonyPatch(typeof(VehicleDepotBase), "TryBuildVehicle")]
internal static class VehicleDepotBase_TryBuildVehicle_Patch
{
    private static void Postfix(VehicleDepotBase __instance, bool __result, ref Vehicle vehicle)
    {
        if (__result && vehicle != null)
        {
            if (PendingVehicleAllocations.TryDequeueCompleted(__instance.Id, vehicle.Prototype.Id, out var towerId))
            {
                var entitiesManager = __instance.Context.EntitiesManager;
                if (entitiesManager.TryGetEntity<IEntityAssignedWithVehicles>(towerId, out var tower))
                {
                    if (!tower.IsDestroyed && tower.CanVehicleBeAssigned(vehicle.Prototype))
                    {
                        tower.AssignVehicle(vehicle);
                    }
                }
            }
        }
    }
}
```

---

## Edge Cases and Mitigations

1. **Queue Cancellations / Depot Deconstructions**:
   * **Problem**: If the player deletes a vehicle from a depot's build queue, our queues will drift.
   * **Mitigation**: Patch `VehicleDepotBase.RemoveVehicleFromBuildOrReplaceQueue(int index)` with a Harmony Prefix. When an item is removed from the build queue, call `PendingVehicleAllocations.RemoveAt(depot.Id, buildIndex)` to remove the cancelled order from both the global list and the tower-depot queue.
2. **Tower Destruction**:
   * **Problem**: The tower is destroyed before the ordered vehicle finishes building.
   * **Mitigation**: When dequeuing the tower ID in the spawn patch, check `tower.IsDestroyed`. If destroyed, let the vehicle remain unassigned (it will fall back to its depot's default logistics zone assignment).
3. **No Eligible Depots**:
   * **Problem**: No operational depots can build the vehicle, or all build queues are full.
   * **Mitigation**: Play an error sound/UI alert if Shift-Alt-clicking fails to find a depot.
