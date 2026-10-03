// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.

using System.Collections.Generic;
using Mafi.Core;
using Mafi.Core.Buildings.VehicleDepots;
using Mafi.Core.Entities;
using Mafi.Core.GameLoop;

namespace AutoForestryDesignations;

/// <summary>
/// Main-thread depot references. Simulation callbacks only queue membership changes;
/// the live entity collection is enumerated once, inside the sync critical section.
/// </summary>
internal static class DepotCache
{
    private static readonly object s_lock = new object();
    private static readonly Queue<KeyValuePair<VehicleDepotBase, bool>> s_pending =
        new Queue<KeyValuePair<VehicleDepotBase, bool>>();
    private static readonly List<VehicleDepotBase> s_depots = new List<VehicleDepotBase>();
    private static IEntitiesManager? s_entitiesManager;
    private static IGameLoopEvents? s_gameLoopEvents;
    private static bool s_seeded;
    private static bool s_active;

    // Read only from UI callbacks on the main thread.
    internal static IReadOnlyList<VehicleDepotBase> Depots => s_depots;

    internal static void Start(IEntitiesManager entitiesManager, IGameLoopEvents gameLoopEvents)
    {
        Stop();
        s_entitiesManager = entitiesManager;
        s_gameLoopEvents = gameLoopEvents;
        lock (s_lock) s_active = true;
        entitiesManager.EntityAdded.AddNonSaveable(typeof(DepotCache), OnEntityAdded);
        entitiesManager.EntityRemoved.AddNonSaveable(typeof(DepotCache), OnEntityRemoved);
        gameLoopEvents.SyncUpdate.AddNonSaveable(typeof(DepotCache), OnSyncUpdate);
    }

    internal static void Stop()
    {
        lock (s_lock)
        {
            s_active = false;
            s_pending.Clear();
        }
        s_entitiesManager?.EntityAdded.RemoveNonSaveable(typeof(DepotCache), OnEntityAdded);
        s_entitiesManager?.EntityRemoved.RemoveNonSaveable(typeof(DepotCache), OnEntityRemoved);
        s_gameLoopEvents?.SyncUpdate.RemoveNonSaveable(typeof(DepotCache), OnSyncUpdate);
        s_entitiesManager = null;
        s_gameLoopEvents = null;
        s_depots.Clear();
        s_seeded = false;
    }

    private static void OnEntityAdded(IEntity entity) => QueueChange(entity, true);
    private static void OnEntityRemoved(IEntity entity) => QueueChange(entity, false);

    private static void QueueChange(IEntity entity, bool added)
    {
        if (!(entity is VehicleDepotBase depot)) return;
        lock (s_lock)
        {
            if (s_active)
                s_pending.Enqueue(new KeyValuePair<VehicleDepotBase, bool>(depot, added));
        }
    }

    private static void OnSyncUpdate(GameTime time)
    {
        if (s_entitiesManager == null) return;
        lock (s_lock)
        {
            if (!s_seeded)
            {
                s_depots.Clear();
                foreach (var depot in s_entitiesManager.GetAllEntitiesOfType<VehicleDepotBase>())
                    if (!depot.IsDestroyed) s_depots.Add(depot);
                // The scan already includes all changes before this sync boundary.
                s_pending.Clear();
                s_seeded = true;
            }
            while (s_pending.Count > 0)
            {
                var change = s_pending.Dequeue();
                if (change.Value)
                {
                    if (!change.Key.IsDestroyed && !s_depots.Contains(change.Key))
                        s_depots.Add(change.Key);
                }
                else
                    s_depots.Remove(change.Key);
            }
        }
    }
}
