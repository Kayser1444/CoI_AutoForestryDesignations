// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Buildings.Forestry;
using Mafi.Core.Entities.Dynamic;
using Mafi.Core.Entities.Static;
using Mafi.Core.PathFinding.Goals;
using Mafi.Core.Products;
using Mafi.Core.Simulation;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Terrain.Trees;
using Mafi.Core.Vehicles;
using Mafi.Core.Vehicles.Jobs;
using Mafi.Core.Vehicles.TreeHarvesters;
using Mafi.Core.Vehicles.TreePlanters;

namespace AutoForestryDesignations;

/// <summary>
/// Simulation-thread-owned coordination for AFD's Forestry vehicle optimizations.
/// Claims and navigation jobs are deliberately transient; only the world setting is persisted.
/// </summary>
public static partial class AutoForestryDesignation
{
    private sealed class FutureHarvestClaim
    {
        public readonly TreeHarvester Harvester;
        public readonly ForestryTower Tower;
        public TreeId Tree;
        public bool ActiveHarvest;

        public FutureHarvestClaim(TreeHarvester harvester, ForestryTower tower, TreeId tree, bool activeHarvest = false)
        {
            Harvester = harvester;
            Tower = tower;
            Tree = tree;
            ActiveHarvest = activeHarvest;
        }
    }

    private sealed class FuturePlantingClaim
    {
        public readonly TreePlanter Planter;
        public readonly ForestryTower Tower;
        public readonly TreeId Tree;
        public readonly Tile2i Tile;
        public bool ActiveHarvest;

        public FuturePlantingClaim(TreePlanter planter, ForestryTower tower, TreeId tree, bool activeHarvest)
        {
            Planter = planter;
            Tower = tower;
            Tree = tree;
            Tile = tree.Position.AsFull;
            ActiveHarvest = activeHarvest;
        }
    }

    private static readonly object s_vehicleOptimizationEventOwner = new object();
    private static readonly Dictionary<EntityId, FutureHarvestClaim> s_futureHarvestByHarvester = new();
    private static readonly Dictionary<TreeId, FutureHarvestClaim> s_futureHarvestByTree = new();
    private static readonly Dictionary<EntityId, FuturePlantingClaim> s_futurePlantingByPlanter = new();
    private static readonly Dictionary<Tile2i, FuturePlantingClaim> s_futurePlantingByTile = new();
    private static readonly Dictionary<EntityId, NavigateToJob> s_stagingJobsByVehicle = new();

    private static ITreesManager? s_vehicleTreesManager;
    private static TreeHarvestingJob.Factory? s_treeHarvestingJobFactory;
    private static NavigateToJob.Factory? s_navigateToJobFactory;
    private static TreeVehicleGoal.Factory? s_treeGoalFactory;
    private static PlantingVehicleGoal.Factory? s_plantingGoalFactory;
    private static UnreachableTerrainDesignationsManager? s_vehicleUnreachablesManager;
    private static IVehiclesManager? s_vehicleManager;
    private static bool s_vehicleOptimizationApplied;
    private static bool s_rebuildActiveHarvestClaims;

    internal static void InitializeVehicleOptimizations(
        ITreesManager treesManager,
        TreeHarvestingJob.Factory treeHarvestingJobFactory,
        NavigateToJob.Factory navigateToJobFactory,
        TreeVehicleGoal.Factory treeGoalFactory,
        PlantingVehicleGoal.Factory plantingGoalFactory,
        UnreachableTerrainDesignationsManager unreachablesManager,
        IVehiclesManager vehicleManager)
    {
        s_vehicleTreesManager = treesManager;
        s_treeHarvestingJobFactory = treeHarvestingJobFactory;
        s_navigateToJobFactory = navigateToJobFactory;
        s_treeGoalFactory = treeGoalFactory;
        s_plantingGoalFactory = plantingGoalFactory;
        s_vehicleUnreachablesManager = unreachablesManager;
        s_vehicleManager = vehicleManager;
        s_vehicleOptimizationApplied = AutoForestryDesignationsMod.ForestryVehicleOptimizations;
        s_rebuildActiveHarvestClaims = true;
        treesManager.TreeRemoved.AddNonSaveable(s_vehicleOptimizationEventOwner, OnTreeRemoved);
    }

