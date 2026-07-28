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

        public static HashSet<EntityId> GetTruckIdsForTower(EntityId towerId)
        {
            lock (s_lock)
            {
                if (s_towerTrucks.TryGetValue(towerId, out var trucks))
                {
                    return new HashSet<EntityId>(trucks);
                }
                return new HashSet<EntityId>();
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
        }

        [ThreadStatic]
        private static bool s_isRebalancing;

        public static bool IsRebalancing => s_isRebalancing;

        public static void RefreshTowerVehicleState(ForestryTower tower)
        {
            if (tower == null || tower.IsDestroyed) return;
            try
            {
                var updateAssignedVehiclesMethod = typeof(ForestryTower).GetMethod("updateAssignedVehicles", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                updateAssignedVehiclesMethod?.Invoke(tower, null);
            }
            catch { }
        }

        public static void RebalanceTowerTrucks(ForestryTower tower, IEntitiesManager? entitiesManager)
        {
            if (s_isRebalancing || tower == null || tower.IsDestroyed || entitiesManager == null) return;
            if (!AutoForestryDesignation.GetTowerTruckPoolingEnabled(tower)) return;

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
                var harvesters = GetHarvesters(tower).Where(h => !h.IsDestroyed).ToList();
                if (harvesters.Count == 0)
                {
                    return;
                }

                // Local per-rebalance truck cache to avoid repeated allocations
                var harvesterTrucksCache = new Dictionary<TreeHarvester, List<Truck>>();
                List<Truck> GetCachedTrucks(TreeHarvester h)
                {
                    if (!harvesterTrucksCache.TryGetValue(h, out var list))
                    {
                        list = GetTrucks(h);
                        harvesterTrucksCache[h] = list;
                    }
                    return list;
                }

                // Filter active/enabled harvesters and sort by physical footprint size (largest first)
                var enabledHarvesters = harvesters.Where(h => !h.IsNotEnabled).ToList();
                var targetHarvesters = (enabledHarvesters.Count > 0 ? enabledHarvesters : harvesters)
                    .OrderByDescending(h => GetHarvesterSizeRating(h))
                    .ThenByDescending(h => h.Id.Value)
                    .ToList();

                int N = validTrucks.Count;
                int M = targetHarvesters.Count;

                int baseQuota = N / M;
                int remainder = N % M;

                // Map each target harvester to its calculated quota (largest harvesters get remainder extra)
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

                // Sort valid trucks by cargo capacity / size rating (largest capacity first)
                validTrucks = validTrucks
                    .OrderByDescending(t => GetTruckCapacityRating(t))
                    .ThenByDescending(t => t.Id.Value)
                    .ToList();

                // Partition valid trucks into target truck sets for each harvester
                var harvesterTargetTrucks = new Dictionary<TreeHarvester, HashSet<Truck>>();
                int truckIndex = 0;
                foreach (var h in targetHarvesters)
                {
                    int count = quotas[h];
                    var targetSet = new HashSet<Truck>();
                    for (int k = 0; k < count && truckIndex < validTrucks.Count; k++)
                    {
                        targetSet.Add(validTrucks[truckIndex++]);
                    }
                    harvesterTargetTrucks[h] = targetSet;
                }
                foreach (var h in harvesters)
                {
                    if (!harvesterTargetTrucks.ContainsKey(h))
                    {
                        harvesterTargetTrucks[h] = new HashSet<Truck>();
                    }
                }

                // Step 1: Unassign trucks not in target set for each harvester
                foreach (var h in harvesters)
                {
                    var targetSet = harvesterTargetTrucks[h];
                    var currentTrucks = GetCachedTrucks(h).ToList();
                    foreach (var truck in currentTrucks)
                    {
                        if (!targetSet.Contains(truck))
                        {
                            h.UnassignVehicle(truck, cancelJobs: false);
                            GetCachedTrucks(h).Remove(truck);
                        }
                    }
                }

                // Step 2: Assign target trucks not currently assigned to each harvester
                foreach (var h in targetHarvesters)
                {
                    var targetSet = harvesterTargetTrucks[h];
                    var currentTrucks = GetCachedTrucks(h);
                    foreach (var truck in targetSet)
                    {
                        if (!currentTrucks.Contains(truck))
                        {
                            h.AssignVehicle(truck, doNotCancelJobs: true);
                            currentTrucks.Add(truck);
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

        private static int GetTruckCapacityRating(Truck t)
        {
            if (t == null || t.Prototype == null) return 0;
            int cap = t.Prototype.CapacityBase.Value;
            if (cap <= 0)
            {
                cap = t.Prototype.EntitySize.X.ToIntRounded() * t.Prototype.EntitySize.Y.ToIntRounded();
            }
            return cap;
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
