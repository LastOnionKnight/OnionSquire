using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace OnionSquire.Services;

/// <summary>
/// Resolves whether an item is eligible for storage in the Armoire (Lumina Cabinet sheet).
/// </summary>
public class ArmoireResolver
{
    private readonly IDataManager? dataManager;
    private readonly HashSet<uint> armoireItemIds = new();
    private bool isInitialized = false;

    public int TotalEligibleArmoireItems => armoireItemIds.Count;

    public ArmoireResolver(IDataManager? dataManager)
    {
        this.dataManager = dataManager;
        Initialize();
    }

    public void Initialize()
    {
        if (isInitialized || dataManager == null) return;

        try
        {
            var cabinetSheet = dataManager.GetExcelSheet<Cabinet>();
            if (cabinetSheet != null)
            {
                foreach (var row in cabinetSheet)
                {
                    uint itemId = row.Item.RowId;
                    if (itemId > 0)
                    {
                        armoireItemIds.Add(itemId);
                    }
                }
            }
            isInitialized = true;
        }
        catch
        {
            // Fallback for tests or missing game data
        }
    }

    /// <summary>
    /// Returns true if the item can be deposited into the Armoire.
    /// </summary>
    public bool IsArmoireEligible(uint itemId)
    {
        return armoireItemIds.Contains(itemId);
    }

    /// <summary>
    /// Explicitly register an item ID (used in unit tests).
    /// </summary>
    public void RegisterTestItem(uint itemId)
    {
        armoireItemIds.Add(itemId);
    }
}
