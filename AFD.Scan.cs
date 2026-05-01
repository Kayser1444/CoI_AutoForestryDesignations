// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
// Auto Forestry Designations - Designation Scanning and Resource Sampling
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Buildings.Mine;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Terrain.Resources;
using Mafi.Core.Terrain.Trees;
using UnityEngine;

namespace AutoForestryDesignations
{
    public static partial class AutoDepthDesignation
    {
        private static IEnumerator CreateDesignationsCoroutine(IAreaManagingTower tower, bool generateRamps, object? inspectorInstance = null)
        {
            if (s_desigManager == null || s_forestryProto == null) yield break;

            var area = tower.Area;
            if (area.IsEmpty) yield break;

            var terrMgr = s_desigManager.TerrainManager;
            var treesManager = s_desigManager.TreesManager;
            var towerSettings = GetOrCreateTowerSettings(tower);
            bool avoidInfertile = towerSettings.AvoidInfertileTiles;
            bool avoidWithTrees = towerSettings.AvoidTilesWithTrees;
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

                    // Place flat designation at surface height at each corner
                    var tile = new Tile2i(x, y);
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
        internal static void CreateDesignationsForTower(IAreaManagingTower tower)
        {
            s_coroutineHost?.StartCoroutine(CreateDesignationsCoroutine(tower, false, null));
        }

        internal static void CreateDesignationsForTower(IAreaManagingTower tower, object? panelKey)
        {
            s_coroutineHost?.StartCoroutine(CreateDesignationsCoroutine(tower, false, panelKey));
        }

        private static List<LooseProductProto> GetScanProducts(IAreaManagingTower tower)
        {
            if (s_protosDb == null)
            {
                return new List<LooseProductProto>();
            }

            // Get all available ores first
            var allOres = s_protosDb.All<LooseProductProto>()
                .Where(product => product != LooseProductProto.Phantom)
                .Where(product => product.CanBeOnTerrain || product.TerrainMaterial != null)
                .Distinct()
                .ToList();

            // Check if a specific ore is selected for this tower
            var selectedOre = GetSelectedOre(tower);
            if (selectedOre != null && selectedOre is LooseProductProto selectedLoose)
            {
                // Return only the selected ore if it's available (allow rock/dirt if explicitly selected)
                return allOres.Contains(selectedLoose) ? new List<LooseProductProto> { selectedLoose } : new List<LooseProductProto>();
            }

            // Return all ores except rock/dirt if "Auto" mode (null selection)
            return allOres.Where(product => !IsRockProduct(product)).ToList();
        }

        private static HashSet<string> BuildTargetProductIdSet(IEnumerable<LooseProductProto> products)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (LooseProductProto product in products)
            {
                ids.Add(product.Id.ToString());
            }

            return ids;
        }

        private static IEnumerable<Tile2i> EnumerateDesignatableTileCells(Tile2i tileOrigin)
        {
            for (int yOffset = 0; yOffset < 4; yOffset++)
            {
                for (int xOffset = 0; xOffset < 4; xOffset++)
                {
                    yield return new Tile2i(tileOrigin.X + xOffset, tileOrigin.Y + yOffset);
                }
            }
        }

        private static float GetMinSurfaceHeightInDesignatableTile(Tile2i tileOrigin, TerrainManager terrMgr)
        {
            float minHeight = float.MaxValue;
            foreach (Tile2i cell in EnumerateDesignatableTileCells(tileOrigin))
            {
                float h = terrMgr.GetHeight(cell).Value.ToFloat();
                if (h < minHeight)
                {
                    minHeight = h;
                }
            }

            return minHeight;
        }

