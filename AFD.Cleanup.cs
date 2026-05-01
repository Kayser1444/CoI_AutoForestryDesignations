// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
// Auto Forestry Designations - Designation Cleanup
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Terrain.Designation;

namespace AutoForestryDesignations
{
    public static partial class AutoDepthDesignation
    {
        private static void ClearDesignationsInArea(IAreaManagingTower tower)
        {
            if (s_desigManager == null) return;

            var area = tower.Area;
            if (area.IsEmpty) return;

            var bbMin = area.BoundingBoxMin;
            var bbMax = area.BoundingBoxMax;

            int minX = TerrainDesignation.GetOrigin(bbMin).X;
            int minY = TerrainDesignation.GetOrigin(bbMin).Y;
            int maxX = TerrainDesignation.GetOrigin(new Tile2i(bbMax.X - 1, bbMax.Y - 1)).X;
            int maxY = TerrainDesignation.GetOrigin(new Tile2i(bbMax.X - 1, bbMax.Y - 1)).Y;

            for (int y = minY; y <= maxY; y += 4)
            {
                for (int x = minX; x <= maxX; x += 4)
                {
                    var origin = new Tile2i(x, y);
                    if (area.ContainsTile(origin) || area.ContainsTile(origin.AddX(3))
                        || area.ContainsTile(origin.AddY(3)) || area.ContainsTile(origin.AddXy(3)))
                        s_desigManager.RemoveDesignation(origin);
                }
            }
        }

        internal static void ClearDesignationsForTower(IAreaManagingTower tower)
        {
            ClearDesignationsInArea(tower);
        }

        private static void RemoveFulfilledDesignationsForTower(IAreaManagingTower tower)
        {
            if (s_desigManager == null)
            {
                return;
            }

            var fulfilledOrigins = new List<Tile2i>();
            foreach (TerrainDesignation designation in tower.ManagedDesignations)
            {
                if (designation.IsFulfilled)
                {
                    fulfilledOrigins.Add(designation.OriginTileCoord);
                }
            }

            foreach (Tile2i origin in fulfilledOrigins)
            {
                s_desigManager.RemoveDesignation(origin);
            }
        }

        private static void CleanupIsolatedLeftoverDesignationsForTower(IAreaManagingTower tower, Dict<Tile2i, int> originalOreOrigins)
        {
            if (s_desigManager == null)
            {
                return;
            }

            var remainingOrigins = new HashSet<Tile2i>();
            foreach (TerrainDesignation designation in tower.ManagedDesignations)
            {
                if (!designation.IsFulfilled)
                {
                    remainingOrigins.Add(designation.OriginTileCoord);
                }
            }

            if (remainingOrigins.Count == 0)
            {
                return;
            }

            var originalOriginSet = new HashSet<Tile2i>(originalOreOrigins.Keys);
            var visited = new HashSet<Tile2i>();
            var originsToRemove = new List<Tile2i>();

            foreach (Tile2i origin in remainingOrigins)
            {
                if (visited.Contains(origin))
                {
                    continue;
                }

                var component = new List<Tile2i>();
                FloodFillOrigins(origin, remainingOrigins, visited, component);

                bool touchesOriginalOre = component.Any(originalOriginSet.Contains);
                if (!touchesOriginalOre)
                {
                    originsToRemove.AddRange(component);
                }
            }

            foreach (Tile2i origin in originsToRemove)
            {
                s_desigManager.RemoveDesignation(origin);
            }

            if (originsToRemove.Count > 0)
            {
                LogDebug(string.Format("Removed {0} isolated leftover designation tile(s) after ramp cleanup", originsToRemove.Count));
            }
        }
    }
}
