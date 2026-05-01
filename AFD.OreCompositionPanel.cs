// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Buildings.Mine;
using Mafi.Core.Buildings.Towers;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Core.Terrain.Designation;
using Mafi.Core.Terrain.Resources;
using Mafi.Core.Vehicles.Excavators;
using Mafi.Localization;
using Mafi.Base;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using Row = Mafi.Unity.UiToolkit.Library.Row;

namespace AutoForestryDesignations
{
    /// <summary>
    /// Reads ore resources from a mine tower's current ManagedDesignations and
    /// displays a collapsible breakdown panel in the inspector. Independent of the
    /// scan-and-place feature -- works with any designations managed by the tower.
    /// </summary>
    internal static class OreCompositionPanel
    {
        private static TerrainDesignationsManager? s_desigManager;
        private static ProtosDb? s_protosDb;
        private static TerrainMaterialProto? s_bedrockMaterial;

        // LooseProductProto -> first TerrainMaterialProto that produces it; used to convert
        // raw terrain thickness to actual mined quantity (accounts for MinedQuantityMult and
        // the global Ore Mining Yield difficulty setting via MinedQuantityPerTileCubed).
        private static readonly Dictionary<LooseProductProto, TerrainMaterialProto> s_productToTerrainMaterial =
            new Dictionary<LooseProductProto, TerrainMaterialProto>();

        // Called when the inspector switches to a new tower; resets content to the prompt.
        private static readonly Dictionary<object, Action> s_resetContentCallbacks =
            new Dictionary<object, Action>();

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        internal static void Initialize(
            TerrainDesignationsManager? desigManager,
            ProtosDb? protosDb,
            TerrainMaterialProto? bedrockMaterial)
        {
            s_desigManager = desigManager;
            s_protosDb = protosDb;
            s_bedrockMaterial = bedrockMaterial;

            // Build lookup: LooseProductProto -> TerrainMaterialProto
            // MinedQuantityPerTileCubed on TerrainMaterialProto is kept live by the game
            // (via OnPropertyUpdated) so it always reflects the current difficulty setting.
            s_productToTerrainMaterial.Clear();
            if (protosDb != null)
            {
                foreach (var mat in protosDb.All<TerrainMaterialProto>())
                {
                    var prod = mat.MinedProduct;
                    if (prod != null && prod != LooseProductProto.Phantom
                        && !s_productToTerrainMaterial.ContainsKey(prod))
                    {
                        s_productToTerrainMaterial[prod] = mat;
                    }
                }
            }
        }

        internal static void ResetContent(object inspectorInstance) { }

        // ------------------------------------------------------------------
        // UI injection
        // ------------------------------------------------------------------

        /// <summary>
        /// Creates an "Ore Composition" collapsible panel and inserts it into
        /// mainBody at position 1 (below the AFD panel). The Refresh button reads
        /// live ManagedDesignations, so the panel works independently of AFD scans.
        /// </summary>
        internal static void Inject(Column mainBody, PropertyInfo entityProp, object inspector) { }