    internal static void VehicleOptimizationsUpdateStart()
    {
        bool enabled = AutoForestryDesignationsMod.ForestryVehicleOptimizations;
        if (enabled != s_vehicleOptimizationApplied)
        {
            s_vehicleOptimizationApplied = enabled;
            if (!enabled)
            {
                ReleaseAllClaimsAndStaging();
            }
            else
            {
                s_rebuildActiveHarvestClaims = true;
            }
        }

        if (s_vehicleOptimizationApplied)
        {
            if (s_rebuildActiveHarvestClaims)
            {
                RebuildActiveHarvestClaims();
                s_rebuildActiveHarvestClaims = false;
            }
            ValidateClaims();
        }
    }

    internal static void BeforeVehicleOptimizationsSave()
    {
        // Staging jobs are derived runtime control and must not be retained as AFD state in a save.
        ReleaseAllClaimsAndStaging();
        s_rebuildActiveHarvestClaims = true;
    }

    internal static void ClearVehicleOptimizations()
    {
        ReleaseAllClaimsAndStaging();
        if (s_vehicleTreesManager != null)
        {
            try { s_vehicleTreesManager.TreeRemoved.RemoveNonSaveable(s_vehicleOptimizationEventOwner, OnTreeRemoved); }
            catch { }
        }
        s_vehicleTreesManager = null;
        s_treeHarvestingJobFactory = null;
        s_navigateToJobFactory = null;
        s_treeGoalFactory = null;
        s_plantingGoalFactory = null;
        s_vehicleUnreachablesManager = null;
        s_vehicleManager = null;
        s_vehicleOptimizationApplied = false;
        s_rebuildActiveHarvestClaims = false;
    }

    internal static void ApplyVehicleOptimizationPatches(Harmony harmony)
    {
        try
        {
            PatchMethod(harmony,
                typeof(ParkAndWaitJobFactory).GetMethod(nameof(ParkAndWaitJobFactory.TryEnqueueParkingJobIfNeeded), BindingFlags.Instance | BindingFlags.Public),
                nameof(SuppressForestryTowerParkingPrefix), prefix: true);

            PatchMethod(harmony,
                typeof(TreePlanterJobProvider).GetMethod(nameof(TreePlanterJobProvider.TryGetJobFor), BindingFlags.Instance | BindingFlags.Public),
                nameof(TreePlanterTryGetJobForPostfix));
            PatchMethod(harmony,
                typeof(TreeHarvesterJobProvider).GetMethod(nameof(TreeHarvesterJobProvider.TryGetJobFor), BindingFlags.Instance | BindingFlags.Public),
                nameof(TreeHarvesterTryGetJobForPostfix));

            PatchMethod(harmony,
                typeof(TreeHarvestingJob.Factory).GetMethod(nameof(TreeHarvestingJob.Factory.EnqueueJob), BindingFlags.Instance | BindingFlags.Public),
                nameof(TreeHarvestingJobEnqueuedPostfix));
            PatchMethod(harmony,
                typeof(TreePlantingJob.Factory).GetMethod(nameof(TreePlantingJob.Factory.EnqueueJob), BindingFlags.Instance | BindingFlags.Public),
                nameof(TreePlantingJobEnqueuedPostfix));

            MethodInfo? privateHarvestSelection = typeof(TreeHarvesterJobProvider).GetMethod("tryGetTreeHarvestingJob", BindingFlags.Instance | BindingFlags.NonPublic);
            PatchMethod(harmony, privateHarvestSelection, nameof(ClaimedHarvestSelectionPrefix), prefix: true);

            MethodInfo? towerSelection = typeof(ForestryTower).GetMethod(nameof(ForestryTower.FindClosestTreeForHarvestFor), BindingFlags.Instance | BindingFlags.Public);
            PatchMethod(harmony, towerSelection, nameof(FilterGloballyClaimedHarvestPostfix));

            MethodInfo? setNewArea = typeof(ForestryTower).GetMethod(nameof(ForestryTower.SetNewArea), BindingFlags.Instance | BindingFlags.Public);
            PatchMethod(harmony, setNewArea, nameof(ForestryTowerAreaChangedPostfix));
        }
        catch (Exception ex)
        {
            s_log.Warning($"[AFD] Forestry vehicle optimization patches failed: {ex.Message}");
        }
    }

    private static void PatchMethod(Harmony harmony, MethodInfo? method, string patchName, bool prefix = false)
    {
        if (method == null)
        {
            s_log.Warning($"[AFD] Forestry optimization target '{patchName}' was not found.");
            return;
        }

        HarmonyMethod patch = new HarmonyMethod(typeof(AutoForestryDesignation), patchName);
        if (prefix)
            harmony.Patch(method, prefix: patch);
        else
            harmony.Patch(method, postfix: patch);
        LogDebug($"Forestry optimization patch applied: {method.DeclaringType?.Name}.{method.Name}");
    }

