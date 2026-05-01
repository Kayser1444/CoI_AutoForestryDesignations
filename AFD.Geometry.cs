// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
// Auto Forestry Designations - Spatial Geometry: Flood Fill, Hull, Smoothing
using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Terrain.Resources;

namespace AutoForestryDesignations
{
    public static partial class AutoDepthDesignation
    {
        private static void FilterIsolatedDesignations(
            Dict<Tile2i, int> maxOreDepths,
            HashSet<string> targetProductIds,
            Dictionary<Tile2i, List<ProductResource>> resourceDetailsByTile,
            int purityLevel)
        {
            if (maxOreDepths.Count == 0)
                return;

            int clampedPurityLevel = Math.Max(0, Math.Min(4, purityLevel));
            int minComponentSize = s_minComponentSizeByLevel[clampedPurityLevel];

            // Find all connected components
            var visited = new HashSet<Tile2i>();
            var components = new List<List<Tile2i>>();

            foreach (var tile in maxOreDepths.Keys)
            {
                if (!visited.Contains(tile))
                {
                    var component = new List<Tile2i>();
                    FloodFill(tile, maxOreDepths, visited, component);
                    components.Add(component);
                }
            }

            if (components.Count <= 1)
                return; // No isolated components to prune

            // Always keep the largest component; prune the rest by size + ore height
            var mainComponent = components.OrderByDescending(c => c.Count).First();

            var tilesToRemove = new List<Tile2i>();
            foreach (var component in components)
            {
                if (component == mainComponent)
                    continue;

                // Size-based filter (aggressiveness scales with purity level)
                if (minComponentSize > 0 && component.Count < minComponentSize)
                {
                    tilesToRemove.AddRange(component);
                    continue;
                }

                // Ore-height filter
                if (IsIsolatedComponentBelowThreshold(component, targetProductIds, resourceDetailsByTile, clampedPurityLevel))
                    tilesToRemove.AddRange(component);
            }

            foreach (var tile in tilesToRemove)
                maxOreDepths.Remove(tile);

            if (tilesToRemove.Count > 0)
                LogDebug(string.Format("Filtered out {0} tiles from isolated designations below threshold", tilesToRemove.Count));
        }

        /// <summary>
        /// Connects surviving secondary components to the main (largest) component
        /// by adding fixed-width rectilinear corridors and then filling enclosed holes.
        /// </summary>
        private static void FillRectilinearHull(
            Dict<Tile2i, int> maxOreDepths,
            HashSet<string> targetProductIds,
            Dictionary<Tile2i, List<ProductResource>> resourceDetailsByTile,
            int corridorClearance)
        {
            if (maxOreDepths.Count == 0)
                return;

            // Re-compute components after filtering
            var visited = new HashSet<Tile2i>();
            var components = new List<List<Tile2i>>();
            foreach (var tile in maxOreDepths.Keys)
            {
                if (!visited.Contains(tile))
                {
                    var comp = new List<Tile2i>();
                    FloodFill(tile, maxOreDepths, visited, comp);
                    components.Add(comp);
                }
            }

            if (corridorClearance > 0 && components.Count > 1)
            {
                // Largest first
                components.Sort((a, b) => b.Count.CompareTo(a.Count));

                // Start with main component
                var connectedSet = new HashSet<Tile2i>(components[0]);

                // Connect each secondary to the growing connected set
                for (int i = 1; i < components.Count; i++)
                {
                    var secondary = components[i];

                    // Find closest tile pair between connected and secondary (by squared distance)
                    Tile2i bestA = default, bestB = default;
                    long bestDist = long.MaxValue;

                    foreach (var a in connectedSet)
                    {
                        foreach (var b in secondary)
                        {
                            long dx = (long)(a.X - b.X);
                            long dy = (long)(a.Y - b.Y);
                            long dist = dx * dx + dy * dy;
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestA = a;
                                bestB = b;
                            }
                        }
                    }

                    // Draw L-shaped rectilinear path: X-align first, then Y-align
                    var pathTiles = ComputeRectilinearPath(bestA, bestB);
                    int pathCount = AddFixedWidthHullPath(pathTiles, maxOreDepths, connectedSet);

                    // For clearance=2, square off single-tile notches at the junction
                    // where the new corridor spine meets an existing designation body.
                    //
                    // When the spine ends at a tile that already has 3 of its 4 anchor
                    // neighbours present (cost=1), the one missing tile is always safe to
                    // add: it completes a 2×2 block whose other 3 corners are already in
                    // the designation.  This prevents the "staircase inner-corner notch"
                    // that appears at every corridor-to-body attachment point.
                    if (corridorClearance >= 2)
                    {
                        foreach (var pathTile in pathTiles)
                        {
                            Tile2i[] jAnchors = {
                                new Tile2i(pathTile.X,     pathTile.Y    ),
                                new Tile2i(pathTile.X - 4, pathTile.Y    ),
                                new Tile2i(pathTile.X,     pathTile.Y - 4),
                                new Tile2i(pathTile.X - 4, pathTile.Y - 4)
                            };
                            foreach (var ja in jAnchors)
                            {
                                if (IsFullTwoByTwoAtAnchor(ja, maxOreDepths)) continue;
                                if (CountMissingInAnchor(ja, maxOreDepths) != 1) continue;
                                GetTwoByTwoBlock(ja, out Tile2i j00, out Tile2i j10, out Tile2i j01, out Tile2i j11);
                                Tile2i jMissing = !maxOreDepths.ContainsKey(j00) ? j00
                                               : !maxOreDepths.ContainsKey(j10) ? j10
                                               : !maxOreDepths.ContainsKey(j01) ? j01 : j11;
                                maxOreDepths[jMissing] = NearestDepth(jMissing, maxOreDepths);
                                connectedSet.Add(jMissing);
                            }
                        }
                    }

                    // Add secondary component to connected set
                    foreach (var t in secondary)
                        connectedSet.Add(t);

                    LogDebug(string.Format(
                        "Connected component ({0} tiles) via {1}-tile corridor",
                        secondary.Count,
                        pathCount));
                }
            }

