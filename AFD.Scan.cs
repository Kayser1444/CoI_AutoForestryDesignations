// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
// Auto Forestry Designations - Designation Scanning
using System.Collections;
using System.Collections.Generic;
using Mafi;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Entities;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Terrain.Trees;
using UnityEngine;

namespace AutoForestryDesignations
{
    public static partial class AutoForestryDesignation
    {
        private readonly struct DesignationCandidate
        {
            public Tile2i Origin { get; }
            public DesignationData Data { get; }
            public long DistanceSqrToTower { get; }

            public DesignationCandidate(Tile2i origin, DesignationData data, long distanceSqrToTower)
            {
                Origin = origin;
                Data = data;
                DistanceSqrToTower = distanceSqrToTower;
            }
        }

        private static IEnumerator CreateDesignationsCoroutine(IAreaManagingTower tower)
        {
            if (s_desigManager == null || s_forestryProto == null) yield break;

            var area = tower.Area;
            if (area.IsEmpty) yield break;

            var terrMgr = s_desigManager.TerrainManager;
            var treesManager = s_desigManager.TreesManager;
            var towerSettings = GetOrCreateTowerSettings(tower);
            bool onlyFertile = towerSettings.OnlyFertileTiles;
            bool avoidWithTrees = towerSettings.AvoidTilesWithTrees;
            bool avoidMiningDesignations = towerSettings.AvoidMiningDesignations;
            int maxTiles = towerSettings.MaxTiles;
            bool markHarvestReady = towerSettings.MarkHarvestReadyForHarvest;

            var bbMin = TerrainDesignation.GetOrigin(area.BoundingBoxMin);
            var bbMax = TerrainDesignation.GetOrigin(area.BoundingBoxMax);

            LogDebug(string.Format("Scanning forestry area from {0} to {1} for planting zones...", bbMin, bbMax));

            int designCount = 0;
            int scanCount = 0;
            Tile2i towerPosition = GetTowerPosition(tower, bbMin, bbMax);
            List<DesignationCandidate>? candidates = maxTiles > 0 ? new List<DesignationCandidate>() : null;

            for (int y = bbMin.Y; y <= bbMax.Y; y += 4)
            {
                for (int x = bbMin.X; x <= bbMax.X; x += 4)
                {
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
                            if (onlyFertile && allFertile && !treesManager.IsGroundFertileAtPosition(sub))
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

                    if (candidates != null)
                    {
                        candidates.Add(new DesignationCandidate(tile, data, tile.DistanceSqrTo(towerPosition)));
                    }
                    else if (s_desigManager.AddOrReplaceDesignation(s_forestryProto, data))
                    {
                        designCount++;
                    }

                    scanCount++;
                    if (scanCount % GetEffectiveBatchSize() == 0)
                        yield return null;
                }
            }

            if (candidates != null)
            {
                candidates.Sort((a, b) => a.DistanceSqrToTower.CompareTo(b.DistanceSqrToTower));
                foreach (DesignationCandidate candidate in candidates)
                {
                    if (designCount >= maxTiles)
                        break;
                    if (s_desigManager.AddOrReplaceDesignation(s_forestryProto, candidate.Data))
                        designCount++;
                    if (designCount % GetEffectiveBatchSize() == 0)
                        yield return null;
                }
            }

            LogDebug(string.Format("Created {0} forestry designations", designCount));

            if (markHarvestReady)
                MarkHarvestReadyTreesForHarvest(tower, treesManager, area, bbMin, bbMax);
        }

        private static Tile2i GetTowerPosition(IAreaManagingTower tower, Tile2i bbMin, Tile2i bbMax)
        {
            if (tower is IEntityWithPosition positioned)
                return positioned.Position2f.Tile2i;
            return new Tile2i((bbMin.X + bbMax.X) / 2, (bbMin.Y + bbMax.Y) / 2);
        }

        private static void MarkHarvestReadyTreesForHarvest(
            IAreaManagingTower tower,
            TreesManager treesManager,
            PolygonTerrainArea2i area,
            Tile2i bbMin,
            Tile2i bbMax)
        {
            if (!(tower is ForestryTower forestryTower))
                return;

            foreach (var kvp in treesManager.Trees)
            {
                Tile2i pos = kvp.Value.Position2i;
                if (pos.X < bbMin.X || pos.X > bbMax.X || pos.Y < bbMin.Y || pos.Y > bbMax.Y) continue;
                if (!area.ContainsTile(pos)) continue;
                if (!treesManager.IsTreeSelected(kvp.Key) && forestryTower.IsTreeReadyForHarvest(kvp.Key))
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