    public static bool SuppressForestryTowerParkingPrefix(Vehicle vehicle, ILayoutEntity staticEntity, ref bool __result)
    {
        if (s_vehicleOptimizationApplied
            && staticEntity is ForestryTower tower
            && IsAssignedOptimizedVehicle(vehicle, tower))
        {
            __result = false;
            return false;
        }

        return true;
    }

    public static void TreePlanterTryGetJobForPostfix(TreePlanter planter, ref bool __result)
    {
        if (__result || !s_vehicleOptimizationApplied || !IsAssignedOptimizedVehicle(planter, out ForestryTower tower))
            return;

        if (planter.IsEmpty || planter.LastRefuelRequestIssue != RefuelRequestIssue.None || planter.CannotWorkDueToLowFuel)
            return;

        if (TryMaintainPlantingClaim(planter, tower) || TryClaimExistingHarvestForPlanter(planter, tower))
            __result = true;
    }

    public static void TreeHarvesterTryGetJobForPostfix(TreeHarvester harvester, ref bool __result)
    {
        if (__result || !s_vehicleOptimizationApplied || !IsAssignedOptimizedVehicle(harvester, out ForestryTower tower))
            return;

        if (harvester.LastRefuelRequestIssue != RefuelRequestIssue.None || harvester.CannotWorkDueToLowFuel || !harvester.Cargo.IsEmpty)
            return;

        if (TryMaintainHarvestClaim(harvester, tower))
            __result = true;
        else if (s_futureHarvestByHarvester.TryGetValue(harvester.Id, out FutureHarvestClaim? claim))
            LogDebug($"[DEBUG-HVST] Harvester {harvester.Id} retained claim {claim.Tree} but did not receive staging; position={harvester.Position2f}, currentJob={harvester.CurrentJob.ValueOrNull?.GetType().Name}.");
    }

    public static void TreeHarvestingJobEnqueuedPostfix(TreeHarvester harvester, TreeId tree, Option<ForestryTower> tower)
    {
        if (!s_vehicleOptimizationApplied)
            return;

        ForestryTower? assignedTower = harvester.ForestryTower.ValueOrNull;
        ForestryTower? effectiveTower = assignedTower ?? tower.ValueOrNull;
        if (effectiveTower == null || effectiveTower.IsDestroyed || !effectiveTower.IsEnabled || s_vehicleTreesManager == null
            || !s_vehicleTreesManager.Trees.ContainsKey(tree) || !effectiveTower.Area.Contains(tree.Position.CenterTile2f))
            return;

        MarkActiveHarvestClaim(harvester, effectiveTower, tree);
        EnsurePlantingClaim(effectiveTower, harvester, tree, activeHarvest: true);
    }

    public static void TreePlantingJobEnqueuedPostfix(TreePlanter planter, TreeProto treeProto, Tile2i position, Option<ForestryTower> tmpTower)
    {
        if (s_vehicleOptimizationApplied)
            ReleasePlantingClaim(planter.Id);
    }

    public static bool ClaimedHarvestSelectionPrefix(TreeHarvester harvester, ref bool __result)
    {
        if (!s_vehicleOptimizationApplied || s_treeHarvestingJobFactory == null)
            return true;

        if (s_futureHarvestByHarvester.TryGetValue(harvester.Id, out FutureHarvestClaim? claim)
            && !claim.ActiveHarvest
            && IsValidFutureHarvestClaim(claim)
            && (s_vehicleUnreachablesManager == null || !s_vehicleUnreachablesManager.GetUnreachableTreesFor(harvester).Contains(claim.Tree))
            && claim.Tower.IsTreeReadyForHarvest(claim.Tree)
            && !s_vehicleTreesManager!.IsTreeReserved(claim.Tree))
        {
            s_treeHarvestingJobFactory.EnqueueJob(harvester, claim.Tree, Option.Some(claim.Tower));
            __result = true;
            return false;
        }

        return true;
    }

