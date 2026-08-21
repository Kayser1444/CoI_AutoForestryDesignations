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
using System.Diagnostics;
using Mafi;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Entities;
using Mafi.Core.Input;
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
        private const int TARGET_ESTIMATE_STEP_CHUNK = 64;
        private const int TARGET_PLANNING_PLAY_BUDGET_MS = 10;
        private const int TARGET_PLANNING_PAUSED_BUDGET_MS = 30;
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
            ForestryTower? forestryTower = tower as ForestryTower;
            bool onlyFertile = towerSettings.OnlyFertileTiles;
            bool avoidWithTrees = towerSettings.AvoidTilesWithTrees;
            bool avoidFlatTiles = towerSettings.AvoidFlatTiles;
            bool onlyReachableTiles = towerSettings.OnlyReachableTiles;
            int targetYield = towerSettings.TargetYield;
            int legacyMaxTiles = targetYield > 0 ? 0 : towerSettings.MaxTiles;
            bool markHarvestReady = towerSettings.MarkHarvestReadyForHarvest;

            var bbMin = TerrainDesignation.GetOrigin(area.BoundingBoxMin);
            var bbMax = TerrainDesignation.GetOrigin(area.BoundingBoxMax);

            LogDebug(string.Format("Scanning forestry area from {0} to {1} for planting zones...", bbMin, bbMax));
            if (targetYield > 0 && forestryTower != null)
                ForestryInfoPanel.LogTargetYieldSnapshot("scan-start", forestryTower, treesManager);

            int designCount = 0;
            int scanCount = 0;
            Tile2i towerPosition = GetTowerPosition(tower, bbMin, bbMax);
            // Avoid-flat is an eligibility filter and does not require candidate
            // collection, driving-distance search, or deferred placement by itself.
            bool useCandidatePipeline = ShouldUseCandidatePipeline(
                targetYield,
                legacyMaxTiles,
                onlyReachableTiles);
            List<DesignationCandidate>? candidates = useCandidatePipeline ? new List<DesignationCandidate>() : null;
            var pendingDesignations = new List<DesignationData>();
            Stopwatch candidateScanSlice = Stopwatch.StartNew();

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
                    if (!inArea)
                    {
                        scanCount++;
                        if (ShouldYieldDesignationScan(candidateScanSlice, scanCount))
                        {
                            yield return null;
                            candidateScanSlice.Restart();
                        }
                        continue;
                    }

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
                    if (!allFertile || anyTree)
                    {
                        scanCount++;
                        if (ShouldYieldDesignationScan(candidateScanSlice, scanCount))
                        {
                            yield return null;
                            candidateScanSlice.Restart();
                        }
                        continue;
                    }

                    var tile = new Tile2i(x, y);
                    if (!AutoForestryDesignationsMod.OverrideTerrainDesignations && HasTerrainDesignationAt(tile))
                    {
                        scanCount++;
                        if (ShouldYieldDesignationScan(candidateScanSlice, scanCount))
                        {
                            yield return null;
                            candidateScanSlice.Restart();
                        }
                        continue;
                    }

                    HeightTilesF heightNW = terrMgr.GetHeight(tile);
                    HeightTilesF heightNE = terrMgr.GetHeight(tile.AddX(4));
                    HeightTilesF heightSE = terrMgr.GetHeight(tile.AddXy(4));
                    HeightTilesF heightSW = terrMgr.GetHeight(tile.AddY(4));

                    if (avoidFlatTiles && IsFlatAtIntegerHeight(heightNW, heightNE, heightSE, heightSW))
                    {
                        scanCount++;
                        if (ShouldYieldDesignationScan(candidateScanSlice, scanCount))
                        {
                            yield return null;
                            candidateScanSlice.Restart();
                        }
                        continue;
                    }

                    int hNW = (int)heightNW.Value.ToFloat();
                    int hNE = (int)heightNE.Value.ToFloat();
                    int hSE = (int)heightSE.Value.ToFloat();
                    int hSW = (int)heightSW.Value.ToFloat();

                    var data = new DesignationData(tile,
                        new HeightTilesI(hNW), new HeightTilesI(hNE),
                        new HeightTilesI(hSE), new HeightTilesI(hSW));

                    if (candidates != null)
                    {
                        candidates.Add(new DesignationCandidate(tile, data, tile.DistanceSqrTo(towerPosition)));
                    }
                    else
                    {
                        pendingDesignations.Add(data);
                    }

                    scanCount++;
                    if (ShouldYieldDesignationScan(candidateScanSlice, scanCount))
                    {
                        yield return null;
                            candidateScanSlice.Restart();
                    }
                }
            }

            if (candidates != null)
            {
                yield return AssignDrivingDistancesCoroutine(
                    candidates,
                    towerPosition,
                    bbMin,
                    bbMax);
                candidates.Sort(CompareCandidatesByDistance);
                bool canEvaluateReachability = s_vehiclePathFindingManager != null && s_standardVehiclePathFindingParams != null;
                bool filterUnreachableCandidates = onlyReachableTiles && canEvaluateReachability;
                int filteredOutCount = 0;
                var placedOrigins = new HashSet<Tile2i>();
                List<DesignationCandidate>? unreachableCandidates =
                    (filterUnreachableCandidates && targetYield == 0 && legacyMaxTiles == 0) ? new List<DesignationCandidate>() : null;

                int projectedYield = 0;
                List<DesignationCandidate>? targetCandidates =
                    targetYield > 0 ? new List<DesignationCandidate>() : null;

                if (onlyReachableTiles && !canEvaluateReachability)
                    s_log.Warning("Reachable tiles only is enabled, but pathfinding is unavailable; skipping reachability filter for this run.");

                int filteredCandidateCount = 0;
                Stopwatch filterSlice = Stopwatch.StartNew();
                foreach (DesignationCandidate candidate in candidates)
                {
                    if (legacyMaxTiles > 0 && pendingDesignations.Count >= legacyMaxTiles)
                        break;

                    if (filterUnreachableCandidates && !candidate.DrivingDistanceToTower.HasValue)
                    {
                        filteredOutCount++;
                        if (unreachableCandidates != null)
                            unreachableCandidates.Add(candidate);
                        filteredCandidateCount++;
                        if (ShouldYieldCandidatePipeline(
                            filterSlice,
                            targetYield,
                            filteredCandidateCount))
                        {
                            yield return null;
                            filterSlice.Restart();
                        }
                        continue;
                    }

                    if (targetYield > 0)
                    {
                        targetCandidates!.Add(candidate);
                    }
                    else
                    {
                        pendingDesignations.Add(candidate.Data);
                        placedOrigins.Add(candidate.Origin);
                    }
                    filteredCandidateCount++;
                    if (ShouldYieldCandidatePipeline(
                        filterSlice,
                        targetYield,
                        filteredCandidateCount))
                    {
                        yield return null;
                        filterSlice.Restart();
                    }
                }

                if (targetYield > 0 && forestryTower == null)
                {
                    s_log.Warning("Target yield is set, but the tower is not a forestry tower; no designations were placed for this run.");
                }
                else if (targetYield > 0 && targetCandidates!.Count > 0)
                {
                    var baselineResult = new ProjectedYieldEstimateResult();
                    yield return RunProjectedYieldEstimate(
                        forestryTower!,
                        treesManager,
                        null,
                        baselineResult);
                    if (!baselineResult.Succeeded)
                    {
                        s_log.Warning("Target yield could not be estimated for this tower; no designations were placed for this run.");
                    }
                    else
                    {
                        projectedYield = baselineResult.SustainableWoodPerMonth;
                        ForestryInfoPanel.ProjectedYieldEstimateWork projection = baselineResult.Work!;
                        ForestryInfoPanel.LogTargetYieldSnapshot(
                            "scan-after-baseline-projection",
                            forestryTower!,
                            treesManager,
                            projection);
                        int placementSlices = 0;
                        double placementProcessingMilliseconds = 0d;
                        Stopwatch placementSlice = Stopwatch.StartNew();

                        if (projectedYield < targetYield && !projection.CanAddDesignations)
                        {
                            s_log.Warning("Target yield cannot be increased because no planting tree type is configured; no designations were placed for this run.");
                        }
                        else
                        {
                            foreach (DesignationCandidate candidate in targetCandidates)
                            {
                                if (projectedYield >= targetYield)
                                    break;
                                if (!projection.TryAddDesignation(candidate.Origin))
                                {
                                    s_log.Warning("Target yield projection failed while planning a designation; stopping below target.");
                                    break;
                                }
                                pendingDesignations.Add(candidate.Data);
                                placedOrigins.Add(candidate.Origin);
                                projectedYield = projection.SustainableWoodPerMonth;

                                if (placementSlice.ElapsedMilliseconds
                                    >= GetTargetPlanningSliceBudgetMilliseconds())
                                {
                                    placementSlice.Stop();
                                    placementProcessingMilliseconds += placementSlice.Elapsed.TotalMilliseconds;
                                    placementSlices++;
                                    yield return null;
                                    placementSlice.Restart();
                                }
                            }
                        }

                        ForestryInfoPanel.LogTargetYieldSnapshot(
                            "scan-after-target-planning",
                            forestryTower!,
                            treesManager,
                            projection);

                        placementSlice.Stop();
                        placementProcessingMilliseconds += placementSlice.Elapsed.TotalMilliseconds;
                        LogDebug(string.Format(
                            "Target yield planner performance: baselineMs={0:F1} placementMs={1:F1} slices={2} candidates={3} capacity={4}",
                            baselineResult.ProcessingMilliseconds,
                            placementProcessingMilliseconds,
                            baselineResult.Slices + placementSlices,
                            targetCandidates.Count,
                            projection.TreeCapacity));
                    }
                }

                if (unreachableCandidates != null && unreachableCandidates.Count > 0)
                {
                    int filledHoleCount = 0;
                    foreach (DesignationCandidate candidate in unreachableCandidates)
                    {
                        if (!IsInteriorHoleCandidate(candidate.Origin, placedOrigins))
                            continue;

                        pendingDesignations.Add(candidate.Data);
                        filledHoleCount++;
                        placedOrigins.Add(candidate.Origin);
                    }

                    if (filledHoleCount > 0)
                        LogDebug(string.Format("Backfilled {0} interior hole designations in unlimited mode", filledHoleCount));
                }

                if (filterUnreachableCandidates && filteredOutCount > 0)
                    LogDebug(string.Format("Skipped {0} unreachable designation candidates", filteredOutCount));

                if (targetYield > 0)
                    LogDebug(string.Format("Target yield planning: target={0} projected={1} newDesignations={2}", targetYield, projectedYield, pendingDesignations.Count));
            }

            designCount = pendingDesignations.Count;
            if (designCount > 0)
                yield return CommitDesignationsCoroutine(pendingDesignations);

            if (targetYield > 0 && forestryTower != null)
                ForestryInfoPanel.LogTargetYieldSnapshot("scan-after-commit", forestryTower, treesManager);

            LogDebug(string.Format("Created {0} forestry designations", designCount));

            if (markHarvestReady)
                MarkHarvestReadyTreesForHarvest(tower, treesManager, area, bbMin, bbMax);

            QueueForestryInfoRefresh(tower);
        }

        private sealed class ProjectedYieldEstimateResult
        {
            public bool Succeeded { get; set; }
            public int SustainableWoodPerMonth { get; set; }
            public ForestryInfoPanel.ProjectedYieldEstimateWork? Work { get; set; }
            public double ProcessingMilliseconds { get; set; }
            public int Slices { get; set; }
        }

        private static IEnumerator RunProjectedYieldEstimate(
            ForestryTower tower,
            TreesManager treesManager,
            IEnumerable<Tile2i>? additionalDesignationOrigins,
            ProjectedYieldEstimateResult result)
        {
            ForestryInfoPanel.ProjectedYieldEstimateWork work =
                ForestryInfoPanel.BeginProjectedYieldEstimate(
                    tower,
                    treesManager,
                    additionalDesignationOrigins);
            while (!work.IsComplete)
            {
                Stopwatch sliceTimer = Stopwatch.StartNew();
                do
                {
                    work.Step(TARGET_ESTIMATE_STEP_CHUNK);
                }
                while (!work.IsComplete
                    && sliceTimer.ElapsedMilliseconds
                        < GetTargetPlanningSliceBudgetMilliseconds());
                sliceTimer.Stop();
                result.ProcessingMilliseconds += sliceTimer.Elapsed.TotalMilliseconds;
                result.Slices++;
                if (!work.IsComplete)
                    yield return null;
            }

            result.Succeeded = work.Succeeded;
            result.SustainableWoodPerMonth = work.SustainableWoodPerMonth;
            result.Work = work;
        }

        private static int GetTargetPlanningSliceBudgetMilliseconds()
            => Time.timeScale <= 0f
                ? TARGET_PLANNING_PAUSED_BUDGET_MS
                : TARGET_PLANNING_PLAY_BUDGET_MS;

        private static bool ShouldUseCandidatePipeline(
            int targetYield,
            int legacyMaxTiles,
            bool onlyReachableTiles)
            => targetYield > 0 || legacyMaxTiles > 0 || onlyReachableTiles;

        private static bool ShouldYieldDesignationScan(
            Stopwatch? planningSlice,
            int scanCount)
            => planningSlice != null
                && planningSlice.ElapsedMilliseconds
                    >= GetTargetPlanningSliceBudgetMilliseconds();

        private static bool ShouldYieldCandidatePipeline(
            Stopwatch planningSlice,
            int targetYield,
            int processedCandidateCount)
            => planningSlice.ElapsedMilliseconds
                >= GetTargetPlanningSliceBudgetMilliseconds();

        private static IEnumerator CommitDesignationsCoroutine(List<DesignationData> designations)
        {
            if (s_forestryProto == null || designations.Count == 0)
                yield break;

            if (s_inputScheduler != null)
            {
                AddTerrainDesignationsCmd command = s_inputScheduler.ScheduleInputCmd(
                    new AddTerrainDesignationsCmd(
                        s_forestryProto.Id,
                        designations.ToImmutableArray()));
                while (!command.IsProcessed)
                    yield return null;
                yield break;
            }

            s_log.Error("Input scheduler is unavailable; forestry designations were not committed.");
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

        private static IEnumerator AssignDrivingDistancesCoroutine(
            List<DesignationCandidate> candidates,
            Tile2i towerPosition,
            Tile2i bbMin,
            Tile2i bbMax)
        {
            if (candidates.Count == 0 || s_vehiclePathFindingManager == null || s_standardVehiclePathFindingParams == null)
                yield break;

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
                yield break;

            int targetSize = Math.Max(1, AutoForestryDesignationsMod.PathabilityTargetSize);
            int lowOffset = (targetSize - 1) / 2;
            int highOffset = targetSize / 2;
            var candidateIndexesByTargetTile = new Dictionary<Tile2i, List<int>>();
            Stopwatch sliceTimer = Stopwatch.StartNew();
            for (int i = 0; i < candidates.Count; i++)
            {
                Tile2i center = candidates[i].Origin.AddXy(2);
                for (int y = -lowOffset; y <= highOffset; y++)
                {
                    for (int x = -lowOffset; x <= highOffset; x++)
                    {
                        Tile2i target = center + new RelTile2i(x, y);
                        if (!candidateIndexesByTargetTile.TryGetValue(target, out List<int>? indexes))
                        {
                            indexes = new List<int>();
                            candidateIndexesByTargetTile[target] = indexes;
                        }
                        indexes.Add(i);
                    }
                }

                if (sliceTimer.ElapsedMilliseconds
                    >= GetTargetPlanningSliceBudgetMilliseconds())
                {
                    yield return null;
                    sliceTimer.Restart();
                }
            }

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

            int visitedSinceBudgetCheck = 0;
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

                visitedSinceBudgetCheck++;
                if (visitedSinceBudgetCheck >= TARGET_ESTIMATE_STEP_CHUNK
                    && sliceTimer.ElapsedMilliseconds
                        >= GetTargetPlanningSliceBudgetMilliseconds())
                {
                    visitedSinceBudgetCheck = 0;
                    yield return null;
                    sliceTimer.Restart();
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

                if (sliceTimer.ElapsedMilliseconds
                    >= GetTargetPlanningSliceBudgetMilliseconds())
                {
                    yield return null;
                    sliceTimer.Restart();
                }
            }
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

        /// <summary>
        /// Matches the game's flat-surface heuristic: all designation vertices must be
        /// within the vanilla surface tolerance of one shared integer height.
        /// </summary>
        internal static bool IsFlatAtIntegerHeight(
            HeightTilesF heightNW,
            HeightTilesF heightNE,
            HeightTilesF heightSE,
            HeightTilesF heightSW)
        {
            HeightTilesF integerHeight = heightNW.TilesHeightRounded.HeightTilesF;
            HeightTilesF tolerance = TerrainDesignation.SURFACE_HEIGHT_TOLERANCE;
            return heightNW.IsNear(integerHeight, tolerance)
                && heightNE.IsNear(integerHeight, tolerance)
                && heightSE.IsNear(integerHeight, tolerance)
                && heightSW.IsNear(integerHeight, tolerance);
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

    }
}
