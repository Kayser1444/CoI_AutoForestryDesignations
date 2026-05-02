// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
using Mafi.Core.Buildings.Forestry;
using UnityEngine;

namespace AutoForestryDesignations;

public sealed class AutoForestryDesignationsTicker : MonoBehaviour
{
    private static ForestryTower? s_pendingRefreshTower;
    private static float s_pendingRefreshAtTime;

    internal static void QueueForestryInfoRefresh(ForestryTower tower)
    {
        s_pendingRefreshTower = tower;
        s_pendingRefreshAtTime = Time.unscaledTime + 0.05f;
    }

    private void Update()
    {
        if (s_pendingRefreshTower == null || Time.unscaledTime < s_pendingRefreshAtTime)
            return;

        var tower = s_pendingRefreshTower;
        s_pendingRefreshTower = null;
        ForestryInfoPanel.RefreshForTower(tower);
    }
}