    public static void FilterGloballyClaimedHarvestPostfix(
        ForestryTower __instance,
        Vehicle vehicle,
        ProductProto.ID productId,
        IReadOnlySet<TreeId> unreachableTrees,
        ref TreeId? __result)
    {
        if (!s_vehicleOptimizationApplied || !__result.HasValue || !s_futureHarvestByTree.TryGetValue(__result.Value, out FutureHarvestClaim? claim))
            return;

        if (claim.Harvester == vehicle)
            return;

        TreeId? replacement = null;
        long bestDistance = long.MaxValue;
        foreach (KeyValuePair<TreeId, TreeData> item in s_vehicleTreesManager!.Trees)
        {
            TreeData tree = item.Value;
            if (!tree.IsValid
                || tree.HarvestedProductId != productId
                || !__instance.Area.Contains(tree.Position2f)
                || !__instance.IsTreeReadyForHarvest(tree.Id)
                || s_vehicleTreesManager.IsTreeReserved(tree.Id)
                || unreachableTrees.Contains(tree.Id)
                || (s_futureHarvestByTree.TryGetValue(tree.Id, out FutureHarvestClaim? owner) && owner.Harvester != vehicle))
            {
                continue;
            }

            long distance = vehicle.GroundPositionTile2i.DistanceSqrTo(tree.Id.Position);
            if (distance < bestDistance || (distance == bestDistance && (!replacement.HasValue || tree.Id.CompareTo(replacement.Value) < 0)))
            {
                bestDistance = distance;
                replacement = tree.Id;
            }
        }

        __result = replacement;
    }

    public static void ForestryTowerAreaChangedPostfix(ForestryTower __instance)
    {
        if (s_vehicleOptimizationApplied)
            ValidateClaims();
    }

    private static bool IsAssignedOptimizedVehicle(Vehicle vehicle, ForestryTower tower)
        => IsAssignedOptimizedVehicle(vehicle, out ForestryTower assignedTower) && assignedTower == tower;

    private static bool IsAssignedOptimizedVehicle(Vehicle vehicle, out ForestryTower tower)
    {
        ForestryTower? assignedTower = vehicle.AssignedTo.ValueOrNull as ForestryTower;
        if (assignedTower == null)
        {
            tower = null!;
            return false;
        }

        tower = assignedTower;
        return s_vehicleOptimizationApplied
            && !vehicle.IsDestroyed
            && vehicle.IsSpawned
            && vehicle.IsEnabled
            && !tower.IsDestroyed
            && tower.IsEnabled;
    }

    private static bool TryMaintainPlantingClaim(TreePlanter planter, ForestryTower tower)
    {
        if (!s_futurePlantingByPlanter.TryGetValue(planter.Id, out FuturePlantingClaim? claim))
            return false;

        if (!IsValidPlantingClaim(claim))
        {
            ReleasePlantingClaim(planter.Id);
            return false;
        }

        return TryStagePlanter(claim);
    }

    private static bool TryClaimExistingHarvestForPlanter(TreePlanter planter, ForestryTower tower)
    {
        FutureHarvestClaim? best = null;
        bool bestIsActive = false;
        long bestDistance = long.MaxValue;
        foreach (FutureHarvestClaim candidate in s_futureHarvestByTree.Values)
        {
            if (candidate.Tower != tower || !IsValidFutureHarvestClaim(candidate))
                continue;

            long distance = planter.GroundPositionTile2i.DistanceSqrTo(candidate.Tree.Position);
            bool isActive = candidate.ActiveHarvest;
            if (best == null
                || (isActive && !bestIsActive)
                || (isActive == bestIsActive && (distance < bestDistance
                    || (distance == bestDistance && candidate.Tree.CompareTo(best.Tree) < 0))))
            {
                best = candidate;
                bestIsActive = isActive;
                bestDistance = distance;
            }
        }

        if (best == null)
            return false;

        EnsurePlantingClaim(tower, best.Harvester, best.Tree, best.ActiveHarvest);
        return s_futurePlantingByPlanter.TryGetValue(planter.Id, out FuturePlantingClaim? claim)
            && TryStagePlanter(claim);
    }

    private static bool TryMaintainHarvestClaim(TreeHarvester harvester, ForestryTower tower)
    {
        s_futureHarvestByHarvester.TryGetValue(harvester.Id, out FutureHarvestClaim? claim);
        if (claim?.ActiveHarvest == true)
        {
            if (HasActiveHarvestJob(harvester) && s_vehicleTreesManager != null
                && (s_vehicleTreesManager.IsTreeReserved(claim.Tree) || s_vehicleTreesManager.IsTreeSelected(claim.Tree)))
                return false;
            ReleaseFutureHarvestClaim(harvester.Id);
            claim = null;
        }

        if (claim == null || !IsValidFutureHarvestClaim(claim))
        {
            ReleaseFutureHarvestClaim(harvester.Id);
            claim = FindAndClaimFutureHarvest(harvester, tower);
        }

        return claim != null && TryStageHarvester(claim);
    }