            if (corridorClearance > 0)
            {
                int interiorFilled = FillInteriorHoles(maxOreDepths);
                if (interiorFilled > 0)
                    LogDebug(string.Format("Filled {0} enclosed interior hull tiles", interiorFilled));
            }

            if (corridorClearance >= 2)
            {
                int widthEnforced = EnforceMinimumClearanceTwo(maxOreDepths);
                if (widthEnforced > 0)
                    LogDebug(string.Format("Added {0} tiles to enforce minimum clearance=2 across the full designation", widthEnforced));

                // Clearance expansion can create new enclosed holes; fill them.
                int postFilled = FillInteriorHoles(maxOreDepths);
                if (postFilled > 0)
                    LogDebug(string.Format("Filled {0} enclosed interior tiles after clearance expansion", postFilled));
            }
        }

        /// <summary>
        /// Ensures every tile in the designation is reachable by a 2×2 brush (clearance=2),
        /// adding the minimum number of extra tiles needed.
        ///
        /// Concepts:
        ///   Anchor – the lower-left corner of a 2×2 block.  An anchor at (ax,ay) covers
        ///     tiles t00=(ax,ay), t10=(ax+4,ay), t01=(ax,ay+4), t11=(ax+4,ay+4).
        ///   Passable anchor – one where all 4 tiles are present in maxOreDepths.
        ///   A tile is "covered" iff at least one of its 4 containing anchors is passable.
        ///   The designation is traversable iff every tile is covered AND the graph of
        ///   passable anchors (edges between cardinally adjacent anchors) is connected.
        ///
        /// Three-phase algorithm:
        ///   Phase 1 – Coverage: single pass over all original tiles; for each uncovered
        ///     tile, activate the cheapest containing anchor (fewest missing tiles).
        ///   Phase 1.5 – Adjacency: for every pair of cardinal-adjacent tiles that share
        ///     no passable anchor, activate the cheaper of the two shared anchors.  Loops
        ///     until stable.  This fixes "mouth" junctions where a corridor meets an ore
        ///     body at an edge the previous phase chose a non-overlapping anchor for.
        ///   Phase 2 – Connectivity: Dijkstra from the largest passable-anchor component
        ///     across all anchor positions in the expanded bounding box (edge cost =
        ///     missing tiles per anchor).  When another component is reached, activate
        ///     anchors along the cheapest path.  Repeats until one component remains.
        /// </summary>
        private static int EnforceMinimumClearanceTwo(Dict<Tile2i, int> maxOreDepths)
        {
            if (maxOreDepths.Count == 0)
                return 0;

            int totalAdded = 0;

            // ── Phase 1: Coverage ─────────────────────────────────────────────────────
            // For every original tile that isn't covered by any passable anchor, activate
            // the cheapest candidate anchor (fewest missing tiles).  One pass suffices
            // because tiles added for earlier entries are immediately visible.
            var originalTiles = new HashSet<Tile2i>(maxOreDepths.Keys);
            foreach (var tile in originalTiles)
            {
                if (IsTileCovered(tile, maxOreDepths))
                    continue;

                // Pick the cheapest (fewest missing tiles) of the 4 candidate anchors.
                Tile2i bestAnchor = default;
                int fewestMissing = int.MaxValue;
                var candidateAnchors = new Tile2i[]
                {
                    new Tile2i(tile.X,     tile.Y    ),
                    new Tile2i(tile.X - 4, tile.Y    ),
                    new Tile2i(tile.X,     tile.Y - 4),
                    new Tile2i(tile.X - 4, tile.Y - 4),
                };
                foreach (var anchor in candidateAnchors)
                {
                    int missing = CountMissingInAnchor(anchor, maxOreDepths);
                    if (missing < fewestMissing)
                    {
                        fewestMissing = missing;
                        bestAnchor = anchor;
                    }
                }

                var toAdd = new Dict<Tile2i, int>();
                AddMissingTilesForAnchor(bestAnchor, maxOreDepths, toAdd);
                foreach (var kvp in toAdd)
                    maxOreDepths[kvp.Key] = kvp.Value;
                totalAdded += toAdd.Count;
            }

            // ── Phase 1.5: Adjacency passability ─────────────────────────────────────
            // For every pair of cardinal-adjacent tiles, ensure they share a passable anchor.
            bool adjacencyChanged = true;
            while (adjacencyChanged)
            {
                adjacencyChanged = false;
                var tilesNow = maxOreDepths.Keys.ToList();
                foreach (var tile in tilesNow)
                {
                    foreach (var dir in s_cardinalDirections)
                    {
                        var nb = Offset(tile, dir);
                        if (!maxOreDepths.ContainsKey(nb)) continue;

                        Tile2i shared1, shared2;
                        if (dir.X != 0)
                        {
                            int minX2 = Math.Min(tile.X, nb.X);
                            shared1 = new Tile2i(minX2, tile.Y);
                            shared2 = new Tile2i(minX2, tile.Y - 4);
                        }
                        else
                        {
                            int minY2 = Math.Min(tile.Y, nb.Y);
                            shared1 = new Tile2i(tile.X, minY2);
                            shared2 = new Tile2i(tile.X - 4, minY2);
                        }

                        if (IsFullTwoByTwoAtAnchor(shared1, maxOreDepths) ||
                            IsFullTwoByTwoAtAnchor(shared2, maxOreDepths))
                            continue;

                        int m1 = CountMissingInAnchor(shared1, maxOreDepths);
                        int m2 = CountMissingInAnchor(shared2, maxOreDepths);
                        var chosen = m1 <= m2 ? shared1 : shared2;
                        var toAdd2 = new Dict<Tile2i, int>();
                        AddMissingTilesForAnchor(chosen, maxOreDepths, toAdd2);
                        if (toAdd2.Count > 0)
                        {
                            foreach (var kvp in toAdd2)
                                maxOreDepths[kvp.Key] = kvp.Value;
                            totalAdded += toAdd2.Count;
                            adjacencyChanged = true;
                        }
                    }
                }
            }

            // ── Phase 2: Connectivity ─────────────────────────────────────────────────
            // Build a graph of passable anchors (cardinal adjacency).  If there are
            // multiple connected components, run Dijkstra from the largest component
            // outward through all anchor positions in the bounding box (edge cost =
            // missing tiles per anchor).  Activate anchors on the cheapest path when
            // another component is reached.  Repeat until the graph is fully connected.
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var tile in maxOreDepths.Keys)
            {
                if (tile.X < minX) minX = tile.X;
                if (tile.X > maxX) maxX = tile.X;
                if (tile.Y < minY) minY = tile.Y;
                if (tile.Y > maxY) maxY = tile.Y;
            }
            minX -= 4; minY -= 4; maxX += 4; maxY += 4;

