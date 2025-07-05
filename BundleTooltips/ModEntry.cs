using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using SVObject = StardewValley.Object;

//Created by Musbah Sinno

//Resources:Got GetHoveredItemFromMenu and DrawHoverTextbox from a CJB mod and modified them to suit my needs.
//          They also inspired me to make GetHoveredItemFromToolbar, so thank you CJB
//https://github.com/CJBok/SDV-Mods/blob/master/CJBShowItemSellPrice/StardewCJB.cs

namespace StardewValleyBundleTooltips;

/// <summary>The mod entry point.</summary>
public class ModEntry : Mod
{
    /// <summary>
    /// Needed to make sure essential variables are loaded before running what needs them
    /// </summary>
    private bool IsLoaded = false;

    /// <summary>
    /// True if the CJB Sell Item Price mod is loaded
    /// </summary>
    private bool IsCJBSellItemPriceLoaded;

    /// <summary>
    /// True if the Ui Info Suite mod is loaded
    /// </summary>
    private bool IsUiInfoSuiteLoaded;

    /// <summary>
    /// A mapping of all seed ids to the crops they grow
    /// To efficiently look up what seeds crops come from
    /// 
    /// Used to display on seed items so you know whether it's worth buying 
    /// for the community center
    /// </summary>
    private readonly Dictionary<string, string> SeedToCropMap = [];

    /// <summary>
    /// A mapping of all crop ids to the seeds they grow
    /// To efficiently look up what seeds crops come from
    /// 
    /// Used to display on seed items so you know whether a seed maker can be used to get an item
    /// for the community center
    /// </summary>
    private readonly Dictionary<string, string> CropToSeedMap = [];

    private Item ToolbarItem;
    private List<string> ItemIdsInBundles = [];
    private Dictionary<int, List<BundleItem>> Bundles = [];
    private readonly Dictionary<int, RoomAndBundleName> RoomAndBundleNames = [];

    /*********
    ** Public methods
    *********/
    /// <summary>The mod entry point, called after the mod is first loaded.</summary>
    /// <param name="helper">Provides simplified APIs for writing mods.</param>
    public override void Entry(IModHelper helper)
    {
        IsCJBSellItemPriceLoaded = this.Helper.ModRegistry.IsLoaded("CJBok.ShowItemSellPrice");
        IsUiInfoSuiteLoaded = this.Helper.ModRegistry.IsLoaded("Cdaragorn.UiInfoSuite")
                             || this.Helper.ModRegistry.IsLoaded("Annosz.UiInfoSuite2");

        //Events
        helper.Events.GameLoop.SaveLoaded += SaveEvents_AfterLoad;
        helper.Events.Display.RenderedHud += GraphicsEvents_OnPostRenderHudEvent;
        helper.Events.Display.RenderingHud += GraphicsEvents_OnPreRenderHudEvent;
        helper.Events.Display.RenderedActiveMenu += GraphicsEvents_OnPostRenderGuiEvent;
    }


    /*********
    ** Private methods
    *********/
    /// <summary>The method invoked when the player presses a keyboard button.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event data.</param>
    /// 
    private void SaveEvents_AfterLoad(object sender, SaveLoadedEventArgs e)
    {
        PopulateCropDataCache();
        ItemIdsInBundles = [];
        Bundles = GetBundles();
        IsLoaded = true;
    }

    /// <summary>
    /// Fills the crop to seed/seed to crop data so we can cache them for
    /// quick lookups
    /// </summary>
    private void PopulateCropDataCache()
    {
        var allCropData = DataLoader.Crops(Game1.content);
        SeedToCropMap.Clear();
        CropToSeedMap.Clear();

        foreach(var cropDataKV in allCropData)
        {
            var seedId = cropDataKV.Key;
            var cropData = cropDataKV.Value;

            if (cropData != default && !string.IsNullOrWhiteSpace(cropData.HarvestItemId))
            {
                var cropId = cropData.HarvestItemId;
                if (cropId != seedId) // Coffee beans, specifically
                {
                    SeedToCropMap.Add(seedId, cropId);
                    CropToSeedMap.Add(cropId, seedId);
                }
            }
        }
    }

    private void GraphicsEvents_OnPreRenderHudEvent(object sender, RenderingHudEventArgs e)
    {
        //I have to get it on preRendering because it gets set to null post
        ToolbarItem = GetHoveredItemFromToolbar();
    }

    private void GraphicsEvents_OnPostRenderHudEvent(object sender, RenderedHudEventArgs e)
    {
        if (IsLoaded &&
            !Game1.MasterPlayer.mailReceived.Contains("JojaMember") &&
            Game1.activeClickableMenu == null &&
            ToolbarItem != null)
        {
            ShowTooltipForItem(ToolbarItem, isItFromToolbar: true);
            ToolbarItem = null;
        }
    }