    private static bool HasActiveHarvestJob(TreeHarvester harvester)
        => harvester.TreeToBeCut.HasValue || harvester.CurrentJob.ValueOrNull is TreeHarvestingJob;

    private static void MarkActiveHarvestClaim(TreeHarvester harvester, ForestryTower tower, TreeId tree)
    {
        if (s_futureHarvestByHarvester.TryGetValue(harvester.Id, out FutureHarvestClaim? previous)
            && previous.Tree != tree)
        {
            ReleaseFutureHarvestClaim(harvester.Id);
        }

        if (s_futureHarvestByTree.TryGetValue(tree, out FutureHarvestClaim? previousOwner)
            && previousOwner.Harvester != harvester)
        {
            ReleaseFutureHarvestClaim(previousOwner.Harvester.Id);
        }

        if (!s_futureHarvestByHarvester.TryGetValue(harvester.Id, out FutureHarvestClaim? claim))
        {
            claim = new FutureHarvestClaim(harvester, tower, tree, activeHarvest: true);
            s_futureHarvestByHarvester[harvester.Id] = claim;
            s_futureHarvestByTree[tree] = claim;
        }
        else
        {
            claim.ActiveHarvest = true;
            s_futureHarvestByTree[tree] = claim;
        }
    }

    private static void RebuildActiveHarvestClaims()
    {
        if (s_vehicleManager == null || s_vehicleTreesManager == null)
            return;

        foreach (Vehicle vehicle in s_vehicleManager.AllVehicles)
        {
            if (!(vehicle is TreeHarvester harvester) || !harvester.TreeToBeCut.HasValue)
                continue;

            ForestryTower? tower = harvester.ForestryTower.ValueOrNull;
            TreeId tree = harvester.TreeToBeCut.Value.Id;
            if (tower == null || tower.IsDestroyed || !tower.IsEnabled || !s_vehicleTreesManager.Trees.ContainsKey(tree)
                || !tower.Area.Contains(tree.Position.CenterTile2f))
                continue;

            MarkActiveHarvestClaim(harvester, tower, tree);
            EnsurePlantingClaim(tower, harvester, tree, activeHarvest: true);
        }
    }

    private static FutureHarvestClaim? FindAndClaimFutureHarvest(TreeHarvester harvester, ForestryTower tower)
    {
        if (s_vehicleTreesManager == null || s_simLoopEvents == null || harvester.LastRefuelRequestIssue != RefuelRequestIssue.None
            || harvester.CannotWorkDueToLowFuel || !harvester.Cargo.IsEmpty)
            return null;

        TreeData? bestTree = null;
        long bestScore = long.MaxValue;
        long bestDistance = long.MaxValue;
        Percent bestGrowth = Percent.Zero;
        foreach (KeyValuePair<TreeId, TreeData> item in s_vehicleTreesManager.Trees)
        {
            TreeData tree = item.Value;
            if (!tree.IsValid
                || tree.HarvestedProductId != IdsCore.Products.Wood
                || !tower.Area.Contains(tree.Position2f)
                || !tower.Trees.Contains(tree.Id)
                || tower.IsTreeReadyForHarvest(tree.Id)
                || s_vehicleTreesManager.IsTreeSelected(tree.Id)
                || s_vehicleTreesManager.IsTreeReserved(tree.Id)
                || (s_vehicleUnreachablesManager != null && s_vehicleUnreachablesManager.GetUnreachableTreesFor(harvester).Contains(tree.Id))
                || s_futureHarvestByTree.ContainsKey(tree.Id))
            {
                continue;
            }

            Duration age = s_simLoopEvents.CurrentStep - tree.PlantedAtTick;
            long thresholdTicks = tower.TargetHarvestPercent.ApplyCeiled(tree.Proto.GetTreeMaxAge().Ticks);
            long untilThreshold = Math.Max(0L, thresholdTicks - age.Ticks);
            Percent growth = tree.GetGrowthPercentAt(s_simLoopEvents.CurrentStep);
            long distance = harvester.Position2f.DistanceTo(tree.Position2f).ToIntRounded();
            long travelTicks = distance * 60L;
            long score = Math.Max(untilThreshold, travelTicks);
            if (score < bestScore
                || (score == bestScore && (distance < bestDistance
                    || (distance == bestDistance && (!bestTree.HasValue || tree.Id.CompareTo(bestTree.Value.Id) < 0)))))
            {
                bestTree = tree;
                bestScore = score;
                bestDistance = distance;
                bestGrowth = growth;
            }
        }

        if (!bestTree.HasValue)
            return null;

        FutureHarvestClaim claim = new FutureHarvestClaim(harvester, tower, bestTree.Value.Id);
        s_futureHarvestByHarvester[harvester.Id] = claim;
        s_futureHarvestByTree[claim.Tree] = claim;
        EnsurePlantingClaim(tower, harvester, claim.Tree, activeHarvest: false);
        LogDebug($"Future harvest claim: harvester {harvester.Id} -> tree {claim.Tree}.");
        LogDebug($"[DEBUG-HVST] Future claim details: harvester={harvester.Id}, tree={claim.Tree}, growth={bestGrowth}, threshold={tower.TargetHarvestPercent}, scoreTicks={bestScore}, straightDistance={bestDistance}.");
        return claim;
    }

