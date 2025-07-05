using StardewValley;

namespace StardewValleyBundleTooltips;

public class BundleItem
{
    /// <summary>
    /// The item id (not qualified)
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Computes the qualified item id from the id
    /// </summary>
    public string QualifiedItemId
    {
        get => ItemRegistry.QualifyItemId(Id);
    }

    /// <summary>
    /// The amount of the item required in the bundle
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// The min quality of the item required
    /// </summary>
    public ItemQualities ItemQuality { get; set; }

    /// <summary>
    /// The index the item appears in the bundle
    /// </summary>
    public int IndexInBundle { get; set; }

    public BundleItem(
        string id,
        string amount,
        string itemQuality,
        int indexInBundle)
    {
        Id = id;
        Amount = int.Parse(amount);
        ItemQuality = (ItemQualities)int.Parse(itemQuality);
        IndexInBundle = indexInBundle;
    }
}