    private void GraphicsEvents_OnPostRenderGuiEvent(object sender, RenderedActiveMenuEventArgs e)
    {
        if (IsLoaded &&
            !Game1.MasterPlayer.mailReceived.Contains("JojaMember") &&
            Game1.activeClickableMenu != null)
        {
            Item item = GetHoveredItemFromMenu(Game1.activeClickableMenu);
            if (item != null)
            {
                ShowTooltipForItem(item, isItFromToolbar: false);
            }
        }
    }

    /// <summary>
    /// Shows a hover text box for the given item
    /// This handles crops and seeds
    /// - Shows the tooltip for a hovered crop if a seed is required
    /// - Shows the tooltip for a hovered seed if a crop is required
    /// </summary>
    /// <param name="item">The hovered over item</param>
    /// <param name="isItFromToolbar">Whether we're hoving over a toolbar</param>
    private void ShowTooltipForItem(
        Item item,
        bool isItFromToolbar)
    {
        const string seedPrefix = "Seed";
        const string cropPrefix = "Crop";

        var tooltipDataByRoom = GetTooltipData(item);

        if (CropToSeedMap.ContainsKey(item.ItemId))
        {
            GetTooltipData(
                ItemRegistry.Create(CropToSeedMap[item.ItemId]),
                tooltipDataByRoom,
                seedPrefix);
        }
        else if (SeedToCropMap.ContainsKey(item.ItemId))
        {
            GetTooltipData(
                ItemRegistry.Create(SeedToCropMap[item.ItemId]),
                tooltipDataByRoom,
                cropPrefix);
        }

        DrawTooltip(item, isItFromToolbar, tooltipDataByRoom);
    }

    /// <summary>
    /// Gets the data used to display the tooltip
    /// </summary>
    /// <param name="item">The hovered over item</param>
    /// <param name="tooltipDataByRoom">
    /// The current data for tooltips (default to null)
    /// This will be added to if passed in
    /// </param>
    /// <param name="tooltipPrefix">The prefix to include in the tooltip, if any</param>
    /// <returns>The tooltip data, mapping each room to the list of data to display for it</returns>
    private Dictionary<string, List<ItemTooltipData>> GetTooltipData(
        Item item,
        Dictionary<string, List<ItemTooltipData>> tooltipDataByRoom = default,
        string tooltipPrefix = "")
    {
        if (!ItemIdsInBundles.Any(
            id => item.QualifiedItemId == ItemRegistry.QualifyItemId(id)))
        {
            return [];
        }

        CommunityCenter communityCenter =
            Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
        tooltipDataByRoom ??= [];

        foreach (KeyValuePair<int, List<BundleItem>> bundleKV in Bundles)
        {
            foreach (BundleItem bundleItem in bundleKV.Value)
            {
                if (bundleItem == default) { continue; }

                ItemQualities quality = bundleItem.ItemQuality;

                if ((item is SVObject svObject) &&
                    item.Stack != 0 &&

                    // Use qualified ids since normal ids can overlap
                    bundleItem.QualifiedItemId == item.QualifiedItemId)
                {
                    var bundleId = bundleKV.Key;
                    var isItemInBundleSlot = communityCenter.bundles[bundleId][bundleItem.IndexInBundle];
                    if (!isItemInBundleSlot)
                    {
                        var roomName = RoomAndBundleNames[bundleId].RoomName;
                        if (!tooltipDataByRoom.ContainsKey(roomName))
                        {
                            tooltipDataByRoom.Add(roomName, []);
                        }
                        tooltipDataByRoom[roomName].Add(
                            new ItemTooltipData
                            {
                                RoomName = RoomAndBundleNames[bundleId].RoomName,
                                BundleName = RoomAndBundleNames[bundleId].BundleName,
                                ItemQuality = bundleItem.ItemQuality,
                                Amount = bundleItem.Amount,
                                TooltipPrefix = tooltipPrefix
                            }
                        );
                    }
                }
            }
        }

        return tooltipDataByRoom;
    }

    /// <summary>
    /// Draws the tooltip for the hovered item
    /// </summary>
    /// <param name="item">The hovered over item</param>
    /// <param name="isItFromToolbar">Whether we're hoving over a toolbar</param>
    /// <param name="tooltipDataByRoom">The data to use for the tooltip</param>
    private void DrawTooltip(
        Item item,
        bool isItFromToolbar,
        Dictionary<string, List<ItemTooltipData>> tooltipDataByRoom)
    {
        if (tooltipDataByRoom.Count == 0) { return; }

        var tooltipText = "";
        foreach (var tooltipDataKV in tooltipDataByRoom)
        {
            if (!string.IsNullOrWhiteSpace(tooltipText))
            {
                tooltipText += "\n\n";
            }

            tooltipText += $"{tooltipDataKV.Key}";
            foreach (var tooltipData in tooltipDataKV.Value)
            {
                var prefix = !string.IsNullOrWhiteSpace(tooltipData.TooltipPrefix)
                    ? $"[{tooltipData.TooltipPrefix}] "
                    : string.Empty;

                tooltipText += $"\n {prefix}{tooltipData.Amount} x {tooltipData.BundleName}";
                if (tooltipData.ItemQuality > 0)
                {
                    tooltipText += $" ({tooltipData.ItemQuality})";
                }
            }
        }

        DrawHoverTextBox(Game1.smallFont, tooltipText, isItFromToolbar, item.Stack);
    }