            while (true)
            {
                // Collect all fully-passable anchors.
                var passable = new HashSet<Tile2i>();
                foreach (var tile in maxOreDepths.Keys)
                {
                    var cas = new Tile2i[]
                    {
                        new Tile2i(tile.X,     tile.Y    ),
                        new Tile2i(tile.X - 4, tile.Y    ),
                        new Tile2i(tile.X,     tile.Y - 4),
                        new Tile2i(tile.X - 4, tile.Y - 4),
                    };
                    foreach (var a in cas)
                        if (IsFullTwoByTwoAtAnchor(a, maxOreDepths))
                            passable.Add(a);
                }

                if (passable.Count == 0)
                    break;

                // BFS to label connected components of passable anchors.
                var componentOf = new Dictionary<Tile2i, int>();
                int numComponents = 0;
                foreach (var start in passable)
                {
                    if (componentOf.ContainsKey(start)) continue;
                    var bfsQ = new Queue<Tile2i>();
                    bfsQ.Enqueue(start);
                    componentOf[start] = numComponents;
                    while (bfsQ.Count > 0)
                    {
                        var cur = bfsQ.Dequeue();
                        foreach (var dir in s_cardinalDirections)
                        {
                            var nb = Offset(cur, dir);
                            if (passable.Contains(nb) && !componentOf.ContainsKey(nb))
                            {
                                componentOf[nb] = numComponents;
                                bfsQ.Enqueue(nb);
                            }
                        }
                    }
                    numComponents++;
                }

                if (numComponents <= 1)
                    break;

                // Largest component = Dijkstra source.
                var compSizes = new Dictionary<int, int>();
                foreach (var kvp in componentOf)
                    compSizes[kvp.Value] = (compSizes.ContainsKey(kvp.Value) ? compSizes[kvp.Value] : 0) + 1;
                int srcComp = -1, srcSize = -1;
                foreach (var kvp in compSizes)
                    if (kvp.Value > srcSize) { srcSize = kvp.Value; srcComp = kvp.Key; }

                // Dijkstra: seed with source-component passable anchors (cost 0).
                // Expand to any adjacent anchor within bounding box.
                // Stop when a passable anchor in another component is dequeued.
                var dist = new Dictionary<Tile2i, int>();
                var prev = new Dictionary<Tile2i, Tile2i>();
                var pq = new SortedSet<(int cost, int x, int y)>();
                foreach (var anchor in passable)
                {
                    if (componentOf[anchor] != srcComp) continue;
                    dist[anchor] = 0;
                    pq.Add((0, anchor.X, anchor.Y));
                }

                bool found = false;
                Tile2i target = default;
                while (pq.Count > 0)
                {
                    var entry = pq.Min;
                    pq.Remove(entry);
                    var (curCost, cx, cy) = entry;
                    var cur = new Tile2i(cx, cy);

                    if (dist.TryGetValue(cur, out int knownCost) && knownCost < curCost)
                        continue; // stale

                    if (componentOf.TryGetValue(cur, out int cid) && cid != srcComp)
                    {
                        target = cur;
                        found = true;
                        break;
                    }

                    foreach (var dir in s_cardinalDirections)
                    {
                        var nb = Offset(cur, dir);
                        if (nb.X < minX || nb.X > maxX || nb.Y < minY || nb.Y > maxY)
                            continue;
                        int stepCost = CountMissingInAnchor(nb, maxOreDepths);
                        int newCost = curCost + stepCost;
                        if (!dist.TryGetValue(nb, out int oldCost) || newCost < oldCost)
                        {
                            dist[nb] = newCost;
                            prev[nb] = cur;
                            pq.Add((newCost, nb.X, nb.Y));
                        }
                    }
                }

                if (!found)
                    break;

                // Trace back and activate non-passable anchors on the path.
                var pathPos = target;
                while (true)
                {
                    if (!IsFullTwoByTwoAtAnchor(pathPos, maxOreDepths))
                    {
                        var toAdd = new Dict<Tile2i, int>();
                        AddMissingTilesForAnchor(pathPos, maxOreDepths, toAdd);
                        foreach (var kvp in toAdd)
                            maxOreDepths[kvp.Key] = kvp.Value;
                        totalAdded += toAdd.Count;
                    }
                    if (!prev.TryGetValue(pathPos, out Tile2i prevPos))
                        break;
                    pathPos = prevPos;
                }
            }

