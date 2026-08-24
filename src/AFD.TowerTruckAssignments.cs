// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Entities;
using Mafi.Core.Vehicles;
using Mafi.Core.Vehicles.TreeHarvesters;
using Mafi.Core.Vehicles.Trucks;

namespace AutoForestryDesignations
{
    public static class TowerTruckAssignments
    {
        private static readonly object s_lock = new object();

        // TowerId -> Set of TruckIds pseudo-assigned to that tower
        private static readonly Dictionary<EntityId, HashSet<EntityId>> s_towerTrucks = new Dictionary<EntityId, HashSet<EntityId>>();

        // TruckId -> TowerId (reverse lookup)
        private static readonly Dictionary<EntityId, EntityId> s_truckToTower = new Dictionary<EntityId, EntityId>();

        // Cached reflection handle for ForestryTower.updateAssignedVehicles (private)
        private static readonly System.Lazy<MethodInfo?> s_updateAssignedVehiclesMethod = new System.Lazy<MethodInfo?>(() =>
            typeof(ForestryTower).GetMethod("updateAssignedVehicles", BindingFlags.Instance | BindingFlags.NonPublic));

        internal static MethodInfo? UpdateAssignedVehiclesMethod => s_updateAssignedVehiclesMethod.Value;

        public static void ClearAll()
        {
            lock (s_lock)
            {
                s_towerTrucks.Clear();
                s_truckToTower.Clear();
            }
        }

        public static bool HasTrucksAssigned(EntityId towerId)
        {
            lock (s_lock)
            {
                return s_towerTrucks.TryGetValue(towerId, out var trucks) && trucks.Count > 0;
            }
        }

        public static List<EntityId> GetAllTowerIdsWithTrucks()
        {
            lock (s_lock)
            {
                var result = new List<EntityId>();
                foreach (var kvp in s_towerTrucks)
                {
                    if (kvp.Value.Count > 0) result.Add(kvp.Key);
                }
                return result;
            }
        }

        public static List<EntityId> GetTruckIdsForTower(EntityId towerId)
        {
            lock (s_lock)
            {
                if (s_towerTrucks.TryGetValue(towerId, out var trucks))
                {
                    return new List<EntityId>(trucks);
                }
                return new List<EntityId>();
            }
        }

        public static EntityId? GetTowerForTruck(EntityId truckId)
        {
            lock (s_lock)
            {
                if (s_truckToTower.TryGetValue(truckId, out var towerId))
                {
                    return towerId;
                }
                return null;
            }
        }

        public static void SetTruckIdsForTower(EntityId towerId, IEnumerable<EntityId> truckIds)
        {
            lock (s_lock)
            {
                // Remove existing assignments for this tower
                if (s_towerTrucks.TryGetValue(towerId, out var oldTrucks))
                {
                    foreach (var truckId in oldTrucks)
                    {
                        s_truckToTower.Remove(truckId);
                    }
                    s_towerTrucks.Remove(towerId);
                }

                var set = new HashSet<EntityId>();
                foreach (var truckId in truckIds)
                {
                    if (truckId.IsValid)
                    {
                        set.Add(truckId);
                        s_truckToTower[truckId] = towerId;
                    }
                }

                if (set.Count > 0)
                {
                    s_towerTrucks[towerId] = set;
                }
            }
        }

        public static void AssignTruckToTower(ForestryTower tower, Truck truck, IEntitiesManager? entitiesManager)
        {
            if (tower == null || truck == null) return;
            AssignTrucksToTower(tower, new[] { truck }, entitiesManager);
        }