        /// <summary>
        /// Builds an "Ore Composition" panel and returns it. Insert the result at any position
        /// in any inspector's Column. Use the same <paramref name="key"/> for both this panel and
        /// <see cref="AutoDepthDesignation.CreateDesignationsForTower(IAreaManagingTower, object?)"/>
        /// so the panel auto-refreshes after a scan completes.
        /// </summary>
        internal static PanelWithHeader Build(Func<IAreaManagingTower?> getTower, object key)
        {
            var contentCol = new Column(2.pt());
            var promptLabel = new Label(new LocStrFormatted("Press \u21ba to scan ore composition."))
                .Color(Theme.InactiveColor);
            contentCol.Add(promptLabel);

            s_resetContentCallbacks[key] = (Action)delegate
            {
                var t = getTower();
                if (t != null)
                    PopulateContent(contentCol, t);
                else
                {
                    contentCol.Clear();
                    contentCol.Add(promptLabel);
                }
            };

            var orePanel = new PanelWithHeader()
                .Title(new LocStrFormatted("Ore Composition"),
                       new LocStrFormatted("Ore resources within this tower's current mining designations. (Does not account for potential landslides.)"));

            orePanel.Header.Add(new ButtonIcon(Button.General,
                "Assets/Unity/UserInterface/General/Repeat.svg",
                (Action)delegate
                {
                    PopulateContent(contentCol, getTower());
                })
                .Compact()
                .IconSize(14.px())
                .MarginLeft(4.pt())
                .Tooltip(new LocStrFormatted("Scan ore composition")));

            orePanel.BodyAdd(contentCol);
            return orePanel;
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private static void PopulateContent(Column col, IAreaManagingTower? tower)
        {
            col.Clear();

            var mineTower = tower as MineTower;
            if (mineTower == null || s_desigManager == null || s_protosDb == null)
            {
                col.Add(new Label(new LocStrFormatted("No tower selected.")));
                return;
            }

            // Scan live ManagedDesignations fresh each call — no cache.
            var terrMgr = s_desigManager.TerrainManager;
            var allOres = s_protosDb.All<LooseProductProto>()
                .Where(p => p != LooseProductProto.Phantom && (p.CanBeOnTerrain || p.TerrainMaterial != null))
                .Distinct()
                .ToList();
            var productSet = HybridSet<LooseProductProto>.From(allOres);
            var thicknessByProduct = new Dictionary<LooseProductProto, float>();

            foreach (TerrainDesignation designation in mineTower.ManagedDesignations)
            {
                if (designation.Prototype.Id.Value == "DumpingDesignator") continue;

                var center = designation.CenterTileCoord;
                float centerCutoff = designation.Data.CenterTargetHeight.Value.ToFloat();
                try
                {
                    var tile = terrMgr[center];
                    float surfaceHeight = terrMgr.GetHeight(center).Value.ToFloat();
                    float cumulativeDepthF = 0f;
                    TerrainLayerEnumerator enumerator = tile.EnumerateLayers();
                    while (enumerator.MoveNext())
                    {
                        TerrainMaterialThicknessSlim layer = enumerator.Current;
                        if (s_bedrockMaterial != null && layer.SlimId == s_bedrockMaterial.SlimId)
                            break;
                        float layerThick = layer.Thickness.Value.ToFloat();
                        float layerTop   = surfaceHeight - cumulativeDepthF;
                        float layerBot   = layerTop - layerThick;
                        if (layerTop <= centerCutoff) break;
                        float countedThick = layerBot >= centerCutoff ? layerThick : (layerTop - centerCutoff);
                        TerrainMaterialProto mat = layer.SlimId.ToFull(tile.TerrainManager);
                        var product = mat.MinedProduct;
                        if (productSet.Contains(product))
                        {
                            float h = countedThick * 16f;
                            if (thicknessByProduct.TryGetValue(product, out float existing))
                                thicknessByProduct[product] = existing + h;
                            else
                                thicknessByProduct[product] = h;
                        }
                        cumulativeDepthF += layerThick;
                    }
                }
                catch { }
            }

            var results = thicknessByProduct
                .Select(kvp =>
                {
                    float qty = kvp.Value;
                    if (s_productToTerrainMaterial.TryGetValue(kvp.Key, out var mat))
                        qty = kvp.Value * mat.MinedQuantityPerTileCubed.Value.ToFloat();
                    return (kvp.Key, qty);
                })
                .OrderByDescending(t => t.qty)
                .ToList();

            if (results.Count == 0)
            {
                col.Add(new Label(new LocStrFormatted("No minable designations found.")));
                return;
            }

            float total = results.Sum(r => r.qty);
            LooseProductProto? selectedPriorityProduct = AutoDepthDesignation.GetTowerExcavatorPriority(mineTower)
                ?? GetCommonExcavatorMiningFocus(mineTower);

            var cardsRow = new Row().Gap(2.pt());
            var priorityButtons = new List<(LooseProductProto Product, ButtonIcon Button, ColorRgba Color)>();

            void RefreshPriorityButtons()
            {
                foreach (var entry in priorityButtons)
                {
                    bool isSelected = entry.Product == selectedPriorityProduct;
                    entry.Button.Selected(isSelected);
                    string pName = $"<b><color=#{entry.Color.ToHexRgb()}>{entry.Product.Strings.Name.TranslatedString}</color></b>";
                    string tt = isSelected
                        ? $"Excavators set to prioritize {pName}. Click to unset."
                        : $"Set all excavators to prioritize {pName}.";
                    entry.Button.Tooltip(new LocStrFormatted(tt));
                }
            }

            foreach (var (product, thickness) in results)
            {
                float pct = total > 0f ? thickness / total * 100f : 0f;
                string name = product.Strings.Name.TranslatedString;
                var cardProduct = product;

                // Card: dark background, no border (borders managed by container)
                var card = new Column().FlexGrow(1f).Background(Theme.BackgroundDark).OverflowHidden().Padding(4.pt()).Gap(2.pt());

                // ResourcesVizColor is stored with low alpha (~40/255) for the 3D overlay;
                // force alpha=255 so the UI bar is fully opaque.
                // Rock, Dirt etc. have ResourcesVizColor=Empty (no 3D overlay color defined).
                ColorRgba barColor;
                if (product.Graphics is LooseProductProto.Gfx looseGfx && !looseGfx.ResourcesVizColor.IsEmpty)
                    barColor = looseGfx.ResourcesVizColor.SetA(255);
                else if (product.Id == Ids.Products.Dirt)
                    barColor = new ColorRgba(150, 95, 40); // earthy brown, pulled from Dirt icon
                else
                    barColor = Theme.InactiveColor;

                // Top row: icon + name/amount text
                var topRow = new Row().Gap(3.pt()).AlignItemsCenter();
                topRow.Add(new Icon(product, noTooltip: true).Size(40.px()));
                var textCol = new Column();
                textCol.Add(new Label(new LocStrFormatted(name)).FontSize(13).FontBold().NoTextWrap());
                textCol.Add(new Label(new LocStrFormatted(FormatAmount(thickness))).FontSize(12));
                topRow.Add(textCol);
                card.Add(topRow);

                var barTrack = new Row().AlignSelfStretch().Height(10.px()).AlignItemsStretch().Background(Theme.BackgroundPanelLike);
                barTrack.Add(new UiComponent().FlexGrow(pct).Background(barColor));
                barTrack.Add(new UiComponent().FlexGrow(Math.Max(0f, 100f - pct)));
                card.Add(barTrack);

                // Percentage - centered
                card.Add(new Label(new LocStrFormatted(string.Format("{0:F1}%", pct)))
                    .FontSize(13).Color(Theme.InactiveColor));
                
                // Priority button - centered
                var priorityBtn = new ButtonIcon(Button.General,
                    "Assets/Unity/UserInterface/General/Upgrade.svg",
                    (Action)delegate
                    {
                        bool clearFocus = selectedPriorityProduct == cardProduct;
                        if (clearFocus)
                        {
                            SetExcavatorMiningFocus(mineTower, null);
                            selectedPriorityProduct = null;
                        }
                        else
                        {
                            SetExcavatorMiningFocus(mineTower, cardProduct);
                            selectedPriorityProduct = cardProduct;
                        }

                        AutoDepthDesignation.SetTowerExcavatorPriority(mineTower, selectedPriorityProduct);

                        RefreshPriorityButtons();
                    })
                    .Toggleable()
                    .Selected(selectedPriorityProduct == cardProduct)
                    .Compact()
                    .IconSize(14.px())
                    .AlignSelfCenter()
                    .Tooltip(new LocStrFormatted(selectedPriorityProduct == cardProduct
                        ? $"Excavators set to prioritize <b><color=#{barColor.ToHexRgb()}>{name}</color></b>. Click to unset."
                        : $"Set all excavators to prioritize <b><color=#{barColor.ToHexRgb()}>{name}</color></b>."));
                priorityButtons.Add((cardProduct, priorityBtn, barColor));
                card.Add(priorityBtn);

                // Add rounded corners and subtle border to card
                card.BorderRadius(8);
                card.Border(1.px(), Theme.BorderColor, 8);
                
                cardsRow.Add(card);
            }
            col.Add(cardsRow);
        }

        private static string FormatAmount(float value)
        {
            if (value >= 1_000_000f)
            {
                float v = value / 1_000_000f;
                string fmt = v >= 100f ? "F0" : v >= 10f ? "F1" : "F2";
                return v.ToString(fmt) + "M";
            }
            if (value >= 1_000f)
            {
                float v = value / 1_000f;
                string fmt = v >= 100f ? "F0" : v >= 10f ? "F1" : "F2";
                return v.ToString(fmt) + "k";
            }
            {
                string fmt = value >= 100f ? "F0" : value >= 10f ? "F1" : "F2";
                return value.ToString(fmt);
            }
        }

        /// <summary>
        /// Gets the common focus product if all assigned excavators prioritize the same ore,
        /// otherwise returns null (includes mixed priorities and None).
        /// </summary>
        private static LooseProductProto? GetCommonExcavatorMiningFocus(MineTower tower)
        {
            try
            {
                if (tower == null)
                    return null;

                var excavators = tower.AllAssignedExcavators;
                if (excavators == null || excavators.Count == 0)
                    return null;

                LooseProductProto? commonProduct = null;
                bool hasValue = false;

                foreach (var excavator in excavators)
                {
                    var prioritized = excavator.PrioritizedProduct;
                    if (!prioritized.HasValue)
                        return null;

                    if (!hasValue)
                    {
                        commonProduct = prioritized.Value;
                        hasValue = true;
                    }
                    else if (commonProduct != prioritized.Value)
                    {
                        return null;
                    }
                }

                return commonProduct;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sets all excavators assigned to the tower to focus on mining the specified product.
        /// Passing null clears the focus (priority None).
        /// </summary>
        private static void SetExcavatorMiningFocus(MineTower tower, LooseProductProto? product)
        {
            try
            {
                if (tower == null)
                    return;

                AutoDepthDesignation.SetTowerExcavatorPriority(tower, product);

                var excavators = tower.AllAssignedExcavators;
                if (excavators == null || excavators.Count == 0)
                {
                    // Expected state: priority is stored and will be applied by ticker when excavators are assigned.
                    return;
                }

                foreach (var excavator in excavators)
                {
                    if (product != null)
                        excavator.SetPrioritizeProduct(Option.Some(product));
                    else
                        excavator.SetPrioritizeProduct(Option<LooseProductProto>.None);
                }

                if (product != null)
                    Log.Info($"[AFD] Set {excavators.Count} excavators to focus on {product.Strings.Name.TranslatedString}");
                else
                    Log.Info($"[AFD] Cleared mining focus for {excavators.Count} excavators");
            }
            catch (Exception ex)
            {
                Log.Warning($"[AFD] SetExcavatorMiningFocus EXCEPTION: {ex}");
            }
        }

    }
}