    private static void EnsurePlantingClaim(ForestryTower tower, TreeHarvester harvester, TreeId tree, bool activeHarvest)
    {
        if (!s_vehicleOptimizationApplied || s_vehicleTreesManager == null || !s_vehicleTreesManager.Trees.ContainsKey(tree)
            || !tower.IsEnabled || !tower.Area.Contains(tree.Position.CenterTile2f))
            return;

        if (s_futurePlantingByTile.TryGetValue(tree.Position.AsFull, out FuturePlantingClaim? existing))
        {
            if (!IsValidPlantingClaim(existing))
            {
                ReleasePlantingClaim(existing.Planter.Id);
                existing = null;
            }
        }

        if (s_futurePlantingByTile.TryGetValue(tree.Position.AsFull, out existing))
        {
            if (existing.Tower == tower)
            {
                if (activeHarvest)
                    existing.ActiveHarvest = true;
                return;
            }

            if (!activeHarvest || existing.ActiveHarvest)
                return;

            ReleasePlantingClaim(existing.Planter.Id);
        }

        TreePlanter? bestPlanter = null;
        long bestDistance = long.MaxValue;
        foreach (Vehicle vehicle in tower.AllVehicles)
        {
            if (!(vehicle is TreePlanter planter) || !IsWorkReadyPlanter(planter, tower))
                continue;

            if (s_futurePlantingByPlanter.TryGetValue(planter.Id, out FuturePlantingClaim? planterClaim))
            {
                if (planterClaim.ActiveHarvest || !activeHarvest)
                    continue;
                ReleasePlantingClaim(planter.Id);
            }

            long distance = planter.GroundPositionTile2i.DistanceSqrTo(tree.Position);
            if (distance < bestDistance || (distance == bestDistance && (bestPlanter == null || planter.Id.Value < bestPlanter.Id.Value)))
            {
                bestPlanter = planter;
                bestDistance = distance;
            }
        }

        if (bestPlanter == null)
            return;

        FuturePlantingClaim claim = new FuturePlantingClaim(bestPlanter, tower, tree, activeHarvest);
        s_futurePlantingByPlanter[bestPlanter.Id] = claim;
        s_futurePlantingByTile[claim.Tile] = claim;
        TryStagePlanter(claim);
        LogDebug($"{(activeHarvest ? "Active" : "Future")} planting claim: planter {bestPlanter.Id} -> tile {claim.Tile}.");
    }

    private static bool IsWorkReadyPlanter(TreePlanter planter, ForestryTower tower)
    {
        if (!IsAssignedOptimizedVehicle(planter, out ForestryTower assignedTower) || assignedTower != tower || !planter.IsNotEmpty
            || planter.LastRefuelRequestIssue != RefuelRequestIssue.None || planter.CannotWorkDueToLowFuel)
            return false;

        if (!planter.HasJobs)
            return true;

        return s_stagingJobsByVehicle.TryGetValue(planter.Id, out NavigateToJob? staging)
            && planter.CurrentJob.ValueOrNull == staging;
    }