        public static void AssignTrucksToTower(ForestryTower tower, IEnumerable<Truck> trucks, IEntitiesManager? entitiesManager)
        {
            if (tower == null || !tower.Id.IsValid || trucks == null)
                return;

            lock (s_lock)
            {
                if (!s_towerTrucks.TryGetValue(tower.Id, out var set))
                {
                    set = new HashSet<EntityId>();
                    s_towerTrucks[tower.Id] = set;
                }

                foreach (var truck in trucks)
                {
                    if (truck == null || !truck.Id.IsValid) continue;

                    // Unassign from previous tower if needed
                    if (s_truckToTower.TryGetValue(truck.Id, out var oldTowerId))
                    {
                        if (oldTowerId != tower.Id && s_towerTrucks.TryGetValue(oldTowerId, out var oldSet))
                        {
                            oldSet.Remove(truck.Id);
                            if (oldSet.Count == 0)
                            {
                                s_towerTrucks.Remove(oldTowerId);
                            }
                        }
                    }

                    set.Add(truck.Id);
                    s_truckToTower[truck.Id] = tower.Id;
                }
            }

            RebalanceTowerTrucks(tower, entitiesManager);
        }

        public static void UnassignTruckFromTower(ForestryTower tower, Truck truck, IEntitiesManager? entitiesManager, bool playerInitiated)
        {
            if (tower == null || truck == null || !tower.Id.IsValid || !truck.Id.IsValid)
                return;

            lock (s_lock)
            {
                if (s_towerTrucks.TryGetValue(tower.Id, out var trucks))
                {
                    trucks.Remove(truck.Id);
                    if (trucks.Count == 0)
                    {
                        s_towerTrucks.Remove(tower.Id);
                    }
                }
                s_truckToTower.Remove(truck.Id);
            }

            // Only unassign truck from its harvester if this was a player-triggered deallocation
            if (playerInitiated)
            {
                var harvesters = GetHarvesters(tower);
                foreach (var harvester in harvesters)
                {
                    if (GetTrucks(harvester).Contains(truck))
                    {
                        harvester.UnassignVehicle(truck, cancelJobs: true);
                    }
                }
            }

            RebalanceTowerTrucks(tower, entitiesManager);
        }

        public static void OnTowerDestroyed(EntityId towerId)
        {
            if (!towerId.IsValid) return;

            lock (s_lock)
            {
                if (s_towerTrucks.TryGetValue(towerId, out var trucks))
                {
                    foreach (var truckId in trucks)
                    {
                        s_truckToTower.Remove(truckId);
                    }
                    s_towerTrucks.Remove(towerId);
                }
            }
        }

        public static void OnTruckDestroyed(EntityId truckId, IEntitiesManager? entitiesManager)
        {
            if (!truckId.IsValid) return;

            EntityId towerId = EntityId.Invalid;
            lock (s_lock)
            {
                if (s_truckToTower.TryGetValue(truckId, out towerId))
                {
                    s_truckToTower.Remove(truckId);
                    if (s_towerTrucks.TryGetValue(towerId, out var trucks))
                    {
                        trucks.Remove(truckId);
                        if (trucks.Count == 0)
                        {
                            s_towerTrucks.Remove(towerId);
                        }
                    }
                }
            }

            if (towerId.IsValid && entitiesManager != null && entitiesManager.TryGetEntity<ForestryTower>(towerId, out var tower))
            {
                RebalanceTowerTrucks(tower, entitiesManager);
            }
        }

        public static void OnHarvesterRemoved(TreeHarvester harvester, IEntitiesManager? entitiesManager)
        {
            if (harvester == null || entitiesManager == null) return;
            var tower = harvester.AssignedTo.ValueOrNull as ForestryTower;
            if (tower != null)
            {
                RebalanceTowerTrucks(tower, entitiesManager);
            }
        }

