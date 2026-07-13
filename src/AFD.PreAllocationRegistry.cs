// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CoI.AutoHelpers.Persistence;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Buildings.VehicleDepots;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.Prototypes;
using Mafi.Serialization;

namespace AutoForestryDesignations
{
    internal static class PendingVehicleAllocations
    {
        private static readonly object s_lock = new object();

        public class BuildQueueItem
        {
            public readonly DynamicEntityProto.ID ProtoId;
            public EntityId TowerId; // Can be Invalid if the tower was destroyed

            public BuildQueueItem(DynamicEntityProto.ID protoId, EntityId towerId)
            {
                ProtoId = protoId;
                TowerId = towerId;
            }
        }

        public struct Ticket
        {
            public readonly EntityId DepotId;
            public readonly EntityId TowerId;
            public readonly DynamicEntityProto.ID ProtoId;
            public readonly long EnqueuedTickCount;

            public Ticket(EntityId depotId, EntityId towerId, DynamicEntityProto.ID protoId, long enqueuedTickCount = 0)
            {
                DepotId = depotId;
                TowerId = towerId;
                ProtoId = protoId;
                EnqueuedTickCount = enqueuedTickCount != 0 ? enqueuedTickCount : CurrentTimeMs;
            }
        }

        private static long CurrentTimeMs => DateTime.UtcNow.Ticks / 10000;

        // 1. Matched build queue items per depot (aligned 1:1 with actual depot build queue)
        private static readonly Dictionary<EntityId, List<BuildQueueItem>> s_depotBuildQueues =
            new Dictionary<EntityId, List<BuildQueueItem>>();

        // 2. Unmatched pending tickets (enqueued when mod orders vehicle, dequeued when added to build queue)
        private static readonly Queue<Ticket> s_pendingTickets = new Queue<Ticket>();

        public static void ClearAll()
        {
            lock (s_lock)
            {
                s_depotBuildQueues.Clear();
                s_pendingTickets.Clear();
            }
        }

        private static void ExpireStaleTicketsLocked()
        {
            long now = CurrentTimeMs;
            var temp = new Queue<Ticket>();
            while (s_pendingTickets.Count > 0)
            {
                var ticket = s_pendingTickets.Dequeue();
                if (now - ticket.EnqueuedTickCount <= 30_000)
                {
                    temp.Enqueue(ticket);
                }
                else
                {
                    AutoForestryDesignation.s_log.Info($"Pending allocations: Expired stale pending ticket for proto {ticket.ProtoId.Value} at depot {ticket.DepotId.Value}.");
                }
            }
            while (temp.Count > 0)
            {
                s_pendingTickets.Enqueue(temp.Dequeue());
            }
        }

        public static void ExpireStaleTickets()
        {
            lock (s_lock)
            {
                ExpireStaleTicketsLocked();
            }
        }

        public static void Enqueue(EntityId depotId, EntityId towerId, DynamicEntityProto.ID protoId)
        {
            lock (s_lock)
            {
                ExpireStaleTicketsLocked();
                s_pendingTickets.Enqueue(new Ticket(depotId, towerId, protoId, CurrentTimeMs));
            }
        }

        public static void OnVehicleAddFailed(EntityId depotId, DynamicEntityProto.ID protoId)
        {
            lock (s_lock)
            {
                Ticket? matchedTicket = null;
                var temp = new Queue<Ticket>();

                while (s_pendingTickets.Count > 0)
                {
                    var ticket = s_pendingTickets.Dequeue();
                    if (matchedTicket == null && ticket.DepotId == depotId && ticket.ProtoId == protoId)
                    {
                        matchedTicket = ticket;
                    }
                    else
                    {
                        temp.Enqueue(ticket);
                    }
                }

                while (temp.Count > 0)
                {
                    s_pendingTickets.Enqueue(temp.Dequeue());
                }

                if (matchedTicket != null)
                {
                    AutoForestryDesignation.s_log.Info($"Pending allocations: Discarded ticket for {protoId.Value} at depot {depotId.Value} due to AddVehicleToBuildQueue failure.");
                }
            }
        }