    private static bool TryStagePlanter(FuturePlantingClaim claim)
    {
        if (s_plantingGoalFactory == null || s_navigateToJobFactory == null || !IsValidPlantingClaim(claim))
            return false;
        if (s_vehicleUnreachablesManager != null && s_vehicleUnreachablesManager.GetUnreachableTilesFor(claim.Planter).Contains(claim.Tile.AsSlim))
        {
            ReleasePlantingClaim(claim.Planter.Id);
            return false;
        }
        if (s_stagingJobsByVehicle.TryGetValue(claim.Planter.Id, out NavigateToJob? existingJob))
        {
            if (!existingJob.IsDestroyed && claim.Planter.CurrentJob.ValueOrNull == existingJob)
                return true;
            s_stagingJobsByVehicle.Remove(claim.Planter.Id);
        }

        if (claim.Planter.Position2f.DistanceSqrTo(claim.Tile.CenterTile2f) <= (2 * claim.Planter.Prototype.TreePlantDistance).Squared)
            return false;

        PlantingVehicleGoal goal = s_plantingGoalFactory.Create(claim.Tile, claim.Planter.Prototype.TreePlantDistance);
        NavigateToJob job = s_navigateToJobFactory.EnqueueJob(claim.Planter, goal, navigateClosebyIsSufficient: false, asTrueJob: false);
        s_stagingJobsByVehicle[claim.Planter.Id] = job;
        return true;
    }

    private static bool TryStageHarvester(FutureHarvestClaim claim)
    {
        if (claim.ActiveHarvest)
            return false;
        if (s_treeGoalFactory == null || s_navigateToJobFactory == null)
        {
            LogDebug($"[DEBUG-HVST] Staging unavailable because navigation factories are missing: harvester={claim.Harvester.Id}, tree={claim.Tree}.");
            return false;
        }
        if (!IsValidFutureHarvestClaim(claim))
        {
            LogDebug($"[DEBUG-HVST] Staging rejected because future claim is invalid: harvester={claim.Harvester.Id}, tree={claim.Tree}.");
            return false;
        }
        if (s_vehicleUnreachablesManager != null && s_vehicleUnreachablesManager.GetUnreachableTreesFor(claim.Harvester).Contains(claim.Tree))
        {
            LogDebug($"[DEBUG-HVST] Staging rejected as unreachable: harvester={claim.Harvester.Id}, tree={claim.Tree}.");
            ReleaseFutureHarvestClaim(claim.Harvester.Id);
            return false;
        }
        if (s_stagingJobsByVehicle.TryGetValue(claim.Harvester.Id, out NavigateToJob? existingJob))
        {
            if (!existingJob.IsDestroyed && claim.Harvester.CurrentJob.ValueOrNull == existingJob)
                return true;
            s_stagingJobsByVehicle.Remove(claim.Harvester.Id);
        }

        if (claim.Harvester.Position2f.DistanceSqrTo(claim.Tree.Position.CenterTile2f) <= (2 * claim.Harvester.Prototype.TreeHarvestDistance).Squared)
        {
            LogDebug($"[DEBUG-HVST] Staging skipped because harvester is already near tree: harvester={claim.Harvester.Id}, tree={claim.Tree}, position={claim.Harvester.Position2f}.");
            return false;
        }

        TreeVehicleGoal goal = s_treeGoalFactory.Create(claim.Tree, claim.Harvester.Prototype.TreeHarvestDistance);
        NavigateToJob job = s_navigateToJobFactory.EnqueueJob(claim.Harvester, goal, navigateClosebyIsSufficient: false, asTrueJob: false);
        s_stagingJobsByVehicle[claim.Harvester.Id] = job;
        LogDebug($"[DEBUG-HVST] Staging enqueued: harvester={claim.Harvester.Id}, tree={claim.Tree}, from={claim.Harvester.Position2f}, job={job.Id}.");
        return true;
    }

    private static bool IsValidFutureHarvestClaim(FutureHarvestClaim claim)
    {
        if (!IsAssignedOptimizedVehicle(claim.Harvester, out ForestryTower tower)
            || tower != claim.Tower
            || !claim.Tower.Area.Contains(claim.Tree.Position.CenterTile2f)
            || !claim.Tower.Trees.Contains(claim.Tree)
            || s_vehicleTreesManager == null
            || !s_vehicleTreesManager.Trees.ContainsKey(claim.Tree))
            return false;

        if (claim.ActiveHarvest)
            return true;

        return
            !s_vehicleTreesManager.IsTreeSelected(claim.Tree)
            && (s_vehicleUnreachablesManager == null || !s_vehicleUnreachablesManager.GetUnreachableTreesFor(claim.Harvester).Contains(claim.Tree))
            && !claim.Tower.IsTreeReadyForHarvest(claim.Tree);
    }

