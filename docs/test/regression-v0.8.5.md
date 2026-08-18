# AFD Regression Test Plan — CoI v0.8.5

Covers the areas most likely to surface regressions from v0.8.5 changes as they apply to Auto Forestry Designations.

## Impact summary

| v0.8.5 Change | AFD Impact | Reasoning |
|---|---|---|
| **Mine + forestry areas editable together** | Low / verify | `ForestryTowerInspector` constructor gained new params (`ForestryDesignationController`, `MultiAreaEditController`, `MineTowersManager`). AFD patches `ctors[0]` via dynamic reflection — the postfix has no dependency on specific constructor params. New "Edit Designations" button (`StartDesignationEditing`) activates `ForestryDesignationController`; AFD does not patch that controller and is unaffected. |
| **Ctrl+M shortcut opens tower area editing** | None | Activates `MultiAreaEditController`, completely separate from AFD's code paths. |
| **`ForestryDesignationController` is now a real controller** | None | AFD does not patch any `TerrainDesignationController` subclass. AFD places designations directly via `TerrainDesignationsManager.AddOrReplaceDesignation` with the `ForestryDesignator` proto — unchanged. |
| **Surface clearing respects truck zone filters** | None | Behavioral change to clearing only. No change to the designation placement APIs AFD uses. |
| **Construction/deconstruction processed in chunks** | None | Performance refactor. No API change. |
| **Vehicle connectivity manager** | None | `IVehiclePathFindingManager.PathabilityProvider` unchanged — AFD uses it for `OnlyReachableTiles` pathability checks. |
| **`ForestryTower.SetCutAtPercentage`** | None | Method still present with the same signature. AFD's postfix on this method still resolves via `nameof`. |
| **Mod config dialog negative numbers** | None | AFD's config params have non-negative bounds; no change needed. |

---

## 1 — ForestryTowerInspector UI injection

The `ForestryTowerInspector` constructor gained `ForestryDesignationController`, `MultiAreaEditController`, and `MineTowersManager` as new DI parameters. AFD's `InspectorCtorPostfix` patch targets `ctors[0]` dynamically and must survive this.

| # | Steps | Expected |
|---|-------|----------|
| 1.1 | Click any forestry tower. | Inspector opens. AFD designation panel and Forestry info panel both render without errors. Debug log shows `[AFD] Forestry designations panel inserted`. |
| 1.2 | Open multiple different forestry towers in sequence. | No double-injection (guarded by `HasBindings` equivalent). No layout corruption. |
| 1.3 | Switch to a different entity then re-click the forestry tower. | `OnActivated` postfix fires; both panels refresh to their default prompt state. |
| 1.4 | Click the new **Edit Areas** button in the inspector. | Area polygon editor opens. On close, inspector re-opens with AFD panels intact and no errors. |
| 1.5 | Click the new **Edit Designations** button (`StartDesignationEditing`). | `ForestryDesignationController` activates. AFD panels are unaffected (they are not reset by this action). No errors. |

---

## 2 — Auto-scan (core AFD functionality)

| # | Steps | Expected |
|---|-------|----------|
| 2.1 | Forestry tower with a non-empty area → trigger AFD scan. | Forestry designations placed on fertile, unoccupied tiles within the area. |
| 2.2 | Enable `OnlyFertileTiles = true` → scan. | Non-fertile tiles skipped. |
| 2.3 | Enable `AvoidTilesWithTrees = true` → scan area containing existing trees. | Tiles already occupied by a tree are skipped. |
| 2.4 | Leave **Override terrain designations** off and scan an area with existing terrain designations. | Tiles with mining, dumping, or leveling designations are skipped. |
| 2.5 | Enable **Override terrain designations** in the mod settings and scan the same area. | Forestry designations may replace the existing mining, dumping, or leveling designations. |
| 2.6 | Enable `OnlyReachableTiles = true` → scan with an isolated area. | Tiles unreachable by vehicle pathability (`IVehiclePathFindingManager.PathabilityProvider`) are skipped. No null-ref or skip warning. |
| 2.7 | Set `Target yield` to a finite value → scan. | Designations are added in existing reachable/distance priority until projected sustainable yield reaches the target or unavoidable single-designation granularity overshoots it. |
| 2.8 | Use a target close to the yield of the existing forestry area, then scan. | AFD stops after the first designation that reaches the target; any overshoot is limited to that atomic 4×4 designation, and committed designations are not removed. |
| 2.9 | Scan a large tower area with a finite target while observing the game. | Candidate collection, pathability, and yield estimation are spread across computational slices; once planning finishes, the completed designation set appears together through the vanilla bulk command. |
| 2.10 | Enable **Avoid flat tiles** and scan fully flat, near-integer, and visibly uneven areas. | Candidates whose four `HeightTilesF` vertices are all within the game's `0.0625`-tile surface tolerance of one integer are skipped; a vertex outside that tolerance remains eligible. |
| 2.11 | Run a large finite-target scan while playing, then while paused. | The game remains responsive; target planning uses approximately 10 ms per rendered frame while playing and 30 ms while paused, then the designations appear together without a progress toast. |
| 2.12 | Enable **Avoid flat tiles**, disable **Reachable tiles only**, leave Target yield at ∞, and scan a large area while playing. | AFD applies the flatness filter directly without running driving-distance/pathability search; the designation set appears promptly through the vanilla input command and does not throw `Set changed while enumerating`. |
| 2.13 | Repeat a large scan while playing with **Reachable tiles only** first enabled, then disabled. | Both modes commit promptly without filling slowly from one side and without `InvalidOperationException: Set changed while enumerating` in the game log. |

---

## 3 — SetCutAtPercentage patch

AFD patches `ForestryTower.SetCutAtPercentage` to trigger a forestry info refresh. This method is unchanged in v0.8.5.

| # | Steps | Expected |
|---|-------|----------|
| 3.1 | Open forestry tower inspector → change the **Harvesting options** dropdown. | AFD postfix fires. Debug log shows info refresh queued for that tower. No errors. |
| 3.2 | Change harvest percentage repeatedly (stress). | No errors or duplicate queuing issues. |

---

## 4 — Mark harvest-ready trees

| # | Steps | Expected |
|---|-------|----------|
| 4.1 | Enable `MarkHarvestReadyForHarvest = true` → scan area with mature trees. | `treesManager.AddToHarvest` is called for ready trees. Trees are flagged for harvest. |
| 4.2 | Verify `treesManager.IsTreeSelected` guard works: only unselected harvest-ready trees are added. | No already-selected trees are re-queued. |

---

## 5 — Cleanup

| # | Steps | Expected |
|---|-------|----------|
| 5.1 | After scan, remove the tower or shrink its area → trigger cleanup. | Orphaned forestry designations outside the new area are removed. No stale entries. |
| 5.2 | Save and reload game after a scan. | Forestry designations persist correctly. AFD panels re-initialize without errors on the reloaded save. |

---

## 6 — Per-tower saved state

| # | Steps | Expected |
|---|-------|----------|
| 6.1 | Change **Target yield** and **Avoid flat tiles** on a single forestry tower, save, quit to desktop, and reload. | Both settings are restored for that tower only. Other towers continue using the global defaults. |
| 6.2 | Collapse or expand the Forestry designations and Forestry information panels on one tower, save, quit, and reload. | Each panel restores its per-tower collapsed state. |
| 6.3 | Reset a customized tower back to the current global defaults, save, and inspect/reload. | The tower no longer needs a saved override record; after reload it still follows the current defaults. |
