// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
// Auto Forestry Designations - Designation Cleanup
using Mafi;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Terrain.Designation;

namespace AutoForestryDesignations
{
    public static partial class AutoForestryDesignation
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
                    var designationAt = s_desigManager.GetDesignationAt(origin);
                    if ((area.ContainsTile(origin) || area.ContainsTile(origin.AddX(3))
                        || area.ContainsTile(origin.AddY(3)) || area.ContainsTile(origin.AddXy(3)))
                        && designationAt.HasValue
                        && designationAt.Value.IsForestry)
                    {
                        s_desigManager.RemoveDesignation(origin);
                    }
                }
            }
        }

        internal static void ClearDesignationsForTower(IAreaManagingTower tower)
        {
            ClearDesignationsInArea(tower);
            QueueForestryInfoRefresh(tower);
        }

    }
}
