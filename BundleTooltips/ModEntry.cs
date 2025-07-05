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

    Item ToolbarItem;
    List<string> ItemIdsInBundles = [];
    Dictionary<int, List<BundleItem>> Bundles;
    Dictionary<int, RoomAndBundleName> RoomAndBundleNames;


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
        // This will be filled with the itemIDs of every item in every bundle (for a fast search without details)
        ItemIdsInBundles = [];
        Bundles = GetBundles();
        IsLoaded = true;
    }

    private void GraphicsEvents_OnPreRenderHudEvent(object sender, RenderingHudEventArgs e)
    {
        //I have to get it on preRendering because it gets set to null post
        ToolbarItem = GetHoveredItemFromToolbar();
    }

    private void GraphicsEvents_OnPostRenderHudEvent(object sender, RenderedHudEventArgs e)
    {
        if (IsLoaded && !Game1.MasterPlayer.mailReceived.Contains("JojaMember") && Game1.activeClickableMenu == null && ToolbarItem != null)
        {
            PopulateHoverTextBoxAndDraw(ToolbarItem,true);
            ToolbarItem = null;
        }
    }

    private void GraphicsEvents_OnPostRenderGuiEvent(object sender, RenderedActiveMenuEventArgs e)
    {
        if (IsLoaded && !Game1.MasterPlayer.mailReceived.Contains("JojaMember") && Game1.activeClickableMenu != null)
        {
            Item item = this.GetHoveredItemFromMenu(Game1.activeClickableMenu);
            if (item != null)
                PopulateHoverTextBoxAndDraw(item,false);
        }
    }

    private void PopulateHoverTextBoxAndDraw(Item item, bool isItFromToolbar)
    {
        if (!ItemIdsInBundles.Any(
            id => item.QualifiedItemId == ItemRegistry.QualifyItemId(id)))
        {
            return;
        }

        CommunityCenter communityCenter = 
            Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
        Dictionary<string, List<ItemTooltipData>> tooltipDataByRoom = [];

        foreach (KeyValuePair<int, List<BundleItem>> bundleKV in Bundles)
        {
            foreach (BundleItem bundleItem in bundleKV.Value)
            {
                if (bundleItem == default) { continue; }

                ItemQualities quality = bundleItem.ItemQuality;
                
                if ((item is SVObject svObject) && 
                    item.Stack != 0 && 
                    bundleItem.QualifiedItemId == item.QualifiedItemId) // Use this since normal Ids can overlap
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
                                Amount = bundleItem.Amount
                            }
                        );
                    }
                }
            }
        }

        if (tooltipDataByRoom.Count == 0)
        {
            return;
        }

        var tooltipText = "";
        foreach (var tooltipDataKV in tooltipDataByRoom)
        {
            if (!string.IsNullOrWhiteSpace(tooltipText))
            {
                tooltipText += "\n\n";
            }

            tooltipText += $"{tooltipDataKV.Key}";
            foreach(var tooltipData in tooltipDataKV.Value)
            {
                tooltipText += $"\n {tooltipData.Amount} x {tooltipData.BundleName}";
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
        Dictionary<string, string> dictionary = Game1.netWorldState.Value.BundleData;

        Dictionary<int, List<BundleItem>> bundles = [];
        RoomAndBundleNames = [];

        foreach (KeyValuePair<string, string> keyValuePair in dictionary)
        {
            // Format of the values are itemID itemAmount itemQuality
            string[] split = keyValuePair.Key.Split('/');
            string roomName = Game1.content
                .LoadString($"Strings\\Locations:CommunityCenter_AreaName_{split[0].Replace(" ", "")}");

            string[] bundleDataTokens = keyValuePair.Value.Split('/');
            string bundleName = bundleDataTokens[^1].Replace("\n","");
            int bundleIndex = Convert.ToInt32(split[1]);

            //if bundleIndex is between 23 and 26, then they're vault bundles so don't add to dictionary
            if (!(bundleIndex >= 23 && bundleIndex <= 26))
            {
                //creating an array of items[i][j] , i is the item index, j=0 itemId, j=1 itemAmount, j=2 itemQuality, j=3 order of the item for its own bundle
                string[] allItems = bundleDataTokens[2].Split(' ');
                int allItemsLength = allItems.Length / 3;
                
                List<BundleItem> bundleItems = [];

                for(int j = 0, i = 0; j < allItemsLength; j++, i += 3)
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
