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

    private static readonly HashSet<uint> FoodSearchCategories = new()
    {
        43, // Medicine
        44, // Ingredients
        45  // Meals
    };

    private static readonly HashSet<uint> GatheringSearchCategories = new()
    {
        30, // Fishing Tackle
        46, // Seafood
        81  // Gardening Items
    };

    private static readonly HashSet<uint> CraftingSearchCategories = new()
    {
        47, // Stone
        48, // Metal
        49, // Lumber
        50, // Cloth
        51, // Leather
        52, // Bone
        53, // Reagents
        54, // Dyes
        55, // Weapon Parts
        58, // Crystals
        59, // Catalysts
        79  // Airship/Submersible Components
    };

    private static readonly HashSet<uint> GearSearchCategories = new()
    {
        9, 10, 11, 12, 13, 14, 15, 16, 17, 18, // Weapons & Shields
        19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, // Tools
        31, 33, 35, 36, 37, // Armor: Head, Body, Legs, Hands, Feet
        39, 40, 41, 42, // Accessories: Neck, Ear, Wrist, Rings
        73, 76, 77, 78, 83, 84, 85, 86, 87, 88, 89, 91, 92 // Expansion Job Weapons
    };

    private static readonly HashSet<uint> MiscSearchCategories = new()
    {
        56, 57, 60, 65, 66, 67, 68, 69, 70, 71, 72, 74, 75, 80, 82, 90
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

        if (GearSearchCategories.Contains(searchCategoryId))
            return RetainerRole.GearAndGlamour;

        if (MiscSearchCategories.Contains(searchCategoryId))
            return RetainerRole.Miscellaneous;

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

    public string GetItemName(uint itemId)
    {
        if (dataManager != null)
        {
            try
            {
                var sheet = dataManager.GetExcelSheet<Item>();
                if (sheet != null && sheet.TryGetRow(itemId, out var row))
                {
                    return row.Name.ToString();
                }
            }
            catch
            {
            }
        }
        return $"Item #{itemId}";
    }

    public uint GetItemStackSize(uint itemId)
    {
        if (dataManager != null)
        {
            try
            {
                var sheet = dataManager.GetExcelSheet<Item>();
                if (sheet != null && sheet.TryGetRow(itemId, out var row))
                {
                    return row.StackSize;
                }
            }
            catch
            {
            }
        }
        return 999;
    }
}