        /// <summary>
        /// Called when a harvester is unassigned from a tower.  The harvester's
        /// trucks are intentionally removed from the tower pool — trucks travel
        /// with their harvester rather than staying behind in the tower pool.
        /// </summary>
        public static void OnHarvesterUnassignedFromTower(TreeHarvester harvester, ForestryTower tower, IEntitiesManager? entitiesManager)
        {
            if (harvester == null || tower == null || !tower.Id.IsValid) return;

            var harvesterTrucks = GetTrucks(harvester);
            lock (s_lock)
            {
                if (s_towerTrucks.TryGetValue(tower.Id, out var set))
                {
                    foreach (var truck in harvesterTrucks)
                    {
                        if (set.Remove(truck.Id))
                        {
                            s_truckToTower.Remove(truck.Id);
                        }
                    }
                    if (set.Count == 0)
                    {
                        s_towerTrucks.Remove(tower.Id);
                    }
                }
            }

            RebalanceTowerTrucks(tower, entitiesManager);
        }

        public static void AdoptHarvesterTrucksForTower(ForestryTower tower)
        {
            if (tower == null || tower.IsDestroyed) return;
            if (!AutoForestryDesignation.GetTowerTruckPoolingEnabled(tower)) return;

            lock (s_lock)
            {
                var harvesters = GetHarvesters(tower);
                foreach (var harvester in harvesters)
                {
                    if (harvester == null || harvester.IsDestroyed) continue;
                    var trucks = GetTrucks(harvester);
                    foreach (var truck in trucks)
                    {
                        if (truck == null || truck.IsDestroyed || !truck.Id.IsValid) continue;

                        if (!s_towerTrucks.TryGetValue(tower.Id, out var set))
                        {
                            set = new HashSet<EntityId>();
                            s_towerTrucks[tower.Id] = set;
                        }

                        if (!set.Contains(truck.Id))
                        {
                            if (s_truckToTower.TryGetValue(truck.Id, out var oldTowerId) && oldTowerId != tower.Id)
                            {
                                if (s_towerTrucks.TryGetValue(oldTowerId, out var oldSet))
                                {
                                    oldSet.Remove(truck.Id);
                                    if (oldSet.Count == 0) s_towerTrucks.Remove(oldTowerId);
                                }
                            }

                            set.Add(truck.Id);
                            s_truckToTower[truck.Id] = tower.Id;
                        }
                    }
                }
            }
        }

        public static void ReconcileAndPurgeStaleEntries(IEntitiesManager entitiesManager)
        {
            if (entitiesManager == null) return;

            lock (s_lock)
            {
                var deadTowers = new List<EntityId>();
                foreach (var pair in s_towerTrucks)
                {
                    if (!entitiesManager.TryGetEntity<ForestryTower>(pair.Key, out var tower) || tower.IsDestroyed)
                    {
                        deadTowers.Add(pair.Key);
                        continue;
                    }

                    var deadTrucks = new List<EntityId>();
                    foreach (var truckId in pair.Value)
                    {
                        if (!entitiesManager.TryGetEntity<Truck>(truckId, out var truck) || truck.IsDestroyed)
                        {
                            deadTrucks.Add(truckId);
                        }
                    }

                    foreach (var deadTruckId in deadTrucks)
                    {
                        pair.Value.Remove(deadTruckId);
                        s_truckToTower.Remove(deadTruckId);
                    }
                }

                foreach (var deadTowerId in deadTowers)
                {
                    if (s_towerTrucks.TryGetValue(deadTowerId, out var trucks))
                    {
                        foreach (var truckId in trucks)
                        {
                            s_truckToTower.Remove(truckId);
                        }
                        s_towerTrucks.Remove(deadTowerId);
                    }
                }
            }

            foreach (var tower in entitiesManager.GetAllEntitiesOfType<ForestryTower>())
            {
                if (tower == null || tower.IsDestroyed) continue;
                if (!AutoForestryDesignation.GetTowerTruckPoolingEnabled(tower)) continue;

                AdoptHarvesterTrucksForTower(tower);
                RebalanceTowerTrucks(tower, entitiesManager);
            }
        }

        [ThreadStatic]
        private static bool s_isRebalancing;

        public static bool IsRebalancing => s_isRebalancing;