            return totalAdded;
        }

        private static bool IsTileCovered(Tile2i tile, Dict<Tile2i, int> maxOreDepths)
        {
            if (IsFullTwoByTwoAtAnchor(new Tile2i(tile.X,     tile.Y    ), maxOreDepths)) return true;
            if (IsFullTwoByTwoAtAnchor(new Tile2i(tile.X - 4, tile.Y    ), maxOreDepths)) return true;
            if (IsFullTwoByTwoAtAnchor(new Tile2i(tile.X,     tile.Y - 4), maxOreDepths)) return true;
            if (IsFullTwoByTwoAtAnchor(new Tile2i(tile.X - 4, tile.Y - 4), maxOreDepths)) return true;
            return false;
        }


        private static int CountMissingInAnchor(Tile2i anchor, Dict<Tile2i, int> maxOreDepths)
        {
            GetTwoByTwoBlock(anchor, out Tile2i t00, out Tile2i t10, out Tile2i t01, out Tile2i t11);
            int missing = 0;
            if (!maxOreDepths.ContainsKey(t00)) missing++;
            if (!maxOreDepths.ContainsKey(t10)) missing++;
            if (!maxOreDepths.ContainsKey(t01)) missing++;
            if (!maxOreDepths.ContainsKey(t11)) missing++;
            return missing;
        }

        private static bool IsFullTwoByTwoAtAnchor(Tile2i anchor, Dict<Tile2i, int> maxOreDepths)
        {
            GetTwoByTwoBlock(anchor, out Tile2i t00, out Tile2i t10, out Tile2i t01, out Tile2i t11);
            return maxOreDepths.ContainsKey(t00) && maxOreDepths.ContainsKey(t10)
                && maxOreDepths.ContainsKey(t01) && maxOreDepths.ContainsKey(t11);
        }

        private static void AddMissingTilesForAnchor(
            Tile2i anchor,
            Dict<Tile2i, int> maxOreDepths,
            Dict<Tile2i, int> toAdd)
        {
            GetTwoByTwoBlock(anchor, out Tile2i t00, out Tile2i t10, out Tile2i t01, out Tile2i t11);
            TryAdd(t00);
            TryAdd(t10);
            TryAdd(t01);
            TryAdd(t11);

            void TryAdd(Tile2i tile)
            {
                if (maxOreDepths.ContainsKey(tile) || toAdd.ContainsKey(tile))
                    return;

                // For clearance-added tiles: if any cardinal edge has no designated neighbour
                // (i.e. the tile is at the lateral edge of the widened corridor), use the
                // shallowest (highest-value) neighbour depth instead of copying the nearest
                // ore depth.  BuildAndSmoothCornerHeights will then slope-clamp the tile
                // just deep enough to stay within maxAllowedDiff of the ore — effectively
                // tilting the clearance edge upward and minimising unnecessary excavation.
                bool hasFreeEdge = false;
                int shallowest = int.MinValue;
                foreach (var dir in s_cardinalDirections)
                {
                    var nb = Offset(tile, dir);
                    int d;
                    if (maxOreDepths.TryGetValue(nb, out d) || toAdd.TryGetValue(nb, out d))
                    {
                        if (d > shallowest) shallowest = d;
                    }
                    else
                    {
                        hasFreeEdge = true;
                    }
                }

                toAdd[tile] = (hasFreeEdge && shallowest != int.MinValue)
                    ? shallowest
                    : NearestDepth(tile, maxOreDepths);
            }
        }

        private static void GetTwoByTwoBlock(
            Tile2i anchor,
            out Tile2i t00,
            out Tile2i t10,
            out Tile2i t01,
            out Tile2i t11)
        {
            t00 = anchor;
            t10 = new Tile2i(anchor.X + 4, anchor.Y);
            t01 = new Tile2i(anchor.X, anchor.Y + 4);
            t11 = new Tile2i(anchor.X + 4, anchor.Y + 4);
        }

        private static Tile2i SelectBestCandidateByTargetProduct(
            Tile2i first,
            Tile2i second,
            HashSet<string> targetProductIds,
            Dictionary<Tile2i, List<ProductResource>> resourceDetailsByTile,
            Dict<Tile2i, int> maxOreDepths)
        {
            float firstAmount = GetTargetProductAmount(first, targetProductIds, resourceDetailsByTile);
            float secondAmount = GetTargetProductAmount(second, targetProductIds, resourceDetailsByTile);

            if (firstAmount > secondAmount)
                return first;
            if (secondAmount > firstAmount)
                return second;

            // Tie-break with neighbor density for deterministic, coherent shapes.
            return CountExistingNeighbors(first, maxOreDepths) >= CountExistingNeighbors(second, maxOreDepths)
                ? first
                : second;
        }

        private static float GetTargetProductAmount(
            Tile2i tile,
            HashSet<string> targetProductIds,
            Dictionary<Tile2i, List<ProductResource>> resourceDetailsByTile)
        {
            if (!resourceDetailsByTile.TryGetValue(tile, out var resources))
                return 0f;

            return GetTargetProductAmount(resources, targetProductIds);
        }

        private static float GetTargetProductAmount(
            List<ProductResource> resources,
            HashSet<string> targetProductIds)
        {
            if (resources == null || resources.Count == 0)
                return 0f;

            float total = 0f;
            for (int i = 0; i < resources.Count; i++)
            {
                ProductResource resource = resources[i];
                if (targetProductIds.Contains(resource.Product.Id.ToString()))
                    total += resource.Height.Value.ToFloat();
            }

            return total;
        }

        private static int CountExistingNeighbors(Tile2i tile, Dict<Tile2i, int> tiles)
        {
            int count = 0;
            if (tiles.ContainsKey(new Tile2i(tile.X, tile.Y + 4))) count++;
            if (tiles.ContainsKey(new Tile2i(tile.X, tile.Y - 4))) count++;
            if (tiles.ContainsKey(new Tile2i(tile.X + 4, tile.Y))) count++;
            if (tiles.ContainsKey(new Tile2i(tile.X - 4, tile.Y))) count++;
            return count;
        }

        // Lays a single-tile-wide spine along pathTiles.  For clearance=2 the corridor
        // is widened later by EnforceMinimumClearanceTwo; stamping 2×2 blocks here would
        // cause double-widening because those tiles seed the clearance algorithm.
        private static int AddFixedWidthHullPath(List<Tile2i> pathTiles, Dict<Tile2i, int> maxOreDepths, HashSet<Tile2i> connectedSet)
        {
            int added = 0;
            for (int i = 0; i < pathTiles.Count; i++)
                AddHullTile(pathTiles[i], maxOreDepths, connectedSet, ref added);
            return added;
        }

        private static void AddHullTile(
            Tile2i tile,
            Dict<Tile2i, int> maxOreDepths,
            HashSet<Tile2i> connectedSet,
            ref int added)
        {
            if (!maxOreDepths.ContainsKey(tile))
            {
                maxOreDepths[tile] = NearestDepth(tile, maxOreDepths);
                added++;
            }

            connectedSet.Add(tile);
        }

        private static int FillInteriorHoles(Dict<Tile2i, int> maxOreDepths)
        {
            var visited = new HashSet<Tile2i>();
            var holesToAdd = new HashSet<Tile2i>();

            foreach (var tile in maxOreDepths.Keys)
            {
                if (visited.Contains(tile))
                    continue;

                var component = new List<Tile2i>();
                FloodFill(tile, maxOreDepths, visited, component);

                int minX = int.MaxValue;
                int maxX = int.MinValue;
                int minY = int.MaxValue;
                int maxY = int.MinValue;

                var componentSet = new HashSet<Tile2i>();
                foreach (var compTile in component)
                {
                    componentSet.Add(compTile);
                    if (compTile.X < minX) minX = compTile.X;
                    if (compTile.X > maxX) maxX = compTile.X;
                    if (compTile.Y < minY) minY = compTile.Y;
                    if (compTile.Y > maxY) maxY = compTile.Y;
                }

                var exteriorEmpty = new HashSet<Tile2i>();
                var queue = new Queue<Tile2i>();

                for (int y = minY; y <= maxY; y += 4)
                {
                    for (int x = minX; x <= maxX; x += 4)
                    {
                        var coord = new Tile2i(x, y);
                        if (componentSet.Contains(coord))
                            continue;

                        bool isBoundary = x == minX || x == maxX || y == minY || y == maxY;
                        if (isBoundary && exteriorEmpty.Add(coord))
                            queue.Enqueue(coord);
                    }
                }

                while (queue.Count > 0)
                {
                    Tile2i empty = queue.Dequeue();
                    foreach (var direction in s_cardinalDirections)
                    {
                        Tile2i neighbor = Offset(empty, direction);
                        if (neighbor.X < minX || neighbor.X > maxX || neighbor.Y < minY || neighbor.Y > maxY)
                            continue;
                        if (componentSet.Contains(neighbor))
                            continue;
                        if (exteriorEmpty.Add(neighbor))
                            queue.Enqueue(neighbor);
                    }
                }

                for (int y = minY; y <= maxY; y += 4)
                {
                    for (int x = minX; x <= maxX; x += 4)
                    {
                        var coord = new Tile2i(x, y);
                        if (componentSet.Contains(coord))
                            continue;
                        if (exteriorEmpty.Contains(coord))
                            continue;
                        holesToAdd.Add(coord);
                    }
                }
            }

            int added = 0;
            foreach (var holeTile in holesToAdd)
            {
                if (maxOreDepths.ContainsKey(holeTile))
                    continue;

                maxOreDepths[holeTile] = NearestDepth(holeTile, maxOreDepths);
                added++;
            }

            return added;
        }

        /// <summary>
        /// Computes an L-shaped rectilinear path from A to B:
        /// first moves horizontally (X-axis) to align, then vertically (Y-axis) to reach B.
        /// Returns all designation-grid tiles along the path (world coordinates, 4-unit steps).
        /// </summary>
        private static List<Tile2i> ComputeRectilinearPath(Tile2i from, Tile2i to)
        {
            var path = new List<Tile2i>();

            int x = from.X, y = from.Y;
            int tx = to.X, ty = to.Y;

            // Phase 1: move X towards target X (in 4-unit steps)
            int xStep = x < tx ? 4 : (x > tx ? -4 : 0);
            while (x != tx)
            {
                var tile = new Tile2i(x, y);
                if (!path.Contains(tile))
                    path.Add(tile);
                x += xStep;
            }

            // Phase 2: move Y towards target Y (in 4-unit steps)
            int yStep = y < ty ? 4 : (y > ty ? -4 : 0);
            while (y != ty)
            {
                var tile = new Tile2i(x, y);
                if (!path.Contains(tile))
                    path.Add(tile);
                y += yStep;
            }

            // Ensure target is included
            var targetTile = new Tile2i(tx, ty);
            if (!path.Contains(targetTile))
                path.Add(targetTile);

            return path;
        }

        private static int NearestDepth(Tile2i query, Dict<Tile2i, int> existing)
        {
            int best = 0;
            long bestDist = long.MaxValue;
            foreach (var kvp in existing)
            {
                long dx = (long)(kvp.Key.X - query.X);
                long dy = (long)(kvp.Key.Y - query.Y);
                long d = dx * dx + dy * dy;
                if (d < bestDist) { bestDist = d; best = kvp.Value; }
            }
            return best;
        }

        private static void FloodFill(Tile2i start, Dict<Tile2i, int> tiles, HashSet<Tile2i> visited, List<Tile2i> component)
        {
            var queue = new Queue<Tile2i>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                // Check all 4 cardinal directions (4-tile grid offsets)
                foreach (var direction in s_cardinalDirections)
                {
                    var neighbor = Offset(current, direction);
                    if (tiles.ContainsKey(neighbor) && !visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        private static void FloodFillOrigins(Tile2i start, HashSet<Tile2i> origins, HashSet<Tile2i> visited, List<Tile2i> component)
        {
            var queue = new Queue<Tile2i>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                foreach (var direction in s_cardinalDirections)
                {
                    var neighbor = Offset(current, direction);
                    if (origins.Contains(neighbor) && !visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        private static bool IsIsolatedComponentBelowThreshold(
            List<Tile2i> component,
            HashSet<string> targetProductIds,
            Dictionary<Tile2i, List<ProductResource>> resourceDetailsByTile,
            int purityLevel)
        {
            // Check if ALL tiles in this component have ore height < 1.0
            foreach (var tile in component)
            {
                if (!resourceDetailsByTile.TryGetValue(tile, out var resources))
                    continue;

                float tileOreHeight = GetTargetProductAmount(resources, targetProductIds);

                float minOreHeight = s_minOreHeightByLevel[purityLevel];
                if (minOreHeight <= 0f || tileOreHeight >= minOreHeight)
                {
                    // At least one tile has sufficient ore height, keep this component
                    return false;
                }
            }

            // All tiles have insufficient ore height, remove this component
            return true;
        }

        private static Dict<Tile2i, int> BuildAndSmoothCornerHeights(Dict<Tile2i, int> tileDepths, int maxAllowedDiff = 1)
        {
            var corners = new Dict<Tile2i, int>();

            // Build initial corner heights by averaging tile depths at shared corners
            foreach (var kvp in tileDepths)
            {
                var tile = kvp.Key;
                var depth = kvp.Value;

                UpdateCornerHeight(corners, tile,          depth);
                UpdateCornerHeight(corners, tile.AddX(4),  depth);
                UpdateCornerHeight(corners, tile.AddXy(4), depth);
                UpdateCornerHeight(corners, tile.AddY(4),  depth);
            }

            // Slope clamping: iterate over every adjacent corner pair (4 tiles apart in X or Y)
            // and raise the lower (deeper) corner whenever they violate the max slope constraint.
            // This propagates the constraint across the whole grid until no pair is violated.
            var adjacentOffsets = new Tile2i[]
            {
                new Tile2i(4, 0),
                new Tile2i(-4, 0),
                new Tile2i(0, 4),
                new Tile2i(0, -4),
            };

            bool changed = true;
            int iteration = 0;
            int maxIterations = 1000;

            while (changed && iteration < maxIterations)
            {
                changed = false;
                iteration++;

                foreach (var corner in corners.Keys.ToList())
                {
                    int h = corners[corner];
                    foreach (var offset in adjacentOffsets)
                    {
                        var neighbor = Offset(corner, offset);
                        if (!corners.TryGetValue(neighbor, out int nh)) continue;

                        if (h - nh > maxAllowedDiff)
                        {
                            // This corner is too much shallower than neighbor; lower this corner (dig deeper)
                            corners[corner] = nh + maxAllowedDiff;
                            h = corners[corner];
                            changed = true;
                        }
                        else if (nh - h > maxAllowedDiff)
                        {
                            // Neighbor is too much shallower than this corner; lower neighbor (dig deeper)
                            corners[neighbor] = h + maxAllowedDiff;
                            changed = true;
                        }
                    }
                }
            }

            if (iteration >= maxIterations)
                Log.Warning(string.Format("Corner smoothing did not converge after {0} iterations", maxIterations));
            else
                LogDebug(string.Format("Corner smoothing converged after {0} iterations", iteration));

            return corners;
        }

        private static void UpdateCornerHeight(Dict<Tile2i, int> corners, Tile2i corner, int newHeight)
        {
            if (corners.TryGetValue(corner, out int existingHeight))
            {
                corners[corner] = (existingHeight + newHeight) / 2;
            }
            else
            {
                corners[corner] = newHeight;
            }
        }

        private static Tile2i Offset(Tile2i origin, Tile2i delta)
        {
            return new Tile2i(origin.X + delta.X, origin.Y + delta.Y);
        }
    }
}
