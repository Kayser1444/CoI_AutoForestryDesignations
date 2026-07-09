// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
// Auto Forestry Designations - Designation Scanning
using System.Collections;
using System.Collections.Generic;
using System;
using Mafi;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Entities;
using Mafi.Core.PathFinding;
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
            public int? DrivingDistanceToTower { get; }

            public DesignationCandidate(Tile2i origin, DesignationData data, long distanceSqrToTower, int? drivingDistanceToTower = null)
            {
                Origin = origin;
                Data = data;
                DistanceSqrToTower = distanceSqrToTower;
                DrivingDistanceToTower = drivingDistanceToTower;
            }
        }

        private const int PATHABILITY_SEARCH_MARGIN_TILES = 96;
        private const int MAX_PATHABILITY_SEARCH_TILES = 250000;
        private static readonly RelTile2i[] s_pathabilitySearchDirections =
        {
            new RelTile2i(1, 0),
            new RelTile2i(-1, 0),
            new RelTile2i(0, 1),
            new RelTile2i(0, -1)
        };

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
            bool onlyReachableTiles = towerSettings.OnlyReachableTiles;
            int maxTiles = towerSettings.MaxTiles;
            bool markHarvestReady = towerSettings.MarkHarvestReadyForHarvest;

            var bbMin = TerrainDesignation.GetOrigin(area.BoundingBoxMin);
            var bbMax = TerrainDesignation.GetOrigin(area.BoundingBoxMax);

            LogDebug(string.Format("Scanning forestry area from {0} to {1} for planting zones...", bbMin, bbMax));

            int designCount = 0;
            int scanCount = 0;
            Tile2i towerPosition = GetTowerPosition(tower, bbMin, bbMax);
            bool useCandidatePipeline = maxTiles > 0 || onlyReachableTiles;
            List<DesignationCandidate>? candidates = useCandidatePipeline ? new List<DesignationCandidate>() : null;

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
                            if (onlyFertile && allFertile && treesManager.IsBlockedOrOccupied(sub.AsSlim))
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
                AssignDrivingDistances(candidates, towerPosition, bbMin, bbMax);
                candidates.Sort(CompareCandidatesByDistance);
                bool canEvaluateReachability = s_vehiclePathFindingManager != null && s_standardVehiclePathFindingParams != null;
                bool filterUnreachableCandidates = onlyReachableTiles && canEvaluateReachability;
                int filteredOutCount = 0;
                var placedOrigins = new HashSet<Tile2i>();
                List<DesignationCandidate>? unreachableCandidates =
                    (filterUnreachableCandidates && maxTiles == 0) ? new List<DesignationCandidate>() : null;

                if (onlyReachableTiles && !canEvaluateReachability)
                    Log.Warning("[AFD] Reachable tiles only is enabled, but pathfinding is unavailable; skipping reachability filter for this run.");

                foreach (DesignationCandidate candidate in candidates)
                {
                    if (maxTiles > 0 && designCount >= maxTiles)
                        break;

                    if (filterUnreachableCandidates && !candidate.DrivingDistanceToTower.HasValue)
                    {
                        filteredOutCount++;
                        if (unreachableCandidates != null)
                            unreachableCandidates.Add(candidate);
                        continue;
                    }

                    if (s_desigManager.AddOrReplaceDesignation(s_forestryProto, candidate.Data))
                    {
                        designCount++;
                        placedOrigins.Add(candidate.Origin);
                    }
                    if (designCount % GetEffectiveBatchSize() == 0)
                        yield return null;
                }

                if (unreachableCandidates != null && unreachableCandidates.Count > 0)
                {
                    int filledHoleCount = 0;
                    foreach (DesignationCandidate candidate in unreachableCandidates)
                    {
                        if (!IsInteriorHoleCandidate(candidate.Origin, placedOrigins))
                            continue;

                        if (s_desigManager.AddOrReplaceDesignation(s_forestryProto, candidate.Data))
                        {
                            designCount++;
                            filledHoleCount++;
                            placedOrigins.Add(candidate.Origin);
                        }
                    }

                    if (filledHoleCount > 0)
                        LogDebug(string.Format("Backfilled {0} interior hole designations in unlimited mode", filledHoleCount));
                }

                if (filterUnreachableCandidates && filteredOutCount > 0)
                    LogDebug(string.Format("Skipped {0} unreachable designation candidates", filteredOutCount));
            }

            LogDebug(string.Format("Created {0} forestry designations", designCount));

            if (markHarvestReady)
                MarkHarvestReadyTreesForHarvest(tower, treesManager, area, bbMin, bbMax);

            QueueForestryInfoRefresh(tower);
        }

        private static Tile2i GetTowerPosition(IAreaManagingTower tower, Tile2i bbMin, Tile2i bbMax)
        {
            if (tower is IEntityWithPosition positioned)
                return positioned.Position2f.Tile2i;
            return new Tile2i((bbMin.X + bbMax.X) / 2, (bbMin.Y + bbMax.Y) / 2);
        }

        private static int CompareCandidatesByDistance(DesignationCandidate a, DesignationCandidate b)
        {
            int aDriving = a.DrivingDistanceToTower ?? int.MaxValue;
            int bDriving = b.DrivingDistanceToTower ?? int.MaxValue;
            int drivingComparison = aDriving.CompareTo(bDriving);
            if (drivingComparison != 0)
                return drivingComparison;
            return a.DistanceSqrToTower.CompareTo(b.DistanceSqrToTower);
        }

        private static void AssignDrivingDistances(
            List<DesignationCandidate> candidates,
            Tile2i towerPosition,
            Tile2i bbMin,
            Tile2i bbMax)
        {
            if (candidates.Count == 0 || s_vehiclePathFindingManager == null || s_standardVehiclePathFindingParams == null)
                return;

            IPathabilityProvider pathabilityProvider = s_vehiclePathFindingManager.PathabilityProvider;
            VehiclePathFindingParams pfParams = s_standardVehiclePathFindingParams;

            try
            {
                pathabilityProvider.UpdateChangedTiles();
            }
            catch
            {
            }

            if (!TryFindNearestPathableTile(pathabilityProvider, pfParams, towerPosition, out Tile2i start))
                return;

            var candidateIndexesByTargetTile = BuildCandidateTargetMap(candidates, AutoForestryDesignationsMod.PathabilityTargetSize);
            var candidateDistances = new int?[candidates.Count];
            int foundCount = 0;

            int minX = Math.Min(bbMin.X, towerPosition.X) - PATHABILITY_SEARCH_MARGIN_TILES;
            int minY = Math.Min(bbMin.Y, towerPosition.Y) - PATHABILITY_SEARCH_MARGIN_TILES;
            int maxX = Math.Max(bbMax.X, towerPosition.X) + PATHABILITY_SEARCH_MARGIN_TILES;
            int maxY = Math.Max(bbMax.Y, towerPosition.Y) + PATHABILITY_SEARCH_MARGIN_TILES;

            var distances = new Dictionary<Tile2i, int>();
            var queue = new Queue<Tile2i>();
            distances[start] = 0;
            queue.Enqueue(start);

            while (queue.Count > 0 && distances.Count < MAX_PATHABILITY_SEARCH_TILES && foundCount < candidates.Count)
            {
                Tile2i current = queue.Dequeue();
                int distance = distances[current];

                if (candidateIndexesByTargetTile.TryGetValue(current, out List<int> targetCandidates))
                {
                    foreach (int candidateIndex in targetCandidates)
                    {
                        if (!candidateDistances[candidateIndex].HasValue)
                        {
                            candidateDistances[candidateIndex] = distance;
                            foundCount++;
                        }
                    }
                }

                foreach (RelTile2i direction in s_pathabilitySearchDirections)
                {
                    Tile2i next = current + direction;
                    if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY)
                        continue;
                    if (distances.ContainsKey(next))
                        continue;
                    if (!pathabilityProvider.IsPathable(next, pfParams.PathabilityQueryMask))
                        continue;

                    distances[next] = distance + 1;
                    queue.Enqueue(next);
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidateDistances[i].HasValue)
                {
                    DesignationCandidate candidate = candidates[i];
                    candidates[i] = new DesignationCandidate(
                        candidate.Origin,
                        candidate.Data,
                        candidate.DistanceSqrToTower,
                        candidateDistances[i]);
                }
            }
        }

        private static Dictionary<Tile2i, List<int>> BuildCandidateTargetMap(List<DesignationCandidate> candidates, int targetSize)
        {
            int size = Math.Max(1, targetSize);
            int lowOffset = (size - 1) / 2;
            int highOffset = size / 2;

            var result = new Dictionary<Tile2i, List<int>>();
            for (int i = 0; i < candidates.Count; i++)
            {
                // Use a configurable n*n area around the designation center to tune strictness.
                Tile2i center = candidates[i].Origin.AddXy(2);
                for (int y = -lowOffset; y <= highOffset; y++)
                {
                    for (int x = -lowOffset; x <= highOffset; x++)
                    {
                        Tile2i target = center + new RelTile2i(x, y);
                        if (!result.TryGetValue(target, out List<int> indexes))
                        {
                            indexes = new List<int>();
                            result[target] = indexes;
                        }
                        indexes.Add(i);
                    }
                }
            }
            return result;
        }

        private static bool IsInteriorHoleCandidate(Tile2i origin, HashSet<Tile2i> placedOrigins)
        {
            return placedOrigins.Contains(origin + new RelTile2i(4, 0))
                && placedOrigins.Contains(origin + new RelTile2i(-4, 0))
                && placedOrigins.Contains(origin + new RelTile2i(0, 4))
                && placedOrigins.Contains(origin + new RelTile2i(0, -4));
        }

        internal static bool TryFindNearestPathableTile(
            IPathabilityProvider pathabilityProvider,
            VehiclePathFindingParams pfParams,
            Tile2i origin,
            out Tile2i pathableTile)
        {
            if (pathabilityProvider.IsPathable(origin, pfParams.PathabilityQueryMask))
            {
                pathableTile = origin;
                return true;
            }

            for (int radius = 1; radius <= 24; radius++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (TryUsePathableTile(pathabilityProvider, pfParams, origin + new RelTile2i(-radius, y), out pathableTile)
                        || TryUsePathableTile(pathabilityProvider, pfParams, origin + new RelTile2i(radius, y), out pathableTile))
                        return true;
                }
                for (int x = -radius + 1; x < radius; x++)
                {
                    if (TryUsePathableTile(pathabilityProvider, pfParams, origin + new RelTile2i(x, -radius), out pathableTile)
                        || TryUsePathableTile(pathabilityProvider, pfParams, origin + new RelTile2i(x, radius), out pathableTile))
                        return true;
                }
            }

            pathableTile = origin;
            return false;
        }

        internal static bool TryUsePathableTile(
            IPathabilityProvider pathabilityProvider,
            VehiclePathFindingParams pfParams,
            Tile2i tile,
            out Tile2i pathableTile)
        {
            if (pathabilityProvider.IsPathable(tile, pfParams.PathabilityQueryMask))
            {
                pathableTile = tile;
                return true;
            }
            pathableTile = tile;
            return false;
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

        private static void QueueForestryInfoRefresh(IAreaManagingTower tower)
        {
            if (tower is ForestryTower forestryTower)
                AutoForestryDesignationsTicker.QueueForestryInfoRefresh(forestryTower);
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
