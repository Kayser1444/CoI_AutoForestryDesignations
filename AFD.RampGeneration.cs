// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using UnityEngine;

namespace AutoForestryDesignations
{
    public static partial class AutoDepthDesignation
    {
        private struct RampCandidate
        {
            public Tile2i[] OreTiles;
            public bool[] LaneHasOre;
            public Tile2i Direction;
            public Tile2i[] RampTiles;
            public int[] LaneAttachmentDepths;
            public int Score;

            public RampCandidate(Tile2i[] oreTiles, bool[] laneHasOre, Tile2i direction, Tile2i[] rampTiles, int[] laneAttachmentDepths, int score)
            {
                OreTiles = oreTiles;
                LaneHasOre = laneHasOre;
                Direction = direction;
                RampTiles = rampTiles;
                LaneAttachmentDepths = laneAttachmentDepths;
                Score = score;
            }
        }

        private struct RampStep
        {
            public Tile2i[] Tiles;
            public int[] NwHeights;
            public int[] NeHeights;
            public int[] SeHeights;
            public int[] SwHeights;

            public RampStep(Tile2i[] tiles, int[] nwHeights, int[] neHeights, int[] seHeights, int[] swHeights)
            {
                Tiles = tiles;
                NwHeights = nwHeights;
                NeHeights = neHeights;
                SeHeights = seHeights;
                SwHeights = swHeights;
            }
        }

        private struct RampTilePlan
        {
            public Tile2i Tile;
            public int NwHeight;
            public int NeHeight;
            public int SeHeight;
            public int SwHeight;

            public RampTilePlan(Tile2i tile, int nwHeight, int neHeight, int seHeight, int swHeight)
            {
                Tile = tile;
                NwHeight = nwHeight;
                NeHeight = neHeight;
                SeHeight = seHeight;
                SwHeight = swHeight;
            }
        }

        private struct RampVertexAccumulator
        {
            public int HeightSum;
            public int Samples;
            public bool HasFixedHeight;
            public int FixedHeight;

            public void Add(int height, bool isFixed)
            {
                HeightSum += height;
                Samples++;

                if (!isFixed)
                {
                    return;
                }

                if (!HasFixedHeight)
                {
                    FixedHeight = height;
                    HasFixedHeight = true;
                }
                else
                {
                    FixedHeight = (FixedHeight + height) / 2;
                }
            }
        }

        private struct RampVertexState
        {
            public int Height;
            public bool IsFixed;

            public RampVertexState(int height, bool isFixed)
            {
                Height = height;
                IsFixed = isFixed;
            }
        }

        private struct RampVertexEdge
        {
            public Tile2i A;
            public Tile2i B;

            public RampVertexEdge(Tile2i a, Tile2i b)
            {
                A = a;
                B = b;
            }
        }

        /// <summary>
        /// Absolute world tile coordinates occupied by non-tower static buildings within the current tower area.
        /// Built at the start of each <see cref="CreateAccessRamp"/> call and cleared afterward.
        /// </summary>
        private static readonly HashSet<Tile2i> s_buildingOccupiedTiles = new HashSet<Tile2i>();
        private static string? s_lastRampFailureReason;

        private static void BuildBuildingOccupiedTiles(IAreaManagingTower tower)
        {
            s_buildingOccupiedTiles.Clear();
            if (s_entitiesManager == null)
            {
                return;
            }

            foreach (IStaticEntity entity in s_entitiesManager.GetAllEntitiesOfType<IStaticEntity>())
            {
                Tile2i center = entity.CenterTile.Xy;
                var occupiedTiles = entity.OccupiedTiles;
                for (int i = 0; i < occupiedTiles.Length; i++)
                {
                    Tile2i absCoord = center + occupiedTiles[i].RelCoord;
                    // Only include tiles that are at least potentially within the tower area
                    if (tower.Area.ContainsTile(absCoord))
                    {
                        s_buildingOccupiedTiles.Add(absCoord);
                    }
                }
            }
        }

        private static bool CreateAccessRamp(
            IAreaManagingTower tower,
            Dict<Tile2i, int> tileDepths,
            Dict<Tile2i, int> cornerHeights,
            TerrainManager terrMgr,
            int configuredRampWidth)
        {
            s_lastRampFailureReason = null;

            if (tileDepths.Count == 0 || s_desigManager == null || s_forestryProto == null)
            {
                s_lastRampFailureReason = "No excavation tiles were available for ramp planning.";
                return false;
            }

            BuildBuildingOccupiedTiles(tower);

            int rampWidth = Math.Max(1, Math.Min(5, configuredRampWidth));
            List<RampCandidate> candidates = CollectRampCandidates(tower, tileDepths, rampWidth);
            if (candidates.Count == 0)
            {
                LogDebug(string.Format("No usable {0}-wide ramp space found inside the tower area.", rampWidth));
            }

            if (TryPlaceRampCandidates(tower, tileDepths, cornerHeights, terrMgr, candidates))
            {
                return true;
            }

            int lateralRetryOffset = rampWidth / 2;
            if (rampWidth > 1 && lateralRetryOffset > 0)
            {
                LogDebug(string.Format("Retrying ramp search with a sideways offset of {0} lane(s).", lateralRetryOffset));
                List<RampCandidate> shiftedCandidates = CollectRampCandidates(tower, tileDepths, rampWidth, lateralRetryOffset);
                if (TryPlaceRampCandidates(tower, tileDepths, cornerHeights, terrMgr, shiftedCandidates))
                {
                    return true;
                }
            }

            LogDebug("No valid ramp corridor satisfied the slope and surface rules.");
            s_lastRampFailureReason =
                candidates.Count == 0
                    ? "No valid ramp corridor was found in the tower area. Try lowering ramp width, clearing nearby obstacles, or extending the tower area."
                    : "Ramp candidates were found but none satisfied slope or clearance constraints. Try lowering ramp width, clearing nearby obstacles, or extending the tower area.";
            return false;
        }