        private static bool TryGetResourcesFromAllTiles(
            Tile2i tileOrigin,
            PolygonTerrainArea2i area,
            TerrainManager terrMgr,
            HybridSet<LooseProductProto> productSet,
            Lyst<ProductResource> tempResults,
            out List<ProductResource> combinedResources)
        {
            combinedResources = new List<ProductResource>();

            // If any subtile is outside the managed area, reject this designation tile.
            foreach (Tile2i cell in EnumerateDesignatableTileCells(tileOrigin))
            {
                if (!area.ContainsTile(cell))
                {
                    return false;
                }
            }

            // Collect resources from all 16 terrain cells inside the designation tile.
            try
            {
                foreach (Tile2i cell in EnumerateDesignatableTileCells(tileOrigin))
                {
                    tempResults.Clear();
                    GetResourceDetailsNoBedrock(terrMgr, cell, productSet, tempResults);

                    for (int i = 0; i < tempResults.Count; i++)
                    {
                        combinedResources.Add(tempResults[i]);
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private static LooseProductProto SelectMostCommonProduct(Dictionary<LooseProductProto, int> productCounts)
        {
            return productCounts
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key.Id.ToString())
                .First()
                .Key;
        }

        private static int ClampBatchSize(int value)
        {
            return Math.Max(1, Math.Min(MAX_BATCH_SIZE, value));
        }

        private static int GetEffectiveBatchSize()
        {
            int configuredBatchSize = ClampBatchSize(s_batchSize);
            if (Time.timeScale > 0f)
            {
                return configuredBatchSize;
            }

            long boostedBatchSize = (long)configuredBatchSize * PAUSED_BATCH_MULTIPLIER;
            return (int)Math.Min(MAX_BATCH_SIZE, boostedBatchSize);
        }

        private static bool IsRockProduct(LooseProductProto product)
        {
            string productId = product.Id.ToString();
            return productId.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0
                || productId.IndexOf("dirt", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void GetResourceDetailsNoBedrock(
            TerrainManager terrMgr,
            Tile2i coord,
            HybridSet<LooseProductProto> products,
            Lyst<ProductResource> result)
        {
            ThicknessTilesF cumulativeDepth = ThicknessTilesF.Zero;
            TerrainLayerEnumerator enumerator = terrMgr.EnumerateLayers(terrMgr.GetTileIndex(coord));
            while (enumerator.MoveNext())
            {
                TerrainMaterialThicknessSlim layer = enumerator.Current;
                if (s_bedrockTerrainMaterial != null && layer.SlimId == s_bedrockTerrainMaterial.SlimId)
                    break;

                TerrainMaterialProto mat = layer.SlimId.ToFull(terrMgr);
                LooseProductProto minedProduct = mat.MinedProduct;
                if (products.Contains(minedProduct))
                {
                    result.Add(new ProductResource(minedProduct, layer.Thickness, cumulativeDepth));
                }
                cumulativeDepth += layer.Thickness;
            }
        }

        private static bool TryGetDeepestResourceDepth(
            List<ProductResource> resources,
            HashSet<string> targetProductIds,
            float terrainHeight,
            out int depthInt)
        {
            depthInt = 0;
            bool found = false;

            foreach (ProductResource resource in resources)
            {
                if (!targetProductIds.Contains(resource.Product.Id.ToString()))
                {
                    continue;
                }

                int candidateDepth = (terrainHeight - resource.Depth.Value.ToFloat() - resource.Height.Value.ToFloat()).FloorToInt();
                if (!found || candidateDepth < depthInt)
                {
                    depthInt = candidateDepth;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Returns total non-bedrock column thickness and ore thickness for a tile.
        /// Used to compute the overburden contamination ratio.
        /// </summary>
        private static void GetColumnThicknesses(
            TerrainManager terrMgr,
            Tile2i coord,
            HashSet<string> targetProductIds,
            out float totalThickness,
            out float oreThickness)
        {
            totalThickness = 0f;
            oreThickness = 0f;
            TerrainLayerEnumerator enumerator = terrMgr.EnumerateLayers(terrMgr.GetTileIndex(coord));
            while (enumerator.MoveNext())
            {
                TerrainMaterialThicknessSlim layer = enumerator.Current;
                if (s_bedrockTerrainMaterial != null && layer.SlimId == s_bedrockTerrainMaterial.SlimId)
                    break;
                float thickness = layer.Thickness.Value.ToFloat();
                totalThickness += thickness;
                TerrainMaterialProto mat = layer.SlimId.ToFull(terrMgr);
                if (targetProductIds.Contains(mat.MinedProduct.Id.ToString()))
                    oreThickness += thickness;
            }
        }

        /// <summary>
        /// Computes average purity ratio (ore / total column) across every terrain cell in a designatable tile.
        /// Returns 0 if no column data available.
        /// </summary>
        private static float ComputeTilePurityRatio(
            Tile2i tileOrigin,
            TerrainManager terrMgr,
            HashSet<string> targetProductIds)
        {
            float totalOre = 0f, totalAll = 0f;
            foreach (Tile2i cell in EnumerateDesignatableTileCells(tileOrigin))
            {
                try
                {
                    GetColumnThicknesses(terrMgr, cell, targetProductIds, out float colTotal, out float colOre);
                    totalAll += colTotal;
                    totalOre += colOre;
                }
                catch { }
            }
            return totalAll > 0f ? totalOre / totalAll : 0f;
        }

        /// <summary>
        /// Returns the elevation to dig to for a tile using a density-based bottom trim
        /// (Criterion 1: bottom density trim).
        /// Walks ore intervals top-to-bottom. For each interval after the first, computes the
        /// local ore density of the zone from the previous interval's bottom to this one's bottom
        /// (ore_thickness / zone_thickness). If that density falls below minBottomOreDensity the
        /// scan stops — the dig target is set to the bottom of the last qualifying interval.
        /// This avoids digging through large waste gaps to reach thin sparse seams at depth.
        /// </summary>
        private static bool TryGetPurityAdjustedDepth(
            List<ProductResource> resources,
            HashSet<string> targetProductIds,
            float terrainHeight,
            float minBottomOreDensity,
            out int depthInt)
        {
            depthInt = 0;
            var intervals = new List<(float top, float bottom, float thickness)>();
            foreach (var resource in resources)
            {
                if (!targetProductIds.Contains(resource.Product.Id.ToString()))
                    continue;
                float topDepth    = resource.Depth.Value.ToFloat();
                float thickness   = resource.Height.Value.ToFloat();
                float bottomDepth = topDepth + thickness;
                intervals.Add((topDepth, bottomDepth, thickness));
            }
            if (intervals.Count == 0) return false;

            if (minBottomOreDensity <= 0f)
            {
                // No trimming — use deepest bottom
                float deepest = 0f;
                bool anyFound = false;
                foreach (var iv in intervals)
                {
                    if (!anyFound || iv.bottom > deepest) { deepest = iv.bottom; anyFound = true; }
                }
                depthInt = (terrainHeight - deepest).FloorToInt();
                return true;
            }

            // Sort top-to-bottom (shallowest first)
            intervals.Sort((a, b) => a.top.CompareTo(b.top));

            float stopDepth = 0f;
            bool found = false;
            for (int i = 0; i < intervals.Count; i++)
            {
                var iv = intervals[i];
                float localDensity;
                if (i == 0)
                {
                    // Shallowest interval always qualifies — no zone above it to evaluate
                    localDensity = 1f;
                }
                else
                {
                    // Zone = from bottom of previous ore interval to bottom of this one
                    // (includes the waste gap between them plus this ore seam)
                    float zoneThickness = iv.bottom - intervals[i - 1].bottom;
                    localDensity = zoneThickness > 0f ? iv.thickness / zoneThickness : 1f;
                }

                if (localDensity >= minBottomOreDensity)
                {
                    stopDepth = iv.bottom;
                    found = true;
                }
                else
                {
                    // This zone is too sparse — don't dig deeper
                    break;
                }
            }

            if (!found) return false;
            depthInt = (terrainHeight - stopDepth).FloorToInt();
            return true;
        }
    }
}