    private Item GetHoveredItemFromMenu(IClickableMenu menu)
    {
        // game menu
        if (menu is GameMenu gameMenu)
        {
            IClickableMenu page = this.Helper.Reflection.GetField<List<IClickableMenu>>(gameMenu, "pages").GetValue()[gameMenu.currentTab];
            if (page is InventoryPage)
                return this.Helper.Reflection.GetField<Item>(page, "hoveredItem").GetValue();
        }
        // from inventory UI (so things like shops and so on)
        else if (menu is MenuWithInventory inventoryMenu)
        {
            return inventoryMenu.hoveredItem;
        }

        return null;
    }

    private Item GetHoveredItemFromToolbar()
    {
        foreach (IClickableMenu menu in Game1.onScreenMenus)
        {
            if (menu is Toolbar toolbar)
            {
                return this.Helper.Reflection.GetField<Item>(menu, "hoverItem").GetValue();
            }
        }

        return null;
    }

    private void DrawHoverTextBox(SpriteFont font, string description, bool isItFromToolbar, int itemStack)
    {
        Vector2 stringLength = font.MeasureString(description);
        int width = (int)stringLength.X + Game1.tileSize / 2 + 40;
        int height = (int)stringLength.Y + Game1.tileSize / 3 + 5;

        int x = (int)(Mouse.GetState().X / Game1.options.zoomLevel) - Game1.tileSize / 2 - width;
        int y = (int)(Mouse.GetState().Y / Game1.options.zoomLevel) + Game1.tileSize / 2;

        //So that the tooltips don't overlap
        if ((IsCJBSellItemPriceLoaded || IsUiInfoSuiteLoaded) && !isItFromToolbar)
        {
            if (itemStack > 1)
                y += 95;
            else
                y += 55;
        }

        if (x < 0)
            x = 0;

        if (y + height > Game1.graphics.GraphicsDevice.Viewport.Height)
            y = Game1.graphics.GraphicsDevice.Viewport.Height - height;

        IClickableMenu.drawTextureBox(Game1.spriteBatch, Game1.menuTexture, new Rectangle(0, 256, 60, 60), x, y, width, height, Color.White);
        Utility.drawTextWithShadow(Game1.spriteBatch, description, font, new Vector2(x + Game1.tileSize / 4, y + Game1.tileSize / 4), Game1.textColor);
    }

    private Dictionary<int, List<BundleItem>> GetBundles()
    {
        ItemIdsInBundles.Clear();
        RoomAndBundleNames.Clear();

        Dictionary<string, string> dictionary = Game1.netWorldState.Value.BundleData;
        Dictionary<int, List<BundleItem>> bundles = [];

        foreach (KeyValuePair<string, string> keyValuePair in dictionary)
        {
            // Format of the values are itemID itemAmount itemQuality
            string[] split = keyValuePair.Key.Split('/');
            string roomName = Game1.content
                .LoadString($"Strings\\Locations:CommunityCenter_AreaName_{split[0].Replace(" ", "")}");

            string[] bundleDataTokens = keyValuePair.Value.Split('/');
            string bundleName = bundleDataTokens[^1].Replace("\n", "");
            int bundleIndex = Convert.ToInt32(split[1]);

            //if bundleIndex is between 23 and 26, then they're vault bundles so don't add to dictionary
            if (!(bundleIndex >= 23 && bundleIndex <= 26))
            {
                //creating an array of items[i][j] , i is the item index, j=0 itemId, j=1 itemAmount, j=2 itemQuality, j=3 order of the item for its own bundle
                string[] allItems = bundleDataTokens[2].Split(' ');
                int allItemsLength = allItems.Length / 3;

                List<BundleItem> bundleItems = [];

                for (int j = 0, i = 0; j < allItemsLength; j++, i += 3)
                {
                    BundleItem bundleItem = new(
                        id: allItems[i],
                        amount: allItems[i + 1],
                        itemQuality: allItems[i + 2],
                        indexInBundle: i / 3
                    );
                    bundleItems.Add(bundleItem);
                    ItemIdsInBundles.Add(bundleItem.Id);
                }

                bundles.Add(bundleIndex, bundleItems);
                RoomAndBundleNames.Add(bundleIndex, new RoomAndBundleName
                {
                    RoomName = roomName,
                    BundleName = bundleName,
                });
            }
        }

        ItemIdsInBundles = ItemIdsInBundles.Distinct().ToList();
        return bundles;
    }
}