        public static int CancelPendingTickets(EntityId towerId, DynamicEntityProto.ID protoId, int maxToCancel)
        {
            int cancelled = 0;
            lock (s_lock)
            {
                var temp = new Queue<Ticket>();
                while (s_pendingTickets.Count > 0)
                {
                    var ticket = s_pendingTickets.Dequeue();
                    if (cancelled < maxToCancel && ticket.TowerId == towerId && ticket.ProtoId == protoId)
                    {
                        cancelled++;
                    }
                    else
                    {
                        temp.Enqueue(ticket);
                    }
                }
                while (temp.Count > 0)
                {
                    s_pendingTickets.Enqueue(temp.Dequeue());
                }
            }
            return cancelled;
        }

        public static void OnVehicleAddedToQueue(EntityId depotId, DynamicEntityProto.ID protoId)
        {
            lock (s_lock)
            {
                // Find a matching ticket in s_pendingTickets FIFO
                Ticket? matchedTicket = null;
                var temp = new Queue<Ticket>();

                while (s_pendingTickets.Count > 0)
                {
                    var ticket = s_pendingTickets.Dequeue();
                    if (matchedTicket == null && ticket.DepotId == depotId && ticket.ProtoId == protoId)
                    {
                        matchedTicket = ticket;
                    }
                    else
                    {
                        temp.Enqueue(ticket);
                    }
                }
                
                // Restore remaining unmatched tickets
                while (temp.Count > 0)
                {
                    s_pendingTickets.Enqueue(temp.Dequeue());
                }

                if (!s_depotBuildQueues.TryGetValue(depotId, out var queueList))
                {
                    queueList = new List<BuildQueueItem>();
                    s_depotBuildQueues[depotId] = queueList;
                }

                var towerId = matchedTicket.HasValue ? matchedTicket.Value.TowerId : EntityId.Invalid;
                queueList.Add(new BuildQueueItem(protoId, towerId));
            }
        }

        public static void OnVehicleRemovedFromQueue(EntityId depotId, int buildIndex)
        {
            lock (s_lock)
            {
                if (s_depotBuildQueues.TryGetValue(depotId, out var queueList) && buildIndex >= 0 && buildIndex < queueList.Count)
                {
                    queueList.RemoveAt(buildIndex);
                }
            }
        }

