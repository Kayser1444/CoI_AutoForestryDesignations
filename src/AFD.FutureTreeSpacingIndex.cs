// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using Mafi;

namespace AutoForestryDesignations
{
    /// <summary>
    /// Incremental spacing index for projected future trees. A conflicting tree
    /// can only be in the candidate's own spacing-sized bucket or one of its
    /// eight neighbours, avoiding a scan of every previously accepted tree.
    /// </summary>
    internal sealed class FutureTreeSpacingIndex
    {
        private readonly int m_cellSize;
        private readonly long m_requiredSpacingSqr;
        private readonly Dictionary<long, List<Tile2i>> m_buckets =
            new Dictionary<long, List<Tile2i>>();

        public int Count { get; private set; }

        public FutureTreeSpacingIndex(int plantingSpacing)
        {
            m_cellSize = Math.Max(1, plantingSpacing * 2);
            m_requiredSpacingSqr = (long)m_cellSize * m_cellSize;
        }

        public bool TryAdd(Tile2i candidate)
        {
            int cellX = FloorDiv(candidate.X, m_cellSize);
            int cellY = FloorDiv(candidate.Y, m_cellSize);
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (!m_buckets.TryGetValue(
                        GetBucketKey(cellX + offsetX, cellY + offsetY),
                        out List<Tile2i>? bucket))
                    {
                        continue;
                    }

                    foreach (Tile2i existing in bucket)
                    {
                        if (candidate.DistanceSqrTo(existing) < m_requiredSpacingSqr)
                            return false;
                    }
                }
            }

            long key = GetBucketKey(cellX, cellY);
            if (!m_buckets.TryGetValue(key, out List<Tile2i>? ownBucket))
            {
                ownBucket = new List<Tile2i>();
                m_buckets.Add(key, ownBucket);
            }
            ownBucket.Add(candidate);
            Count++;
            return true;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static long GetBucketKey(int x, int y)
            => ((long)x << 32) ^ (uint)y;
    }
}
