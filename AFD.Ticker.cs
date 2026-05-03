// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
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