        public static void RefreshTowerVehicleState(ForestryTower tower)
        {
            if (tower == null || tower.IsDestroyed) return;
            try
            {
                UpdateAssignedVehiclesMethod?.Invoke(tower, null);
            }
            catch { }
        }

        public static void RebalanceTowerTrucks(ForestryTower tower, IEntitiesManager? entitiesManager)
        {
            if (s_isRebalancing || tower == null || tower.IsDestroyed || entitiesManager == null) return;
            if (!AutoForestryDesignation.GetTowerTruckPoolingEnabled(tower)) return;

            AdoptHarvesterTrucksForTower(tower);

            try
            {
                s_isRebalancing = true;

                // Gather valid pseudo-assigned trucks for this tower
                HashSet<EntityId> truckIds;
                lock (s_lock)
                {
                    if (!s_towerTrucks.TryGetValue(tower.Id, out truckIds) || truckIds.Count == 0)
                    {
                        return;
                    }
                    truckIds = new HashSet<EntityId>(truckIds);
                }

                var validTrucks = new List<Truck>();
                foreach (var truckId in truckIds)
                {
                    if (entitiesManager.TryGetEntity<Truck>(truckId, out var truck) && !truck.IsDestroyed)
                    {
                        validTrucks.Add(truck);
                    }
                }

                // Gather valid tree harvesters assigned to this tower
                var harvesters = GetHarvesters(tower);
                harvesters.RemoveAll(h => h.IsDestroyed);
                if (harvesters.Count == 0)
                {
                    return;
                }

                // Snapshot current truck assignments per harvester
                var currentTrucksByHarvester = new Dictionary<TreeHarvester, List<Truck>>();
                foreach (var h in harvesters)
                {
                    currentTrucksByHarvester[h] = GetTrucks(h);
                }

                // Build a set of pool truck IDs for quick membership checks
                var poolTruckIdSet = new HashSet<EntityId>(truckIds);

                // Count current pool trucks per harvester (excluding non-pool trucks)
                var currentPoolCount = new Dictionary<TreeHarvester, int>();
                foreach (var h in harvesters)
                {
                    int count = 0;
                    foreach (var truck in currentTrucksByHarvester[h])
                    {
                        if (poolTruckIdSet.Contains(truck.Id)) count++;
                    }
                    currentPoolCount[h] = count;
                }

                // Filter active/enabled harvesters
                var enabledHarvesters = new List<TreeHarvester>();
                foreach (var h in harvesters)
                {
                    if (!h.IsNotEnabled) enabledHarvesters.Add(h);
                }
                var targetHarvesters = enabledHarvesters.Count > 0 ? enabledHarvesters : harvesters;

                // Sort target harvesters:
                // 1. Physical footprint size rating (largest first)
                // 2. Current assigned pool truck count (highest first — preserves existing assignments when remainder > 0)
                // 3. EntityId (deterministic tie-breaker)
                targetHarvesters.Sort((a, b) =>
                {
                    int cmp = GetHarvesterSizeRating(b).CompareTo(GetHarvesterSizeRating(a));
                    if (cmp != 0) return cmp;

                    int countA = currentPoolCount.TryGetValue(a, out int cA) ? cA : 0;
                    int countB = currentPoolCount.TryGetValue(b, out int cB) ? cB : 0;
                    cmp = countB.CompareTo(countA);
                    if (cmp != 0) return cmp;

                    return b.Id.Value.CompareTo(a.Id.Value);
                });

                int N = validTrucks.Count;
                int M = targetHarvesters.Count;

                int baseQuota = N / M;
                int remainder = N % M;

                // Map each target harvester to its calculated truck quota
                var quotas = new Dictionary<TreeHarvester, int>();
                for (int i = 0; i < targetHarvesters.Count; i++)
                {
                    quotas[targetHarvesters[i]] = baseQuota + (i < remainder ? 1 : 0);
                }

                // Disabled harvesters (if any enabled exist) get quota 0
                foreach (var h in harvesters)
                {
                    if (!quotas.ContainsKey(h))
                    {
                        quotas[h] = 0;
                    }
                }

                // Group pool trucks by proto ID string
                var trucksByProto = new Dictionary<string, List<Truck>>();
                foreach (var truck in validTrucks)
                {
                    string protoKey = truck.Prototype.Id.Value;
                    if (!trucksByProto.TryGetValue(protoKey, out var list))
                    {
                        list = new List<Truck>();
                        trucksByProto[protoKey] = list;
                    }
                    list.Add(truck);
                }

                // --- Stability-preserving rebalance ---
                // For each proto, identify over-quota and under-quota harvesters
                // and transfer the minimum number of trucks.
                foreach (var kvp in trucksByProto)
                {
                    var protoTrucks = kvp.Value;

                    // Count how many of this proto each harvester currently has
                    var protoCountPerHarvester = new Dictionary<TreeHarvester, int>();
                    foreach (var h in harvesters)
                    {
                        int count = 0;
                        foreach (var truck in currentTrucksByHarvester[h])
                        {
                            if (truck.Prototype.Id.Value == kvp.Key && poolTruckIdSet.Contains(truck.Id))
                                count++;
                        }
                        protoCountPerHarvester[h] = count;
                    }

                    // Find unassigned trucks of this proto (not on any harvester)
                    var assignedTruckIds = new HashSet<EntityId>();
                    foreach (var h in harvesters)
                    {
                        foreach (var truck in currentTrucksByHarvester[h])
                        {
                            assignedTruckIds.Add(truck.Id);
                        }
                    }

                    var unassigned = new List<Truck>();
                    foreach (var truck in protoTrucks)
                    {
                        if (!assignedTruckIds.Contains(truck.Id))
                            unassigned.Add(truck);
                    }

                    // Assign unassigned trucks to under-quota harvesters, preferring harvesters with fewer trucks of this proto
                    foreach (var truck in unassigned)
                    {
                        TreeHarvester? best = null;
                        int bestDeficit = 0;
                        int minProtoCount = int.MaxValue;

                        foreach (var h in targetHarvesters)
                        {
                            int deficit = quotas[h] - currentPoolCount[h];
                            if (deficit <= 0) continue;

                            int pCount = protoCountPerHarvester.TryGetValue(h, out int pc) ? pc : 0;
                            if (deficit > bestDeficit || (deficit == bestDeficit && pCount < minProtoCount))
                            {
                                best = h;
                                bestDeficit = deficit;
                                minProtoCount = pCount;
                            }
                        }

                        if (best != null)
                        {
                            // A free truck may still have a job from its previous role. The
                            // job can reference an owner-specific queue, so assigning it to a
                            // harvester must use vanilla's cancel-on-assignment semantics.
                            best.AssignVehicle(truck, doNotCancelJobs: false);
                            currentTrucksByHarvester[best].Add(truck);
                            currentPoolCount[best]++;
                            if (protoCountPerHarvester.ContainsKey(best))
                                protoCountPerHarvester[best]++;
                            else
                                protoCountPerHarvester[best] = 1;
                        }
                    }
                }

                // After assigning unassigned trucks, do the over/under transfer pass
                // across all pool trucks (proto-agnostic — any equivalent truck will do)

                // Collect donors (harvesters over quota) and their stealable pool trucks
                var donors = new List<(TreeHarvester harvester, List<Truck> stealable)>();
                foreach (var h in harvesters)
                {
                    int surplus = currentPoolCount[h] - quotas[h];
                    if (surplus > 0)
                    {
                        // Collect pool trucks that can be moved (pick from the end to minimize disruption)
                        var stealable = new List<Truck>();
                        var currentList = currentTrucksByHarvester[h];
                        for (int i = currentList.Count - 1; i >= 0 && stealable.Count < surplus; i--)
                        {
                            if (poolTruckIdSet.Contains(currentList[i].Id))
                                stealable.Add(currentList[i]);
                        }
                        if (stealable.Count > 0)
                            donors.Add((h, stealable));
                    }
                }

                // Transfer from donors to receivers (under-quota harvesters)
                foreach (var h in targetHarvesters)
                {
                    int deficit = quotas[h] - currentPoolCount[h];
                    if (deficit <= 0) continue;

                    for (int d = 0; d < donors.Count && deficit > 0; d++)
                    {
                        var (donor, stealable) = donors[d];
                        while (stealable.Count > 0 && deficit > 0)
                        {
                            var truck = stealable[stealable.Count - 1];
                            stealable.RemoveAt(stealable.Count - 1);

                            // Jobs issued by the donor can reference its truck queue. Cancel
                            // them before changing ownership so the truck cannot retain a job
                            // for a harvester to which it is no longer assigned.
                            donor.UnassignVehicle(truck, cancelJobs: true);
                            currentTrucksByHarvester[donor].Remove(truck);
                            currentPoolCount[donor]--;

                            // Unassignment above already requested cancellation.
                            h.AssignVehicle(truck, doNotCancelJobs: true);
                            currentTrucksByHarvester[h].Add(truck);
                            currentPoolCount[h]++;

                            deficit--;
                        }
                    }
                }

                // Clean up: unassign any pool trucks still on disabled (quota-0) harvesters
                foreach (var h in harvesters)
                {
                    if (quotas[h] > 0) continue;
                    var currentList = currentTrucksByHarvester[h];
                    for (int i = currentList.Count - 1; i >= 0; i--)
                    {
                        var truck = currentList[i];
                        if (poolTruckIdSet.Contains(truck.Id))
                        {
                            // Disabled harvesters must release both the truck and any job tied
                            // to their queue before the truck is assigned elsewhere.
                            h.UnassignVehicle(truck, cancelJobs: true);
                            currentList.RemoveAt(i);
                            currentPoolCount[h]--;

                            // Assign to the most under-quota target harvester
                            TreeHarvester? best = null;
                            int bestDeficit = 0;
                            foreach (var th in targetHarvesters)
                            {
                                int def = quotas[th] - currentPoolCount[th];
                                if (def > bestDeficit)
                                {
                                    best = th;
                                    bestDeficit = def;
                                }
                            }
                            if (best != null)
                            {
                                // Unassignment above already requested cancellation.
                                best.AssignVehicle(truck, doNotCancelJobs: true);
                                currentTrucksByHarvester[best].Add(truck);
                                currentPoolCount[best]++;
                            }
                        }
                    }
                }
            }
            finally
            {
                s_isRebalancing = false;
                RefreshTowerVehicleState(tower);
            }
        }

        private static int GetHarvesterSizeRating(TreeHarvester h)
        {
            if (h == null || h.Prototype == null) return 0;
            return h.Prototype.EntitySize.X.ToIntRounded() * h.Prototype.EntitySize.Y.ToIntRounded();
        }

        private static List<TreeHarvester> GetHarvesters(ForestryTower tower)
        {
            var list = new List<TreeHarvester>();
            if (tower != null && tower.AllVehicles != null)
            {
                for (int i = 0; i < tower.AllVehicles.Count; i++)
                {
                    if (tower.AllVehicles[i] is TreeHarvester h)
                        list.Add(h);
                }
            }
            return list;
        }

        private static List<Truck> GetTrucks(TreeHarvester harvester)
        {
            var list = new List<Truck>();
            if (harvester != null && harvester.AllVehicles != null)
            {
                for (int i = 0; i < harvester.AllVehicles.Count; i++)
                {
                    if (harvester.AllVehicles[i] is Truck t)
                        list.Add(t);
                }
            }
            return list;
        }
    }
}
