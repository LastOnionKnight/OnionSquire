using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using OnionSquire.Models;

namespace OnionSquire.Services;

/// <summary>
/// Categorizes items based on Lumina ItemSearchCategory into RetainerRoles.
/// </summary>
public class ItemCategorizer
{
    private readonly IDataManager? dataManager;

    // FFXIV ItemSearchCategory IDs:
    // 44..52 = Crafting materials (Cloth, Leather, Bone, Metal, Stone, Reagents, etc.)
    // 53 = Dyes, 54 = Parts
    // 55..57 = Seeds, Soil, Fishing Bait, Seafood, Produce
    // 43 = Medicine / Potions, 42 = Meals
    private static readonly HashSet<uint> FoodSearchCategories = new()
    {
        42, // Meals
        43  // Medicine
    };

    private static readonly HashSet<uint> GatheringSearchCategories = new()
    {
        55, // Fishing Tackle
        56, // Seafood
        57  // Produce
    };

    private static readonly HashSet<uint> CraftingSearchCategories = new()
    {
        44, // Ingredients
        45, // Lumber
        46, // Cloth
        47, // Leather
        48, // Bone
        49, // Metal
        50, // Stone
        51, // Reagents
        52, // Colors (Dyes)
        53, // Building Materials
        54  // Parts
    };

    public ItemCategorizer(IDataManager? dataManager)
    {
        this.dataManager = dataManager;
    }

    /// <summary>
    /// Evaluates which RetainerRole an item belongs to based on its ItemSearchCategory.
    /// </summary>
    public RetainerRole CategorizeItem(uint searchCategoryId)
    {
        if (FoodSearchCategories.Contains(searchCategoryId))
            return RetainerRole.FoodAndPotions;

        if (GatheringSearchCategories.Contains(searchCategoryId))
            return RetainerRole.Gathering;

        if (CraftingSearchCategories.Contains(searchCategoryId))
            return RetainerRole.Crafting;

        return RetainerRole.None;
    }

    public RetainerRole CategorizeItemId(uint itemId)
    {
        if (dataManager == null) return RetainerRole.None;

        try
        {
            var itemSheet = dataManager.GetExcelSheet<Item>();
            if (itemSheet != null && itemSheet.TryGetRow(itemId, out var item))
            {
                uint searchCatId = item.ItemSearchCategory.RowId;
                return CategorizeItem(searchCatId);
            }
        }
        catch
        {
        }

        return RetainerRole.None;
    }
}
