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
using UnityEngine;

namespace AutoForestryDesignations
{
    public static partial class AutoDepthDesignation
    {
        private static IEnumerator CreateDesignationsCoroutine(IAreaManagingTower tower, bool generateRamps, object? inspectorInstance = null)
        {
            if (s_desigManager == null || s_miningProto == null) yield break;

            var area = tower.Area;
            if (area.IsEmpty) yield break;

            var terrMgr = s_desigManager.TerrainManager;

            List<LooseProductProto> scanProducts = GetScanProducts(tower);
            if (scanProducts.Count == 0) yield break;

            var productSet = HybridSet<LooseProductProto>.From(scanProducts);
            var tempResults = new Lyst<ProductResource>();

            var bbMin = TerrainDesignation.GetOrigin(area.BoundingBoxMin);
            var bbMax = TerrainDesignation.GetOrigin(area.BoundingBoxMax);

            int scanCount = 0;

            var productCounts = new Dictionary<LooseProductProto, int>();
            var resourceDetailsByTile = new Dictionary<Tile2i, List<ProductResource>>();
            var towerSettings = GetOrCreateTowerSettings(tower);
            int maxHeightDiff = towerSettings.MaxHeightDiff;
            int maxLayersToExcavate = towerSettings.MaxLayersToExcavate;
            int? maxDepthToDigTo = towerSettings.MaxDepthToDigTo;
            int purityLevel = towerSettings.OrePurityLevel;
            int corridorClearance = towerSettings.CorridorClearance;

            LogDebug(string.Format("Scanning mine area from {0} to {1} for ore depth...", bbMin, bbMax));

            for (int y = bbMin.Y; y < bbMax.Y; y += 4)
            {
                for (int x = bbMin.X; x < bbMax.X; x += 4)
                {
                    var coord = new Tile2i(x, y);
                    
                    // Sample every terrain tile inside the 4x4 designation tile so ore decisions
                    // do not miss interior pockets or contamination.
                    if (!TryGetResourcesFromAllTiles(coord, area, terrMgr, productSet, tempResults, out List<ProductResource> resourcesForTile))
                    {
                        LogDebug(string.Format("Skipping tile with cells outside area: {0}", coord));
                        continue;
                    }

                    if (resourcesForTile.Count == 0)
                    {
                        LogDebug(string.Format("Tile {0}: No resources found in sampled cells", coord));
                        continue;
                    }

                    try
                    {
                        HashSet<LooseProductProto> tileProducts = new HashSet<LooseProductProto>();

                        for (int i = 0; i < resourcesForTile.Count; i++)
                        {
                            ProductResource resource = resourcesForTile[i];
                            tileProducts.Add(resource.Product);
                        }

                        if (resourcesForTile.Count > 0)
                        {
                            resourceDetailsByTile[coord] = resourcesForTile;
                        }

                        foreach (LooseProductProto product in tileProducts)
                        {
                            if (productCounts.TryGetValue(product, out int existingCount))
                            {
                                productCounts[product] = existingCount + 1;
                            }
                            else
                            {
                                productCounts[product] = 1;
                            }
                        }
                    }
                    catch
                    {
                    }

                    scanCount++;
                    int effectiveBatchSize = GetEffectiveBatchSize();
                    if (scanCount % effectiveBatchSize == 0)
                        yield return null;
                }
            }

            if (productCounts.Count == 0) yield break;

            ProductProto? selectedProduct = GetSelectedOre(tower);
            var targetProductIds = BuildTargetProductIdSet(scanProducts);
            var maxOreDepths = new Dict<Tile2i, int>();

            float minBottomOreDensity = s_minBottomOreDensityByLevel[purityLevel];
            float minOrePurity    = s_minOrePurityByLevel[purityLevel];
            float minOreHeight    = s_minOreHeightByLevel[purityLevel];

            foreach (KeyValuePair<Tile2i, List<ProductResource>> kvp in resourceDetailsByTile)
            {
                float terrainH = GetMinSurfaceHeightInDesignatableTile(kvp.Key, terrMgr);

                // Criterion 3: contamination ratio — skip tiles where ore fraction is too low
                if (minOrePurity > 0f)
                {
                    float purityRatio = ComputeTilePurityRatio(kvp.Key, terrMgr, targetProductIds);
                    if (purityRatio < minOrePurity)
                    {
                        LogDebug(string.Format("Tile {0} rejected: purity {1:P0} < threshold {2:P0}", kvp.Key, purityRatio, minOrePurity));
                        continue;
                    }
                }

                // Criterion 2: ore height — skip tiles with too little ore (not just isolated)
                if (minOreHeight > 0f)
                {
                    float tileOreHeight = GetTargetProductAmount(kvp.Value, targetProductIds);
                    if (tileOreHeight < minOreHeight)
                    {
                        LogDebug(string.Format("Tile {0} rejected: ore height {1:F2} < threshold {2:F2}", kvp.Key, tileOreHeight, minOreHeight));
                        continue;
                    }
                }

                // Criterion 1: bottom density trim — stop at the deepest ore zone still meeting the min density threshold
                bool depthFound = minBottomOreDensity > 0f
                    ? TryGetPurityAdjustedDepth(kvp.Value, targetProductIds, terrainH, minBottomOreDensity, out int depthInt)
                    : TryGetDeepestResourceDepth(kvp.Value, targetProductIds, terrainH, out depthInt);

                if (depthFound)
                {
                    // Apply max-layers constraint (0 = unlimited)
                    if (maxLayersToExcavate > 0)
                        depthInt = Math.Max(depthInt, (int)terrainH - maxLayersToExcavate);

                    // Apply absolute min-elevation constraint
                    if (maxDepthToDigTo.HasValue)
                        depthInt = Math.Max(depthInt, maxDepthToDigTo.Value);

                    maxOreDepths[kvp.Key] = depthInt;
                }
            }

            if (maxOreDepths.Count == 0) yield break;

            LogDebug(string.Format("Before filtering: {0} tiles in designations", maxOreDepths.Count));
            FilterIsolatedDesignations(maxOreDepths, targetProductIds, resourceDetailsByTile, purityLevel);

            if (maxOreDepths.Count == 0) yield break;

            FillRectilinearHull(maxOreDepths, targetProductIds, resourceDetailsByTile, corridorClearance);

            LogDebug(string.Format("After filtering+connecting: {0} tiles in designations", maxOreDepths.Count));
            LogDebug(selectedProduct != null
                ? "Selected product: " + selectedProduct.Id
                : "Selected product mode: None (all useful products, excluding dirt/rock)");

            var maxOreDepthOverall = maxOreDepths.Values.Min();

            LogDebug(string.Format("Creating designations for {0} tiles with overall max depth {1}", maxOreDepths.Count, maxOreDepthOverall));

            var cornerHeights = BuildAndSmoothCornerHeights(maxOreDepths, maxHeightDiff);

            int designCount = 0;
            foreach (var kvp in maxOreDepths)
            {
                var tile = kvp.Key;
                var nwCorner = tile;
                var neCorner = tile.AddX(4);
                var seCorner = tile.AddXy(4);
                var swCorner = tile.AddY(4);

                if (!cornerHeights.TryGetValue(nwCorner, out int hNW) ||
                    !cornerHeights.TryGetValue(neCorner, out int hNE) ||
                    !cornerHeights.TryGetValue(seCorner, out int hSE) ||
                    !cornerHeights.TryGetValue(swCorner, out int hSW))
                {
                    Log.Warning(string.Format("Missing corner heights for tile {0}", tile));
                    continue;
                }

                var data = new DesignationData(tile,
                    new HeightTilesI(hNW), new HeightTilesI(hNE),
                    new HeightTilesI(hSE), new HeightTilesI(hSW));

                if (!s_desigManager.AddOrReplaceDesignation(s_miningProto, data))
                {
                    Log.Warning(string.Format("Failed to create designation for tile {0}", tile));
                }

                designCount++;
                int effectiveBatchSize = GetEffectiveBatchSize();
                if (designCount % effectiveBatchSize == 0)
                    yield return null;
            }

            LogDebug(string.Format("Created {0} designations", designCount));

            if (generateRamps)
            {
                LogDebug("Creating access ramp...");
                bool rampCreated = CreateAccessRamp(tower, maxOreDepths, cornerHeights, terrMgr, towerSettings.RampWidth);
                if (!rampCreated)
                {
                    string reason = string.IsNullOrWhiteSpace(s_lastRampFailureReason)
                        ? "Ramp could not be generated."
                        : s_lastRampFailureReason!;
                    Log.Warning("Ramp generation failed: " + reason);
                }
            }
            else
            {
                LogDebug("Ramp generation is disabled in settings.");
            }

            RemoveFulfilledDesignationsForTower(tower);
            CleanupIsolatedLeftoverDesignationsForTower(tower, maxOreDepths);

            // Refresh ore composition panel after creating designations
            if (inspectorInstance != null)
            {
                OreCompositionPanel.ResetContent(inspectorInstance);
            }
        }

        internal static void CreateDesignationsForTower(IAreaManagingTower tower)
        {
            var towerSettings = GetOrCreateTowerSettings(tower);
            s_coroutineHost?.StartCoroutine(CreateDesignationsCoroutine(tower, towerSettings.RampWidth > 0, null));
        }

        /// <summary>
        /// Same as <see cref="CreateDesignationsForTower(IAreaManagingTower)"/> but passes
        /// <paramref name="panelKey"/> to the coroutine so that the Ore Composition panel
        /// registered under that key auto-refreshes when the scan completes.
        /// </summary>
        internal static void CreateDesignationsForTower(IAreaManagingTower tower, object? panelKey)
        {
            var towerSettings = GetOrCreateTowerSettings(tower);
            s_coroutineHost?.StartCoroutine(CreateDesignationsCoroutine(tower, towerSettings.RampWidth > 0, panelKey));
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