    private static bool IsValidPlantingClaim(FuturePlantingClaim claim)
    {
        if (!IsAssignedOptimizedVehicle(claim.Planter, out ForestryTower tower) || tower != claim.Tower || !claim.Planter.IsNotEmpty)
            return false;
        if (!claim.Tower.Area.Contains(claim.Tile.CenterTile2f))
            return false;
        if (s_vehicleTreesManager == null)
            return false;
        if (s_vehicleTreesManager.Trees.ContainsKey(claim.Tree))
            return true;
        return s_vehicleTreesManager.Stumps.ContainsKey(new TreeId(claim.Tile.AsSlim));
    }

    private static void OnTreeRemoved(TreeData tree)
    {
        if (s_futureHarvestByTree.TryGetValue(tree.Id, out FutureHarvestClaim? harvestClaim))
            ReleaseFutureHarvestClaim(harvestClaim.Harvester.Id, releasePlantingForTree: false);

        if (!s_futurePlantingByTile.TryGetValue(tree.Position2i, out FuturePlantingClaim? claim))
            return;
        if (s_vehicleTreesManager == null || !s_vehicleTreesManager.Stumps.ContainsKey(tree.Id))
            ReleasePlantingClaim(claim.Planter.Id);
    }

    private static void ReleaseFutureHarvestClaim(EntityId harvesterId, bool releasePlantingForTree = true)
    {
        if (!s_futureHarvestByHarvester.TryGetValue(harvesterId, out FutureHarvestClaim? claim))
            return;
        s_futureHarvestByHarvester.Remove(harvesterId);
        s_futureHarvestByTree.Remove(claim.Tree);
        if (releasePlantingForTree)
            ReleasePlantingClaimForTree(claim.Tree);
        CancelStaging(harvesterId);
    }

    private static void ReleasePlantingClaimForTree(TreeId tree)
    {
        if (s_futurePlantingByTile.TryGetValue(tree.Position.AsFull, out FuturePlantingClaim? claim)
            && claim.Tree == tree)
        {
            ReleasePlantingClaim(claim.Planter.Id);
        }
    }

    private static void ReleasePlantingClaim(EntityId planterId)
    {
        if (!s_futurePlantingByPlanter.TryGetValue(planterId, out FuturePlantingClaim? claim))
            return;
        s_futurePlantingByPlanter.Remove(planterId);
        s_futurePlantingByTile.Remove(claim.Tile);
        CancelStaging(planterId);
    }

    private static void CancelStaging(EntityId vehicleId)
    {
        if (!s_stagingJobsByVehicle.TryGetValue(vehicleId, out NavigateToJob? job))
            return;
        s_stagingJobsByVehicle.Remove(vehicleId);
        if (!job.IsDestroyed)
            job.RequestCancel();
    }

    private static void ValidateClaims()
    {
        foreach (EntityId id in new List<EntityId>(s_futureHarvestByHarvester.Keys))
        {
            if (!s_futureHarvestByHarvester.TryGetValue(id, out FutureHarvestClaim? claim))
                continue;

            if (claim.ActiveHarvest && !HasActiveHarvestJob(claim.Harvester) && s_vehicleTreesManager != null
                && !s_vehicleTreesManager.IsTreeReserved(claim.Tree)
                && !s_vehicleTreesManager.IsTreeSelected(claim.Tree))
            {
                claim.ActiveHarvest = false;
            }

            if (!IsValidFutureHarvestClaim(claim))
                ReleaseFutureHarvestClaim(id);
        }

        foreach (EntityId id in new List<EntityId>(s_futurePlantingByPlanter.Keys))
        {
            if (!s_futurePlantingByPlanter.TryGetValue(id, out FuturePlantingClaim? claim) || !IsValidPlantingClaim(claim))
                ReleasePlantingClaim(id);
        }

        foreach (EntityId id in new List<EntityId>(s_stagingJobsByVehicle.Keys))
        {
            if (s_stagingJobsByVehicle[id].IsDestroyed)
                s_stagingJobsByVehicle.Remove(id);
        }
    }

    private static void ReleaseAllClaimsAndStaging()
    {
        foreach (EntityId id in new List<EntityId>(s_futureHarvestByHarvester.Keys))
            ReleaseFutureHarvestClaim(id);
        foreach (EntityId id in new List<EntityId>(s_futurePlantingByPlanter.Keys))
            ReleasePlantingClaim(id);
        foreach (NavigateToJob job in new List<NavigateToJob>(s_stagingJobsByVehicle.Values))
        {
            if (!job.IsDestroyed)
                job.RequestCancel();
        }
        s_stagingJobsByVehicle.Clear();
        s_futureHarvestByTree.Clear();
        s_futurePlantingByTile.Clear();
    }
}
