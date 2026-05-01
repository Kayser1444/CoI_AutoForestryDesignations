// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
// Auto Forestry Designations - Excavator Priority Synchronization
using System.Collections.Generic;
using Mafi;
using Mafi.Core;
using Mafi.Core.Buildings.Mine;
using Mafi.Core.Entities;
using Mafi.Core.Products;
using Mafi.Core.Vehicles.Excavators;

namespace AutoForestryDesignations
{
    public static partial class AutoDepthDesignation
    {
        internal static void SetTowerExcavatorPriority(MineTower tower, LooseProductProto? product)
        {
            if (tower == null)
                return;

            if (!TryGetTowerEntityId(tower, out EntityId entityId))
                return;

            if (product != null)
                s_excavatorPriorityByTowerEntityId[entityId] = product;
            else
                s_excavatorPriorityByTowerEntityId.Remove(entityId);
        }

        internal static LooseProductProto? GetTowerExcavatorPriority(MineTower tower)
        {
            if (tower == null)
                return null;

            if (TryGetTowerEntityId(tower, out EntityId entityId)
                && s_excavatorPriorityByTowerEntityId.TryGetValue(entityId, out LooseProductProto storedProduct))
            {
                return storedProduct;
            }

            return null;
        }

        /// <summary>
        /// Called by the Ticker every ~1 second. Applies the stored priority to any excavator
        /// assigned to a tower with a stored priority that currently has no priority set.
        /// This handles the case where a new excavator is assigned after the priority was set.
        /// </summary>
        internal static void ApplyPriorityToNewExcavators()
        {
            TryBootstrapTowerPrioritiesFromAssignedExcavators();

            if (s_entitiesManager == null || s_excavatorPriorityByTowerEntityId.Count == 0)
                return;

            foreach (var kvp in s_excavatorPriorityByTowerEntityId)
            {
                if (!s_entitiesManager.TryGetEntity<MineTower>(kvp.Key, out MineTower tower))
                    continue;

                if (tower == null || tower.IsDestroyed)
                    continue;

                var excavators = tower.AllAssignedExcavators;
                if (excavators == null || excavators.Count == 0)
                    continue;

                var desired = kvp.Value;
                foreach (var excavator in excavators)
                {
                    if (!excavator.PrioritizedProduct.HasValue)
                        excavator.SetPrioritizeProduct(Option.Some(desired));
                }
            }
        }

        /// <summary>
        /// On game start/load, infer a tower-level AFD priority from existing excavator priorities.
        /// If more than half of assigned excavators prioritize the same specific product, seed that
        /// product as the tower priority so new excavators inherit it via the regular ticker flow.
        /// </summary>
        private static void TryBootstrapTowerPrioritiesFromAssignedExcavators()
        {
            if (s_startupTowerPrioritySyncCompleted || s_entitiesManager == null)
                return;

            const int maxAttempts = 30;
            s_startupTowerPrioritySyncAttempts++;

            int towersSeen = 0;
            int towersSeeded = 0;

            foreach (MineTower tower in s_entitiesManager.GetAllEntitiesOfType<MineTower>())
            {
                if (tower == null || tower.IsDestroyed)
                    continue;

                towersSeen++;

                // Respect already chosen tower priority (if any).
                if (GetTowerExcavatorPriority(tower) != null)
                    continue;

                if (TryGetMajorityAssignedExcavatorPriority(tower, out LooseProductProto majorityProduct))
                {
                    SetTowerExcavatorPriority(tower, majorityProduct);
                    towersSeeded++;
                }
            }

            if (towersSeeded > 0)
            {
                Log.Info($"[AFD] Startup priority sync seeded {towersSeeded} tower(s) from assigned excavator priorities");
            }

            // Complete once towers were observed, or after retry budget is exhausted.
            if (towersSeen > 0 || s_startupTowerPrioritySyncAttempts >= maxAttempts)
            {
                s_startupTowerPrioritySyncCompleted = true;
            }
        }

        private static bool TryGetMajorityAssignedExcavatorPriority(MineTower tower, out LooseProductProto majorityProduct)
        {
            majorityProduct = null!;

            var excavators = tower.AllAssignedExcavators;
            if (excavators == null || excavators.Count == 0)
                return false;

            var countsByProduct = new Dictionary<LooseProductProto, int>();
            int totalExcavators = 0;

            foreach (var excavator in excavators)
            {
                totalExcavators++;

                var prioritized = excavator.PrioritizedProduct;
                if (!prioritized.HasValue)
                    continue;

                LooseProductProto product = prioritized.Value;
                if (countsByProduct.TryGetValue(product, out int count))
                    countsByProduct[product] = count + 1;
                else
                    countsByProduct[product] = 1;
            }

            if (totalExcavators == 0 || countsByProduct.Count == 0)
                return false;

            int bestCount = 0;
            LooseProductProto? bestProduct = null;

            foreach (var kvp in countsByProduct)
            {
                if (kvp.Value > bestCount)
                {
                    bestCount = kvp.Value;
                    bestProduct = kvp.Key;
                }
            }

            if (bestProduct != null && bestCount > totalExcavators / 2)
            {
                majorityProduct = bestProduct;
                return true;
            }

            return false;
        }
    }
}