        private static bool TryPlaceRampCandidates(IAreaManagingTower tower, Dict<Tile2i, int> tileDepths, Dict<Tile2i, int> cornerHeights, TerrainManager terrMgr, List<RampCandidate> candidates)
        {
            if (candidates.Count == 0)
            {
                return false;
            }

            candidates.Sort((left, right) => left.Score.CompareTo(right.Score));

            foreach (RampCandidate candidate in candidates)
            {
                if (TryPlaceRamp(tower, candidate, tileDepths, cornerHeights, terrMgr))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<RampCandidate> CollectRampCandidates(IAreaManagingTower tower, Dict<Tile2i, int> tileDepths, int rampWidth, int lateralRetryOffset = 0)
        {
            List<RampCandidate> candidates = new List<RampCandidate>();
            Tile2i towerPos;
            if (tower is IEntityWithPosition posEntity)
                towerPos = posEntity.Position2f.Tile2i;
            else
                towerPos = new Tile2i((tower.Area.BoundingBoxMin.X + tower.Area.BoundingBoxMax.X) / 2, (tower.Area.BoundingBoxMin.Y + tower.Area.BoundingBoxMax.Y) / 2);

            foreach (Tile2i oreTile in tileDepths.Keys)
            {
                foreach (Tile2i direction in s_cardinalDirections)
                {
                    Tile2i perpendicular = GetPerpendicular(direction);
                    for (int startOffset = -rampWidth + 1; startOffset <= 0; startOffset++)
                    {
                        Tile2i firstOreTile = Offset(oreTile, Scale(perpendicular, startOffset));
                        if (lateralRetryOffset == 0)
                        {
                            TryAddRampCandidate(candidates, tower, towerPos, tileDepths, firstOreTile, direction, perpendicular, rampWidth);
                        }
                        else
                        {
                            TryAddRampCandidate(
                                candidates,
                                tower,
                                towerPos,
                                tileDepths,
                                Offset(firstOreTile, Scale(perpendicular, -lateralRetryOffset)),
                                direction,
                                perpendicular,
                                rampWidth);
                            TryAddRampCandidate(
                                candidates,
                                tower,
                                towerPos,
                                tileDepths,
                                Offset(firstOreTile, Scale(perpendicular, lateralRetryOffset)),
                                direction,
                                perpendicular,
                                rampWidth);
                        }
                    }
                }
            }

            return candidates;
        }

        private static void TryAddRampCandidate(List<RampCandidate> candidates, IAreaManagingTower tower, Tile2i towerPos, Dict<Tile2i, int> tileDepths, Tile2i firstOreTile, Tile2i direction, Tile2i perpendicular, int rampWidth)
        {
            Tile2i[] edgeOreTiles = new Tile2i[rampWidth];
            bool[] edgeLaneHasOre = new bool[rampWidth];
            int firstOreLane = -1;

            for (int lane = 0; lane < rampWidth; lane++)
            {
                Tile2i oreTile = Offset(firstOreTile, Scale(perpendicular, lane));
                edgeOreTiles[lane] = oreTile;

                if (tileDepths.ContainsKey(oreTile))
                {
                    edgeLaneHasOre[lane] = true;
                    if (firstOreLane < 0)
                    {
                        firstOreLane = lane;
                    }
                }
                else
                {
                    edgeLaneHasOre[lane] = false;
                }
            }

            if (firstOreLane < 0)
            {
                return;
            }

            if (!TryFindRampWidthOreAnchor(edgeOreTiles, direction, tileDepths, rampWidth + 8, rampWidth,
                out Tile2i[] anchorOreTiles, out int[] laneAttachmentDepths))
            {
                return;
            }

            bool[] laneHasOre = Enumerable.Repeat(true, rampWidth).ToArray();
            int maxAttachmentDepth = laneAttachmentDepths.Max();

            Tile2i[] rampTiles = new Tile2i[rampWidth];
            for (int lane = 0; lane < rampWidth; lane++)
            {
                rampTiles[lane] = Offset(anchorOreTiles[lane], Scale(direction, maxAttachmentDepth + 1));
                int rampDepth = Math.Max(1, (maxAttachmentDepth + 1) - laneAttachmentDepths[lane]);
                if (!IsFreeRampTile(tower, rampTiles[lane], tileDepths, rampDepth, direction))
                {
                    return;
                }
            }

            int oreSumX = 0;
            int oreSumY = 0;
            for (int lane = 0; lane < rampWidth; lane++)
            {
                oreSumX += anchorOreTiles[lane].X;
                oreSumY += anchorOreTiles[lane].Y;
            }

            int oreMidX = oreSumX / rampWidth;
            int oreMidY = oreSumY / rampWidth;
            int dxToTower = towerPos.X - oreMidX;
            int dyToTower = towerPos.Y - oreMidY;
            int directionToTower = (towerPos.X - oreMidX) * direction.X + (towerPos.Y - oreMidY) * direction.Y;
            if (directionToTower <= 0)
            {
                return;
            }

            bool isDominantX = Math.Abs(dxToTower) >= Math.Abs(dyToTower);
            if ((isDominantX && direction.Y != 0) || (!isDominantX && direction.X != 0))
            {
                return;
            }

            for (int lane = 0; lane < rampWidth; lane++)
            {
                for (int step = 1; step <= maxAttachmentDepth + 1; step++)
                {
                    Tile2i tile = Offset(anchorOreTiles[lane], Scale(direction, step));

                    if (step <= laneAttachmentDepths[lane])
                    {
                        if (!tileDepths.ContainsKey(tile))
                        {
                            return;
                        }
                    }
                    else
                    {
                        int rampDepth = (maxAttachmentDepth + 2) - step;
                        if (!IsFreeRampTile(tower, tile, tileDepths, rampDepth, direction))
                        {
                            return;
                        }
                    }
                }
            }

            candidates.Add(new RampCandidate(
                anchorOreTiles,
                laneHasOre,
                direction,
                rampTiles,
                laneAttachmentDepths,
                ScoreRampCandidate(towerPos, anchorOreTiles, direction, rampTiles, laneHasOre, laneAttachmentDepths)));
        }

        private static bool TryFindRampWidthOreAnchor(
            Tile2i[] edgeOreTiles,
            Tile2i direction,
            Dict<Tile2i, int> tileDepths,
            int maxInsetSteps,
            int rampWidth,
            out Tile2i[] anchorOreTiles,
            out int[] laneAttachmentDepths)
        {
            int laneCount = edgeOreTiles.Length;
            Tile2i inward = new Tile2i(-direction.X, -direction.Y);
            anchorOreTiles = Array.Empty<Tile2i>();
            laneAttachmentDepths = Array.Empty<int>();
            int bestDepthSpread = int.MaxValue;
            int bestMaxDepth = int.MaxValue;

            for (int inset = 0; inset <= maxInsetSteps; inset++)
            {
                Tile2i[] candidateRow = new Tile2i[laneCount];
                bool allLanesHaveOre = true;

                for (int lane = 0; lane < laneCount; lane++)
                {
                    Tile2i tile = Offset(edgeOreTiles[lane], Scale(inward, inset));
                    candidateRow[lane] = tile;
                    if (!tileDepths.ContainsKey(tile))
                    {
                        allLanesHaveOre = false;
                        break;
                    }
                }

                if (allLanesHaveOre)
                {
                    int[] candidateDepths = new int[laneCount];
                    int minDepth = int.MaxValue;
                    int maxDepth = int.MinValue;
                    for (int lane = 0; lane < laneCount; lane++)
                    {
                        int depth = CountForwardOreDepth(candidateRow[lane], direction, tileDepths);
                        candidateDepths[lane] = depth;
                        minDepth = Math.Min(minDepth, depth);
                        maxDepth = Math.Max(maxDepth, depth);
                    }

                    int depthSpread = maxDepth - minDepth;
                    if (depthSpread < bestDepthSpread || (depthSpread == bestDepthSpread && maxDepth < bestMaxDepth))
                    {
                        bestDepthSpread = depthSpread;
                        bestMaxDepth = maxDepth;
                        anchorOreTiles = candidateRow;
                        laneAttachmentDepths = candidateDepths;
                    }

                    if (depthSpread == 0)
                        break;
                }
            }

            if (anchorOreTiles.Length == 0)
                return false;

            // Wider ramps hit natural ore-front jaggedness more often. The ramp placement logic
            // already uses per-lane face heights and connector tiles, so slightly larger spreads
            // are still handled coherently.
            int maxAllowedDepthSpread = rampWidth >= 5 ? 2 : 1;
            return bestDepthSpread <= maxAllowedDepthSpread;
        }

        private static bool TryPlaceRamp(IAreaManagingTower tower, RampCandidate candidate, Dict<Tile2i, int> tileDepths, Dict<Tile2i, int> cornerHeights, TerrainManager terrMgr)
        {
            int laneCount = candidate.OreTiles.Length;
            int[] laneFirstEdgeHeights = new int[laneCount];
            int[] laneSecondEdgeHeights = new int[laneCount];
            bool[] laneProcessed = new bool[laneCount];

            for (int lane = 0; lane < laneCount; lane++)
            {
                if (!candidate.LaneHasOre[lane])
                {
                    continue;
                }

                // Use the face tile (last ore tile in direction from anchor) so heights match the
                // actual boundary between ore and the first ramp tile, not the interior anchor edge.
                Tile2i faceTile = Offset(candidate.OreTiles[lane],
                    Scale(candidate.Direction, candidate.LaneAttachmentDepths[lane]));
                if (!TryGetOreLaneEdgeHeights(faceTile, candidate.Direction, cornerHeights,
                    out int firstEdgeHeight, out int secondEdgeHeight))
                {
                    return false;
                }

                laneFirstEdgeHeights[lane] = firstEdgeHeight;
                laneSecondEdgeHeights[lane] = secondEdgeHeight;
                laneProcessed[lane] = true;
            }

            // Fill missing lanes from nearest ore lane so every row has a complete boundary.
            for (int lane = 0; lane < laneCount; lane++)
            {
                if (laneProcessed[lane])
                {
                    continue;
                }

                int nearestOreLane = FindNearestOreLane(candidate.LaneHasOre, lane);
                int laneDistance = Math.Abs(lane - nearestOreLane);
                int edgeDrop = Math.Min(2, laneDistance);
                laneFirstEdgeHeights[lane] = Math.Max(1, laneFirstEdgeHeights[nearestOreLane] - edgeDrop);
                laneSecondEdgeHeights[lane] = Math.Max(1, laneSecondEdgeHeights[nearestOreLane] - edgeDrop);
                laneProcessed[lane] = true;
            }

            Tile2i towerPos;
            if (tower is IEntityWithPosition posEntity)
            {
                towerPos = posEntity.Position2f.Tile2i;
            }
            else
            {
                towerPos = new Tile2i(
                    (tower.Area.BoundingBoxMin.X + tower.Area.BoundingBoxMax.X) / 2,
                    (tower.Area.BoundingBoxMin.Y + tower.Area.BoundingBoxMax.Y) / 2);
            }

            int towerReferenceHeight = GetSurfaceHeight(terrMgr, towerPos);
            int[] currentBoundaryHeights = BuildEdgeBoundaryHeights(laneFirstEdgeHeights, laneSecondEdgeHeights);
            Tile2i[] currentTiles = (Tile2i[])candidate.RampTiles.Clone();
            List<RampTilePlan> plannedTiles = new List<RampTilePlan>();
            int maxSteps = Mathf.Max(tileDepths.Count + 32, 32);
            int maxAttachmentDepth = candidate.LaneAttachmentDepths.Max();

            for (int rampStepIndex = 0; rampStepIndex < maxSteps; rampStepIndex++)
            {
                for (int lane = 0; lane < laneCount; lane++)
                {
                    int laneBaseDepth = Math.Max(1, (maxAttachmentDepth + 1) - candidate.LaneAttachmentDepths[lane]);
                    int rampDepth = Math.Max(1, laneBaseDepth - rampStepIndex);
                    if (!IsFreeRampTile(tower, currentTiles[lane], tileDepths, rampDepth, candidate.Direction))
                    {
                        return false;
                    }
                }

                int[] nextBoundaryHeights = new int[laneCount + 1];
                for (int corner = 0; corner < currentBoundaryHeights.Length; corner++)
                {
                    int currentHeight = currentBoundaryHeights[corner];
                    if (currentHeight < towerReferenceHeight)
                    {
                        nextBoundaryHeights[corner] = currentHeight + 1;
                    }
                    else if (currentHeight > towerReferenceHeight)
                    {
                        nextBoundaryHeights[corner] = currentHeight - 1;
                    }
                    else
                    {
                        nextBoundaryHeights[corner] = currentHeight;
                    }
                }

                // Keep side-to-side seams coherent even when one lane starts lower.
                for (int pass = 0; pass < laneCount; pass++)
                {
                    for (int i = 1; i < nextBoundaryHeights.Length; i++)
                    {
                        if (nextBoundaryHeights[i] > nextBoundaryHeights[i - 1] + 1)
                        {
                            nextBoundaryHeights[i] = nextBoundaryHeights[i - 1] + 1;
                        }
                    }

                    for (int i = nextBoundaryHeights.Length - 2; i >= 0; i--)
                    {
                        if (nextBoundaryHeights[i] > nextBoundaryHeights[i + 1] + 1)
                        {
                            nextBoundaryHeights[i] = nextBoundaryHeights[i + 1] + 1;
                        }
                    }
                }

                BuildRowTileHeights(candidate.Direction, currentBoundaryHeights, nextBoundaryHeights,
                    out int[] nwHeights, out int[] neHeights, out int[] seHeights, out int[] swHeights);

                AddRampRowPlans(plannedTiles, currentTiles, nwHeights, neHeights, seHeights, swHeights);

                bool isAboveSurfaceEverywhere = true;
                for (int lane = 0; lane < laneCount; lane++)
                {
                    int rowTopHeight = Math.Max(Math.Max(nwHeights[lane], neHeights[lane]), Math.Max(seHeights[lane], swHeights[lane]));
                    if (rowTopHeight <= GetSurfaceHeight(terrMgr, currentTiles[lane]))
                    {
                        isAboveSurfaceEverywhere = false;
                        break;
                    }
                }

                if (isAboveSurfaceEverywhere)
                {
                    AddGapConnectorPlans(plannedTiles, candidate, laneFirstEdgeHeights, laneSecondEdgeHeights);
                    ApplyRampPlan(plannedTiles, tileDepths, cornerHeights);
                    return true;
                }

                Tile2i[] nextTiles = new Tile2i[laneCount];
                bool allInsideTower = true;
                for (int lane = 0; lane < laneCount; lane++)
                {
                    Tile2i nextTile = Offset(currentTiles[lane], candidate.Direction);
                    nextTiles[lane] = nextTile;
                    if (!IsInsideTowerArea(tower, nextTile))
                    {
                        allInsideTower = false;
                    }
                }

                if (!allInsideTower)
                {
                    AddGapConnectorPlans(plannedTiles, candidate, laneFirstEdgeHeights, laneSecondEdgeHeights);
                    ApplyRampPlan(plannedTiles, tileDepths, cornerHeights);
                    return true;
                }

                for (int lane = 0; lane < laneCount; lane++)
                {
                    int laneBaseDepth = Math.Max(1, (maxAttachmentDepth + 1) - candidate.LaneAttachmentDepths[lane]);
                    int rampDepth = Math.Max(1, laneBaseDepth - (rampStepIndex + 1));
                    if (!IsFreeRampTile(tower, nextTiles[lane], tileDepths, rampDepth, candidate.Direction))
                    {
                        return false;
                    }
                }

                currentBoundaryHeights = nextBoundaryHeights;
                currentTiles = nextTiles;
            }

            return false;
        }

        private static void AddRampRowPlans(List<RampTilePlan> plannedTiles, Tile2i[] tiles, int[] nwHeights, int[] neHeights, int[] seHeights, int[] swHeights)
        {
            for (int lane = 0; lane < tiles.Length; lane++)
            {
                plannedTiles.Add(new RampTilePlan(tiles[lane], nwHeights[lane], neHeights[lane], seHeights[lane], swHeights[lane]));
            }
        }

        private static void AddGapConnectorPlans(List<RampTilePlan> plannedTiles, RampCandidate candidate, int[] laneFirstEdgeHeights, int[] laneSecondEdgeHeights)
        {
            int maxAttachDepth = candidate.LaneAttachmentDepths.Max();
            if (maxAttachDepth == 0) return; // all lanes flush — no gap possible

            int[] rampBoundaryHeights = BuildEdgeBoundaryHeights(laneFirstEdgeHeights, laneSecondEdgeHeights);

            int laneCount = candidate.LaneAttachmentDepths.Length;
            for (int lane = 0; lane < laneCount; lane++)
            {
                int laneDepth = candidate.LaneAttachmentDepths[lane];
                for (int step = laneDepth + 1; step <= maxAttachDepth; step++)
                {
                    Tile2i gapTile = Offset(candidate.OreTiles[lane], Scale(candidate.Direction, step));
                    plannedTiles.Add(CreateGapConnectorPlan(
                        gapTile,
                        candidate.Direction,
                        laneFirstEdgeHeights[lane],
                        laneSecondEdgeHeights[lane],
                        rampBoundaryHeights[lane],
                        rampBoundaryHeights[lane + 1]));
                }
            }
        }

        private static RampTilePlan CreateGapConnectorPlan(
            Tile2i tile,
            Tile2i direction,
            int backFirstHeight,
            int backSecondHeight,
            int frontFirstHeight,
            int frontSecondHeight)
        {
            int nwHeight;
            int neHeight;
            int seHeight;
            int swHeight;

            if (direction.X > 0)
            {
                nwHeight = backFirstHeight;
                neHeight = frontFirstHeight;
                seHeight = frontSecondHeight;
                swHeight = backSecondHeight;
            }
            else if (direction.X < 0)
            {
                nwHeight = frontFirstHeight;
                neHeight = backFirstHeight;
                seHeight = backSecondHeight;
                swHeight = frontSecondHeight;
            }
            else if (direction.Y > 0)
            {
                nwHeight = backFirstHeight;
                neHeight = backSecondHeight;
                seHeight = frontSecondHeight;
                swHeight = frontFirstHeight;
            }
            else
            {
                nwHeight = frontFirstHeight;
                neHeight = frontSecondHeight;
                seHeight = backSecondHeight;
                swHeight = backFirstHeight;
            }

            return new RampTilePlan(tile, nwHeight, neHeight, seHeight, swHeight);
        }

        private static void ApplyRampPlan(List<RampTilePlan> plannedTiles, Dict<Tile2i, int> tileDepths, Dict<Tile2i, int> cornerHeights)
        {
            NormalizeRampPlan(plannedTiles, tileDepths, cornerHeights);

            foreach (RampTilePlan plannedTile in plannedTiles)
            {
                PlaceDesignation(plannedTile.Tile, plannedTile.NwHeight, plannedTile.NeHeight, plannedTile.SeHeight, plannedTile.SwHeight);
            }
        }

        private static void NormalizeRampPlan(List<RampTilePlan> plannedTiles, Dict<Tile2i, int> tileDepths, Dict<Tile2i, int> cornerHeights)
        {
            if (plannedTiles.Count == 0)
            {
                return;
            }

            var plannedTileSet = new HashSet<Tile2i>(plannedTiles.Select(tile => tile.Tile));
            var vertexAccumulators = new Dictionary<Tile2i, RampVertexAccumulator>();
            var vertexEdges = new List<RampVertexEdge>(plannedTiles.Count * 4);

            foreach (RampTilePlan plannedTile in plannedTiles)
            {
                GetTileCornerCoordinates(plannedTile.Tile, out Tile2i nwCorner, out Tile2i neCorner, out Tile2i seCorner, out Tile2i swCorner);

                bool touchesWestOre = BordersOreDesignation(plannedTile.Tile.AddX(-4), plannedTileSet, tileDepths);
                bool touchesEastOre = BordersOreDesignation(plannedTile.Tile.AddX(4), plannedTileSet, tileDepths);
                bool touchesNorthOre = BordersOreDesignation(plannedTile.Tile.AddY(-4), plannedTileSet, tileDepths);
                bool touchesSouthOre = BordersOreDesignation(plannedTile.Tile.AddY(4), plannedTileSet, tileDepths);

                AddVertexCandidate(vertexAccumulators, nwCorner, GetAnchoredCornerHeight(cornerHeights, nwCorner, plannedTile.NwHeight), touchesWestOre || touchesNorthOre);
                AddVertexCandidate(vertexAccumulators, neCorner, GetAnchoredCornerHeight(cornerHeights, neCorner, plannedTile.NeHeight), touchesEastOre || touchesNorthOre);
                AddVertexCandidate(vertexAccumulators, seCorner, GetAnchoredCornerHeight(cornerHeights, seCorner, plannedTile.SeHeight), touchesEastOre || touchesSouthOre);
                AddVertexCandidate(vertexAccumulators, swCorner, GetAnchoredCornerHeight(cornerHeights, swCorner, plannedTile.SwHeight), touchesWestOre || touchesSouthOre);

                vertexEdges.Add(new RampVertexEdge(nwCorner, neCorner));
                vertexEdges.Add(new RampVertexEdge(neCorner, seCorner));
                vertexEdges.Add(new RampVertexEdge(seCorner, swCorner));
                vertexEdges.Add(new RampVertexEdge(swCorner, nwCorner));
            }

            var vertexStates = new Dictionary<Tile2i, RampVertexState>(vertexAccumulators.Count);
            foreach (KeyValuePair<Tile2i, RampVertexAccumulator> kvp in vertexAccumulators)
            {
                RampVertexAccumulator accumulator = kvp.Value;
                int initialHeight = accumulator.HasFixedHeight ? accumulator.FixedHeight : accumulator.HeightSum / Math.Max(1, accumulator.Samples);
                vertexStates[kvp.Key] = new RampVertexState(initialHeight, accumulator.HasFixedHeight);
            }

            RelaxRampVertexHeights(vertexStates, vertexEdges);

            for (int index = 0; index < plannedTiles.Count; index++)
            {
                RampTilePlan plannedTile = plannedTiles[index];
                GetTileCornerCoordinates(plannedTile.Tile, out Tile2i nwCorner, out Tile2i neCorner, out Tile2i seCorner, out Tile2i swCorner);
                plannedTile.NwHeight = vertexStates[nwCorner].Height;
                plannedTile.NeHeight = vertexStates[neCorner].Height;
                plannedTile.SeHeight = vertexStates[seCorner].Height;
                plannedTile.SwHeight = vertexStates[swCorner].Height;
                plannedTiles[index] = plannedTile;
            }
        }

        private static bool BordersOreDesignation(Tile2i neighborTile, HashSet<Tile2i> plannedTileSet, Dict<Tile2i, int> tileDepths)
        {
            return !plannedTileSet.Contains(neighborTile) && tileDepths.ContainsKey(neighborTile);
        }

        private static int GetAnchoredCornerHeight(Dict<Tile2i, int> cornerHeights, Tile2i corner, int fallbackHeight)
        {
            int anchoredHeight;
            return cornerHeights.TryGetValue(corner, out anchoredHeight) ? anchoredHeight : fallbackHeight;
        }

        private static void AddVertexCandidate(Dictionary<Tile2i, RampVertexAccumulator> vertexAccumulators, Tile2i vertex, int height, bool isFixed)
        {
            RampVertexAccumulator accumulator;
            if (vertexAccumulators.TryGetValue(vertex, out accumulator))
            {
                accumulator.Add(height, isFixed);
            }
            else
            {
                accumulator = new RampVertexAccumulator();
                accumulator.Add(height, isFixed);
            }

            vertexAccumulators[vertex] = accumulator;
        }

        private static void RelaxRampVertexHeights(Dictionary<Tile2i, RampVertexState> vertexStates, List<RampVertexEdge> vertexEdges)
        {
            const int maxIterations = 512;

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool changed = false;

                foreach (RampVertexEdge edge in vertexEdges)
                {
                    RampVertexState a = vertexStates[edge.A];
                    RampVertexState b = vertexStates[edge.B];

                    int diff = a.Height - b.Height;
                    if (Math.Abs(diff) <= 1)
                    {
                        continue;
                    }

                    changed = true;
                    if (diff > 1)
                    {
                        ClampVertexPair(ref a, ref b);
                    }
                    else
                    {
                        ClampVertexPair(ref b, ref a);
                    }

                    vertexStates[edge.A] = a;
                    vertexStates[edge.B] = b;
                }

                if (!changed)
                {
                    return;
                }
            }
        }

        private static void ClampVertexPair(ref RampVertexState higher, ref RampVertexState lower)
        {
            if (higher.Height <= lower.Height + 1)
            {
                return;
            }

            if (higher.IsFixed && !lower.IsFixed)
            {
                lower.Height = higher.Height - 1;
                return;
            }

            if (!higher.IsFixed && lower.IsFixed)
            {
                higher.Height = lower.Height + 1;
                return;
            }

            if (higher.IsFixed && lower.IsFixed)
            {
                higher.Height = lower.Height + 1;
                return;
            }

            higher.Height--;
            if (higher.Height > lower.Height + 1)
            {
                lower.Height++;
            }
        }

        private static void GetTileCornerCoordinates(Tile2i tile, out Tile2i nwCorner, out Tile2i neCorner, out Tile2i seCorner, out Tile2i swCorner)
        {
            nwCorner = tile;
            neCorner = tile.AddX(4);
            seCorner = tile.AddXy(4);
            swCorner = tile.AddY(4);
        }

        private static void PlaceDesignation(Tile2i tile, int nwHeight, int neHeight, int seHeight, int swHeight)
        {
            if (s_desigManager == null || s_forestryProto == null)
            {
                return;
            }

            DesignationData data = new DesignationData(tile,
                new HeightTilesI(nwHeight), new HeightTilesI(neHeight),
                new HeightTilesI(seHeight), new HeightTilesI(swHeight));

            if (!s_desigManager.AddOrReplaceDesignation(s_forestryProto, data))
            {
                Log.Warning(string.Format("Failed to create ramp designation for tile {0}", tile));
            }
        }

        private static bool IsFreeRampTile(IAreaManagingTower tower, Tile2i tile, Dict<Tile2i, int> tileDepths, int rampDepth, Tile2i rampDirection)
        {
            if (!IsInsideTowerArea(tower, tile))
                return false;
            if (tileDepths.ContainsKey(tile))
                return false;
            if (DoesTileOverlapBuildingFootprint(tile, rampDepth, rampDirection))
                return false;
            return true;
        }

        private static bool DoesTileOverlapBuildingFootprint(Tile2i tile, int rampDepth, Tile2i rampDirection)
        {
            // A designation tile covers world tiles from (tile.X, tile.Y) to (tile.X+3, tile.Y+3).
            // The requested safety margin is 1 world tile per depth.
            // Apply margin only perpendicular to the ramp direction, not along the ramp axis.
            int safeDepth = Math.Max(1, rampDepth);
            int marginWorldTiles = safeDepth;

            int minDx = 0;
            int maxDx = 3;
            int minDy = 0;
            int maxDy = 3;

            if (rampDirection.X != 0)
            {
                // Ramp goes east-west, so expand only north-south.
                minDy = -marginWorldTiles;
                maxDy = 3 + marginWorldTiles;
            }
            else
            {
                // Ramp goes north-south, so expand only east-west.
                minDx = -marginWorldTiles;
                maxDx = 3 + marginWorldTiles;
            }

            for (int dx = minDx; dx <= maxDx; dx++)
            {
                for (int dy = minDy; dy <= maxDy; dy++)
                {
                    if (s_buildingOccupiedTiles.Contains(new Tile2i(tile.X + dx, tile.Y + dy)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsInsideTowerArea(IAreaManagingTower tower, Tile2i tile)
        {
            return tower.Area.ContainsTile(tile)
                && tower.Area.ContainsTile(tile.AddX(3))
                && tower.Area.ContainsTile(tile.AddY(3))
                && tower.Area.ContainsTile(tile.AddXy(3));
        }

        private static int ScoreRampCandidate(Tile2i towerPos, Tile2i[] oreTiles, Tile2i direction, Tile2i[] rampTiles, bool[] laneHasOre, int[] laneAttachmentDepths)
        {
            int rampSumX = 0;
            int rampSumY = 0;
            for (int i = 0; i < rampTiles.Length; i++)
            {
                rampSumX += rampTiles[i].X;
                rampSumY += rampTiles[i].Y;
            }

            int oreSumX = 0;
            int oreSumY = 0;
            for (int i = 0; i < oreTiles.Length; i++)
            {
                oreSumX += oreTiles[i].X;
                oreSumY += oreTiles[i].Y;
            }

            int midX = rampSumX / rampTiles.Length;
            int midY = rampSumY / rampTiles.Length;
            int dx = towerPos.X - midX;
            int dy = towerPos.Y - midY;
            int distanceScore = dx * dx + dy * dy;

            int oreMidX = oreSumX / oreTiles.Length;
            int oreMidY = oreSumY / oreTiles.Length;
            int alignmentScore = (towerPos.X - oreMidX) * direction.X + (towerPos.Y - oreMidY) * direction.Y;

            int oreLaneCount = 0;
            int attachmentDepthSum = 0;
            for (int i = 0; i < laneHasOre.Length; i++)
            {
                if (laneHasOre[i])
                {
                    oreLaneCount++;
                    attachmentDepthSum += laneAttachmentDepths[i];
                }
            }

            // Prefer broader ore contact. Penalise interior anchors (higher D) so face candidates (D=0)
            // are always tried first - they produce correctly-connected ramps.
            int oreConnectivityBonus = oreLaneCount * 250;
            return distanceScore - alignmentScore * 8 - oreConnectivityBonus + attachmentDepthSum * 30;
        }

        private static bool TryGetOreLaneEdgeHeights(Tile2i oreTile, Tile2i direction, Dict<Tile2i, int> cornerHeights, out int firstHeight, out int secondHeight)
        {
            firstHeight = 0;
            secondHeight = 0;
            Tile2i firstCorner;
            Tile2i secondCorner;

            if (direction.X > 0)
            {
                firstCorner = oreTile.AddX(4);
                secondCorner = oreTile.AddXy(4);
            }
            else if (direction.X < 0)
            {
                firstCorner = oreTile;
                secondCorner = oreTile.AddY(4);
            }
            else if (direction.Y > 0)
            {
                firstCorner = oreTile.AddY(4);
                secondCorner = oreTile.AddXy(4);
            }
            else
            {
                firstCorner = oreTile;
                secondCorner = oreTile.AddX(4);
            }

            if (!cornerHeights.TryGetValue(firstCorner, out firstHeight) ||
                !cornerHeights.TryGetValue(secondCorner, out secondHeight))
            {
                return false;
            }

            return true;
        }

        private static int CountForwardOreDepth(Tile2i origin, Tile2i direction, Dict<Tile2i, int> tileDepths)
        {
            int depth = 0;
            Tile2i current = origin;

            while (true)
            {
                Tile2i next = Offset(current, direction);
                if (!tileDepths.ContainsKey(next))
                {
                    return depth;
                }

                depth++;
                current = next;
            }
        }

        private static int FindNearestOreLane(bool[] laneHasOre, int lane)
        {
            int bestLane = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < laneHasOre.Length; i++)
            {
                if (!laneHasOre[i])
                {
                    continue;
                }

                int distance = Math.Abs(i - lane);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLane = i;
                }
            }

            return bestLane >= 0 ? bestLane : lane;
        }

        private static int[] BuildBoundaryHeights(int[] laneHeights)
        {
            int[] boundaryHeights = new int[laneHeights.Length + 1];
            boundaryHeights[0] = laneHeights[0];
            boundaryHeights[laneHeights.Length] = laneHeights[laneHeights.Length - 1];

            for (int i = 1; i < laneHeights.Length; i++)
            {
                boundaryHeights[i] = Math.Max(laneHeights[i - 1], laneHeights[i]);
            }

            return boundaryHeights;
        }

        private static int[] BuildEdgeBoundaryHeights(int[] firstEdgeHeights, int[] secondEdgeHeights)
        {
            int laneCount = firstEdgeHeights.Length;
            int[] boundaryHeights = new int[laneCount + 1];
            boundaryHeights[0] = firstEdgeHeights[0];
            boundaryHeights[laneCount] = secondEdgeHeights[laneCount - 1];

            for (int i = 1; i < laneCount; i++)
            {
                boundaryHeights[i] = Math.Max(secondEdgeHeights[i - 1], firstEdgeHeights[i]);
            }

            return boundaryHeights;
        }

        private static void BuildRowTileHeights(Tile2i direction, int[] backBoundaryHeights, int[] frontBoundaryHeights,
            out int[] nwHeights, out int[] neHeights, out int[] seHeights, out int[] swHeights)
        {
            int laneCount = backBoundaryHeights.Length - 1;
            nwHeights = new int[laneCount];
            neHeights = new int[laneCount];
            seHeights = new int[laneCount];
            swHeights = new int[laneCount];

            for (int lane = 0; lane < laneCount; lane++)
            {
                if (direction.X > 0)
                {
                    nwHeights[lane] = backBoundaryHeights[lane];
                    neHeights[lane] = frontBoundaryHeights[lane];
                    seHeights[lane] = frontBoundaryHeights[lane + 1];
                    swHeights[lane] = backBoundaryHeights[lane + 1];
                }
                else if (direction.X < 0)
                {
                    nwHeights[lane] = frontBoundaryHeights[lane];
                    neHeights[lane] = backBoundaryHeights[lane];
                    seHeights[lane] = backBoundaryHeights[lane + 1];
                    swHeights[lane] = frontBoundaryHeights[lane + 1];
                }
                else if (direction.Y > 0)
                {
                    nwHeights[lane] = backBoundaryHeights[lane];
                    neHeights[lane] = backBoundaryHeights[lane + 1];
                    seHeights[lane] = frontBoundaryHeights[lane + 1];
                    swHeights[lane] = frontBoundaryHeights[lane];
                }
                else
                {
                    nwHeights[lane] = frontBoundaryHeights[lane];
                    neHeights[lane] = frontBoundaryHeights[lane + 1];
                    seHeights[lane] = backBoundaryHeights[lane + 1];
                    swHeights[lane] = backBoundaryHeights[lane];
                }
            }
        }

        private static int GetSurfaceHeight(TerrainManager terrMgr, Tile2i tile)
        {
            return (int)Math.Floor(terrMgr.GetHeight(tile).Value.ToFloat());
        }

        private static Tile2i GetPerpendicular(Tile2i direction)
        {
            return direction.X != 0 ? new Tile2i(0, 4) : new Tile2i(4, 0);
        }

        private static Tile2i Scale(Tile2i delta, int factor)
        {
            return new Tile2i(delta.X * factor, delta.Y * factor);
        }
    }
}
