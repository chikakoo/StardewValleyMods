namespace StardewValleyBundleTooltips;

public class ItemTooltipData
{
    /// <summary>
    /// The Community Center room the data is for
    /// </summary>
    public string RoomName { get; set; }

    /// <summary>
    /// The bundle name the data is for
    /// </summary>
    public string BundleName { get; set; }

    /// <summary>
    /// The lowest item quality accepted by the bundle
    /// </summary>
    public ItemQualities ItemQuality { get; set; }

    /// <summary>
    /// The amount of the item required for the bundle
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// A prefix to place before the tooltip (will be displayed in brackets)
    /// 
    /// Used to identify items like seeds and crops so the user knows that
    /// it's not this specific item for the bundle
    /// </summary>
    public string TooltipPrefix { get; set; }
}