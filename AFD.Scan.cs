// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
// Auto Forestry Designations - Designation Scanning
using System.Collections;
using Mafi;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Terrain.Trees;
using UnityEngine;

namespace AutoForestryDesignations
{
    public static partial class AutoForestryDesignation
    {
        private static IEnumerator CreateDesignationsCoroutine(IAreaManagingTower tower)
        {
            if (s_desigManager == null || s_forestryProto == null) yield break;

            var area = tower.Area;
            if (area.IsEmpty) yield break;

            var terrMgr = s_desigManager.TerrainManager;
            var treesManager = s_desigManager.TreesManager;
            var towerSettings = GetOrCreateTowerSettings(tower);
            bool avoidInfertile = towerSettings.AvoidInfertileTiles;
            bool avoidWithTrees = towerSettings.AvoidTilesWithTrees;
            bool avoidMiningDesignations = towerSettings.AvoidMiningDesignations;
            int maxTiles = towerSettings.MaxTiles;
            bool markFullyGrown = towerSettings.MarkFullyGrownForHarvest;

            var bbMin = TerrainDesignation.GetOrigin(area.BoundingBoxMin);
            var bbMax = TerrainDesignation.GetOrigin(area.BoundingBoxMax);

            LogDebug(string.Format("Scanning forestry area from {0} to {1} for planting zones...", bbMin, bbMax));

            int designCount = 0;
            int scanCount = 0;
            bool limitReached = false;

            for (int y = bbMin.Y; y <= bbMax.Y && !limitReached; y += 4)
            {
                for (int x = bbMin.X; x <= bbMax.X && !limitReached; x += 4)
                {
                    if (maxTiles > 0 && designCount >= maxTiles) { limitReached = true; break; }

                    // Reject if any sub-tile is outside the polygon area
                    bool inArea = true;
                    for (int dy = 0; dy < 4 && inArea; dy++)
                        for (int dx = 0; dx < 4 && inArea; dx++)
                            if (!area.ContainsTile(new Tile2i(x + dx, y + dy)))
                                inArea = false;
                    if (!inArea) { scanCount++; continue; }

                    // Check fertility and tree presence across all sub-tiles
                    bool allFertile = true;
                    bool anyTree = false;
                    for (int dy = 0; dy < 4; dy++)
                    {
                        for (int dx = 0; dx < 4; dx++)
                        {
                            var sub = new Tile2i(x + dx, y + dy);
                            if (avoidInfertile && allFertile && !treesManager.IsGroundFertileAtPosition(sub))
                                allFertile = false;
                            if (avoidWithTrees && !anyTree && treesManager.HasTree(new TreeId(sub.AsSlim)))
                                anyTree = true;
                        }
                    }
                    if (!allFertile || anyTree) { scanCount++; continue; }

                    var tile = new Tile2i(x, y);
                    if (avoidMiningDesignations && HasTerrainDesignationAt(tile))
                    {
                        scanCount++;
                        continue;
                    }

                    int hNW = (int)terrMgr.GetHeight(tile).Value.ToFloat();
                    int hNE = (int)terrMgr.GetHeight(tile.AddX(4)).Value.ToFloat();
                    int hSE = (int)terrMgr.GetHeight(tile.AddXy(4)).Value.ToFloat();
                    int hSW = (int)terrMgr.GetHeight(tile.AddY(4)).Value.ToFloat();

                    var data = new DesignationData(tile,
                        new HeightTilesI(hNW), new HeightTilesI(hNE),
                        new HeightTilesI(hSE), new HeightTilesI(hSW));

                    if (s_desigManager.AddOrReplaceDesignation(s_forestryProto, data))
                        designCount++;

                    scanCount++;
                    if (scanCount % GetEffectiveBatchSize() == 0)
                        yield return null;
                }
            }

            LogDebug(string.Format("Created {0} forestry designations", designCount));

            if (markFullyGrown)
                MarkFullyGrownTreesForHarvest(treesManager, area, bbMin, bbMax);
        }

        private static void MarkFullyGrownTreesForHarvest(
            TreesManager treesManager,
            PolygonTerrainArea2i area,
            Tile2i bbMin,
            Tile2i bbMax)
        {
            var currentStep = s_simLoopEvents?.CurrentStep ?? default;
            foreach (var kvp in treesManager.Trees)
            {
                Tile2i pos = kvp.Value.Position2i;
                if (pos.X < bbMin.X || pos.X > bbMax.X || pos.Y < bbMin.Y || pos.Y > bbMax.Y) continue;
                if (!area.ContainsTile(pos)) continue;
                if (kvp.Value.IsFullyGrownAt(currentStep) && !treesManager.IsTreeSelected(kvp.Key))
                    treesManager.AddToHarvest(kvp.Key);
            }
        }

        private static bool HasTerrainDesignationAt(Tile2i origin)
        {
            if (s_desigManager == null) return false;
            return s_desigManager.GetDesignationAt(origin).HasValue;
        }

        internal static void CreateDesignationsForTower(IAreaManagingTower tower)
        {
            s_coroutineHost?.StartCoroutine(CreateDesignationsCoroutine(tower));
        }


        private static int ClampBatchSize(int value)
        {
            return System.Math.Max(1, System.Math.Min(MAX_BATCH_SIZE, value));
        }

        private static int GetEffectiveBatchSize()
        {
            int configuredBatchSize = ClampBatchSize(s_batchSize);
            if (Time.timeScale > 0f)
                return configuredBatchSize;
            long boostedBatchSize = (long)configuredBatchSize * PAUSED_BATCH_MULTIPLIER;
            return (int)System.Math.Min(MAX_BATCH_SIZE, boostedBatchSize);
        }
    }
}