        public static bool TryGetTowerForBuildIndex(IEntitiesManager entitiesManager, EntityId depotId, int buildIndex, out string towerDescription)
        {
            towerDescription = "";
            lock (s_lock)
            {
                if (s_depotBuildQueues.TryGetValue(depotId, out var queueList) && buildIndex >= 0 && buildIndex < queueList.Count)
                {
                    var towerId = queueList[buildIndex].TowerId;
                    if (towerId.IsValid)
                    {
                        if (entitiesManager.TryGetEntity<IEntityAssignedWithVehicles>(towerId, out var tower) && !tower.IsDestroyed)
                        {
                            towerDescription = GetEntityDescription(tower);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static bool TryDequeueCompleted(EntityId depotId, DynamicEntityProto.ID protoId, out EntityId towerId)
        {
            towerId = EntityId.Invalid;
            lock (s_lock)
            {
                if (s_depotBuildQueues.TryGetValue(depotId, out var queueList))
                {
                    if (queueList.Count > 0)
                    {
                        if (queueList[0].ProtoId == protoId)
                        {
                            towerId = queueList[0].TowerId;
                            queueList.RemoveAt(0);
                            return towerId.IsValid;
                        }
                        else
                        {
                            int matchIndex = -1;
                            for (int i = 1; i < queueList.Count; i++)
                            {
                                if (queueList[i].ProtoId == protoId)
                                {
                                    matchIndex = i;
                                    break;
                                }
                            }

                            if (matchIndex >= 0)
                            {
                                AutoForestryDesignation.s_log.Warning($"TryDequeueCompleted: Proto mismatch at head for depot {depotId.Value}. Expected: {protoId.Value}, Found: {queueList[0].ProtoId.Value}. Recovering by popping {matchIndex + 1} item(s) up to matching index {matchIndex}.");
                                towerId = queueList[matchIndex].TowerId;
                                queueList.RemoveRange(0, matchIndex + 1);
                                return towerId.IsValid;
                            }
                            else
                            {
                                AutoForestryDesignation.s_log.Warning($"TryDequeueCompleted: Proto mismatch at queue head for depot {depotId.Value}. Expected: {protoId.Value}, Found: {queueList[0].ProtoId.Value}. No match found in registry queue.");
                            }
                        }
                    }
                }
            }
            return false;
        }

        public static int GetQueuedCountForTower(EntityId towerId, DynamicEntityProto.ID protoId)
        {
            int count = 0;
            lock (s_lock)
            {
                // Count tickets
                foreach (var ticket in s_pendingTickets)
                {
                    if (ticket.TowerId == towerId && ticket.ProtoId == protoId) count++;
                }

                // Count items in build queues
                foreach (var queueList in s_depotBuildQueues.Values)
                {
                    foreach (var item in queueList)
                    {
                        if (item.TowerId == towerId && item.ProtoId == protoId) count++;
                    }
                }
            }
            return count;
        }

        public static int GetQueuedCountForDepot(EntityId depotId)
        {
            int count = 0;
            lock (s_lock)
            {
                foreach (var ticket in s_pendingTickets)
                {
                    if (ticket.DepotId == depotId) count++;
                }

                if (s_depotBuildQueues.TryGetValue(depotId, out var queueList))
                {
                    foreach (var item in queueList)
                    {
                        if (item.TowerId.IsValid) count++;
                    }
                }
            }
            return count;
        }

        public static void OnTowerDestroyed(EntityId towerId)
        {
            lock (s_lock)
            {
                // Clear any unmatched tickets
                var temp = new Queue<Ticket>();
                while (s_pendingTickets.Count > 0)
                {
                    var ticket = s_pendingTickets.Dequeue();
                    if (ticket.TowerId != towerId)
                    {
                        temp.Enqueue(ticket);
                    }
                }
                while (temp.Count > 0)
                {
                    s_pendingTickets.Enqueue(temp.Dequeue());
                }

                // Clear towerId (set to Invalid) from all matching items in build queues
                foreach (var queueList in s_depotBuildQueues.Values)
                {
                    foreach (var item in queueList)
                    {
                        if (item.TowerId == towerId)
                        {
                            item.TowerId = EntityId.Invalid;
                        }
                    }
                }
            }
        }

        public static void OnDepotDestroyed(EntityId depotId)
        {
            lock (s_lock)
            {
                s_depotBuildQueues.Remove(depotId);

                // Purge unmatched tickets for this depot
                var temp = new Queue<Ticket>();
                while (s_pendingTickets.Count > 0)
                {
                    var ticket = s_pendingTickets.Dequeue();
                    if (ticket.DepotId != depotId)
                    {
                        temp.Enqueue(ticket);
                    }
                }
                while (temp.Count > 0)
                {
                    s_pendingTickets.Enqueue(temp.Dequeue());
                }
            }
        }

        public class EnqueuedItemInfo
        {
            public readonly EntityId DepotId;
            public readonly BuildQueueItem Item;

            public EnqueuedItemInfo(EntityId depotId, BuildQueueItem item)
            {
                DepotId = depotId;
                Item = item;
            }
        }

        public static Lyst<EnqueuedItemInfo> GetEnqueuedItemsForTowerAndProto(EntityId towerId, DynamicEntityProto.ID protoId)
        {
            var result = new Lyst<EnqueuedItemInfo>();
            lock (s_lock)
            {
                foreach (var kv in s_depotBuildQueues)
                {
                    foreach (var item in kv.Value)
                    {
                        if (item.TowerId == towerId && item.ProtoId == protoId)
                        {
                            result.Add(new EnqueuedItemInfo(kv.Key, item));
                        }
                    }
                }
            }
            return result;
        }

        public static bool TryGetBuildIndexForItem(EntityId depotId, BuildQueueItem item, out int buildIndex)
        {
            buildIndex = -1;
            lock (s_lock)
            {
                if (s_depotBuildQueues.TryGetValue(depotId, out var queueList))
                {
                    buildIndex = queueList.IndexOf(item);
                    return buildIndex >= 0;
                }
            }
            return false;
        }

        public static string GetEntityDescription(IEntity entity)
        {
            if (entity is IObjectWithCustomTitle customTitleObj && customTitleObj.CustomTitle.HasValue)
            {
                return customTitleObj.CustomTitle.Value;
            }

            return entity.Prototype.Strings.Name.ToString();
        }

        public static void LoadFromJsonStore(IModStateJsonStore store)
        {
            string json = store.LoadJson();
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                lock (s_lock)
                {
                    object parsed = new JsonParser().Parse(new StringReader(json));
                    if (parsed is Dict<string, object> root)
                    {
                        if (TryGetInt(root, "schemaVersion", out int schemaVersion) && schemaVersion == 1)
                        {
                            s_depotBuildQueues.Clear();
                            s_pendingTickets.Clear();

                            if (root.TryGetValue("queues", out object rawQueues) && rawQueues is object[] queues)
                            {
                                foreach (object rawQueue in queues)
                                {
                                    if (rawQueue is Dict<string, object> qEntry &&
                                        TryGetInt(qEntry, "depotId", out int depotIdVal) &&
                                        qEntry.TryGetValue("items", out object rawItems) &&
                                        rawItems is object[] items)
                                    {
                                        var list = new List<BuildQueueItem>();
                                        foreach (object rawItem in items)
                                        {
                                            if (rawItem is Dict<string, object> itemEntry &&
                                                itemEntry.TryGetValue("protoId", out object rawProto) && rawProto is string protoStr &&
                                                TryGetInt(itemEntry, "towerId", out int towerIdVal))
                                            {
                                                list.Add(new BuildQueueItem(new DynamicEntityProto.ID(protoStr), new EntityId(towerIdVal)));
                                            }
                                        }
                                        s_depotBuildQueues[new EntityId(depotIdVal)] = list;
                                    }
                                }
                            }

                            if (root.TryGetValue("tickets", out object rawTickets) && rawTickets is object[] tickets)
                            {
                                foreach (object rawTicket in tickets)
                                {
                                    if (rawTicket is Dict<string, object> ticketEntry &&
                                        TryGetInt(ticketEntry, "depotId", out int depotIdVal) &&
                                        TryGetInt(ticketEntry, "towerId", out int towerIdVal) &&
                                        ticketEntry.TryGetValue("protoId", out object rawProto) && rawProto is string protoStr)
                                    {
                                        long ticksVal = CurrentTimeMs;
                                        if (ticketEntry.TryGetValue("enqueuedTickCount", out object rawTicks) && rawTicks is long lVal)
                                        {
                                            ticksVal = lVal;
                                        }
                                        s_pendingTickets.Enqueue(new Ticket(new EntityId(depotIdVal), new EntityId(towerIdVal), new DynamicEntityProto.ID(protoStr), ticksVal));
                                    }
                                }
                            }
                        }
                    }
                }
                AutoForestryDesignation.s_log.Info($"Pending allocations: loaded from {store.StorageKind}.");
            }
            catch (Exception ex)
            {
                AutoForestryDesignation.s_log.Warning($"Pending allocations: failed to load from {store.StorageKind}: {ex.Message}");
            }
        }

        public static void SaveToJsonStore(IModStateJsonStore store)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\"schemaVersion\":1,\"queues\":[");

                lock (s_lock)
                {
                    bool firstQueue = true;
                    foreach (var kvp in s_depotBuildQueues)
                    {
                        if (kvp.Value.Count == 0) continue;
                        if (!firstQueue) sb.Append(',');
                        firstQueue = false;

                        sb.Append("{\"depotId\":").Append(kvp.Key.Value).Append(",\"items\":[");
                        bool firstItem = true;
                        foreach (var item in kvp.Value)
                        {
                            if (!firstItem) sb.Append(',');
                            firstItem = false;
                            sb.Append("{\"protoId\":\"").Append(item.ProtoId.Value).Append("\",\"towerId\":").Append(item.TowerId.Value).Append("}");
                        }
                        sb.Append("]}");
                    }
                    sb.Append("],\"tickets\":[");

                    bool firstTicket = true;
                    foreach (var ticket in s_pendingTickets)
                    {
                        if (!firstTicket) sb.Append(',');
                        firstTicket = false;
                        sb.Append("{\"depotId\":").Append(ticket.DepotId.Value)
                          .Append(",\"towerId\":").Append(ticket.TowerId.Value)
                          .Append(",\"protoId\":\"").Append(ticket.ProtoId.Value)
                          .Append("\",\"enqueuedTickCount\":").Append(ticket.EnqueuedTickCount).Append("}");
                    }
                    sb.Append("]}");
                }

                ModStateJsonSaveResult result = store.SaveJson(sb.ToString());
                if (!result.Succeeded)
                {
                    AutoForestryDesignation.s_log.Warning($"Pending allocations: failed to stage in {result.StorageKind}: {result.ErrorMessage}");
                    return;
                }
                AutoForestryDesignation.s_log.Info($"Pending allocations: staged in {store.StorageKind}.");
            }
            catch (Exception ex)
            {
                AutoForestryDesignation.s_log.Warning($"Pending allocations: failed to save to {store.StorageKind}: {ex.Message}");
            }
        }

        private static bool TryGetInt(Dict<string, object> dict, string key, out int value)
        {
            value = 0;
            if (dict.TryGetValue(key, out object rawValue))
            {
                if (rawValue is int intValue)
                {
                    value = intValue;
                    return true;
                }
                if (rawValue is double doubleValue)
                {
                    value = (int)doubleValue;
                    return true;
                }
            }
            return false;
        }

        public static void ReconcileQueues(IEntitiesManager entitiesManager)
        {
            lock (s_lock)
            {
                ExpireStaleTicketsLocked();
                foreach (VehicleDepotBase depot in entitiesManager.GetAllEntitiesOfType<VehicleDepotBase>())
                {
                    if (depot.IsDestroyed || s_depotBuildQueues.ContainsKey(depot.Id))
                        continue;
                    var seededQueue = new List<BuildQueueItem>();
                    for (int i = 0; i < depot.BuildQueue.Count; i++)
                        seededQueue.Add(new BuildQueueItem(
                            new DynamicEntityProto.ID(depot.BuildQueue[i].Id.Value),
                            EntityId.Invalid));
                    s_depotBuildQueues[depot.Id] = seededQueue;
                }

                var depotIdsToRemove = new List<EntityId>();
                foreach (var kvp in s_depotBuildQueues)
                {
                    EntityId depotId = kvp.Key;
                    var ourQueue = kvp.Value;

                    if (!entitiesManager.TryGetEntity<VehicleDepotBase>(depotId, out var depot) || depot.IsDestroyed)
                    {
                        depotIdsToRemove.Add(depotId);
                        continue;
                    }

                    int actualCount = depot.BuildQueue.Count;
                    if (ourQueue.Count != actualCount)
                    {
                        AutoForestryDesignation.s_log.Warning($"ReconcileQueues: Queue count mismatch for depot {depotId.Value}. Actual: {actualCount}, Registry: {ourQueue.Count}. Re-aligning...");

                        if (ourQueue.Count > actualCount)
                        {
                            ourQueue.RemoveRange(actualCount, ourQueue.Count - actualCount);
                        }
                        else
                        {
                            while (ourQueue.Count < actualCount)
                            {
                                ourQueue.Add(new BuildQueueItem(new DynamicEntityProto.ID(depot.BuildQueue[ourQueue.Count].Id.Value), EntityId.Invalid));
                            }
                        }
                    }

                    for (int i = 0; i < actualCount; i++)
                    {
                        string actualProtoId = depot.BuildQueue[i].Id.Value;
                        if (ourQueue[i].ProtoId.Value != actualProtoId)
                        {
                            AutoForestryDesignation.s_log.Warning($"ReconcileQueues: Proto mismatch at index {i} for depot {depotId.Value}. Actual: {actualProtoId}, Registry: {ourQueue[i].ProtoId.Value}. Resetting allocation target.");
                            ourQueue[i] = new BuildQueueItem(new DynamicEntityProto.ID(actualProtoId), EntityId.Invalid);
                        }
                    }
                }

                foreach (var depotId in depotIdsToRemove)
                {
                    s_depotBuildQueues.Remove(depotId);
                }
            }
        }
    }
}
