using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using OnionSquire.Configuration;
using OnionSquire.Models;

namespace OnionSquire.Services;

public sealed class SortPlanItem
{
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public InventoryType SourceBag { get; set; }
    public ushort SourceSlot { get; set; }
    public RetainerRole Role { get; set; }
    public string TargetRetainerName { get; set; } = string.Empty;
}

public sealed class BagSorterService : IDisposable
{
    private static readonly InventoryType[] PlayerBags =
    {
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    };

    private static readonly InventoryType[] RetainerPages =
    {
        InventoryType.RetainerPage1,
        InventoryType.RetainerPage2,
        InventoryType.RetainerPage3,
        InventoryType.RetainerPage4,
        InventoryType.RetainerPage5,
        InventoryType.RetainerPage6,
        InventoryType.RetainerPage7,
    };

    private readonly PluginConfiguration configuration;
    private readonly ItemCategorizer itemCategorizer;
    private readonly RetainerService retainerService;
    private readonly AutoRetainerIpc autoRetainerIpc;
    private readonly IFramework? framework;
    private readonly IChatGui? chatGui;
    private readonly IPluginLog? log;

    private readonly Queue<(InventoryType srcBag, ushort srcSlot, InventoryType dstPage, ushort dstSlot, uint itemId, string itemName)> moveQueue = new();
    private string activeProcessingRetainer = string.Empty;
    private int throttleTicks = 0;
    private int sessionDepositedCount = 0;

    public bool IsSorting => moveQueue.Count > 0;
    public string StatusMessage { get; private set; } = "Ready";
    public int LastDepositedCount { get; private set; } = 0;

    public BagSorterService(
        PluginConfiguration configuration,
        ItemCategorizer itemCategorizer,
        RetainerService retainerService,
        AutoRetainerIpc autoRetainerIpc,
        IFramework? framework = null,
        IChatGui? chatGui = null,
        IPluginLog? log = null)
    {
        this.configuration = configuration;
        this.itemCategorizer = itemCategorizer;
        this.retainerService = retainerService;
        this.autoRetainerIpc = autoRetainerIpc;
        this.framework = framework;
        this.chatGui = chatGui;
        this.log = log;

        if (this.framework != null)
        {
            this.framework.Update += OnFrameworkUpdate;
        }
    }

    public void Dispose()
    {
        if (framework != null)
        {
            framework.Update -= OnFrameworkUpdate;
        }
    }

    /// <summary>
    /// Gets the name of the retainer that is currently active/summoned, or empty if none.
    /// </summary>
    public unsafe string GetActiveRetainerName()
    {
        try
        {
            var rm = RetainerManager.Instance();
            if (rm != null)
            {
                var active = rm->GetActiveRetainer();
                if (active != null && active->Available && !string.IsNullOrWhiteSpace(active->NameString))
                {
                    return active->NameString;
                }
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    /// <summary>
    /// Scans the player's 4 inventory bags and returns all items planned to be deposited
    /// into retainers according to their configured roles.
    /// </summary>
    public unsafe List<SortPlanItem> GetSortPlan()
    {
        var plan = new List<SortPlanItem>();
        try
        {
            var im = InventoryManager.Instance();
            if (im == null) return plan;

            // Map each RetainerRole to the list of retainer names that accept it
            var roleMap = new Dictionary<RetainerRole, List<string>>();
            foreach (var kvp in configuration.RetainerRoles)
            {
                if (kvp.Value == RetainerRole.None) continue;
                if (!roleMap.TryGetValue(kvp.Value, out var list))
                {
                    list = new List<string>();
                    roleMap[kvp.Value] = list;
                }
                list.Add(kvp.Key);
            }

            foreach (var bag in PlayerBags)
            {
                var container = im->GetInventoryContainer(bag);
                if (container == null || !container->IsLoaded) continue;

                for (int slot = 0; slot < container->Size; slot++)
                {
                    var item = container->GetInventorySlot(slot);
                    if (item == null || item->ItemId == 0) continue;

                    var role = itemCategorizer.CategorizeItemId(item->ItemId);
                    if (role == RetainerRole.None) continue;

                    if (roleMap.TryGetValue(role, out var targets) && targets.Count > 0)
                    {
                        plan.Add(new SortPlanItem
                        {
                            ItemId = item->ItemId,
                            ItemName = itemCategorizer.GetItemName(item->ItemId),
                            Quantity = item->Quantity,
                            SourceBag = bag,
                            SourceSlot = (ushort)slot,
                            Role = role,
                            TargetRetainerName = string.Join(", ", targets)
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log?.Debug(ex, "Failed to build sort plan.");
        }

        return plan;
    }

    /// <summary>
    /// Starts the sorting process. If a retainer is currently active, deposits matching items into it.
    /// If no retainer is active, plans the sort and informs the user.
    /// </summary>
    public unsafe bool ExecuteSort()
    {
        string activeRetainer = GetActiveRetainerName();
        if (string.IsNullOrEmpty(activeRetainer))
        {
            var plan = GetSortPlan();
            if (plan.Count == 0)
            {
                StatusMessage = "No items in your bags match any configured retainer roles.";
                chatGui?.Print($"[OnionSquire] {StatusMessage}");
            }
            else
            {
                StatusMessage = $"Found {plan.Count} items matching retainer roles. Open any retainer at the Summoning Bell to deposit!";
                chatGui?.Print($"[OnionSquire] {StatusMessage}");
            }
            return false;
        }

        return SortIntoRetainer(activeRetainer);
    }

    /// <summary>
    /// Enqueues item transfers for the specified retainer while its inventory is loaded.
    /// </summary>
    public unsafe bool SortIntoRetainer(string retainerName)
    {
        if (moveQueue.Count > 0)
        {
            StatusMessage = "Deposit already in progress...";
            return false;
        }

        if (!configuration.RetainerRoles.TryGetValue(retainerName, out var assignedRole))
        {
            assignedRole = RetainerRole.None;
        }

        var im = InventoryManager.Instance();
        if (im == null) return false;

        var itemsToMove = new List<(InventoryType bag, ushort slot, uint itemId, string name)>();

        // Find all items in player bags matching this retainer's role
        foreach (var bag in PlayerBags)
        {
            var container = im->GetInventoryContainer(bag);
            if (container == null || !container->IsLoaded) continue;

            for (int s = 0; s < container->Size; s++)
            {
                var item = container->GetInventorySlot(s);
                if (item == null || item->ItemId == 0) continue;

                var role = itemCategorizer.CategorizeItemId(item->ItemId);
                if (role == assignedRole && assignedRole != RetainerRole.None)
                {
                    itemsToMove.Add((bag, (ushort)s, item->ItemId, itemCategorizer.GetItemName(item->ItemId)));
                }
            }
        }

        if (itemsToMove.Count == 0)
        {
            StatusMessage = $"No items in bags match {retainerName}'s assigned role ({assignedRole}).";
            chatGui?.Print($"[OnionSquire] {StatusMessage}");
            return false;
        }

        // Find available destination slots in the retainer's pages
        activeProcessingRetainer = retainerName;
        sessionDepositedCount = 0;
        int planned = 0;

        foreach (var (srcBag, srcSlot, itemId, name) in itemsToMove)
        {
            if (TryFindDestinationSlot(im, itemId, out var dstPage, out var dstSlot))
            {
                moveQueue.Enqueue((srcBag, srcSlot, dstPage, dstSlot, itemId, name));
                planned++;
            }
        }

        if (planned > 0)
        {
            StatusMessage = $"Depositing {planned} items into {retainerName}...";
            chatGui?.Print($"[OnionSquire] Starting deposit of {planned} items into {retainerName} ({assignedRole})...");
            return true;
        }
        else
        {
            StatusMessage = $"{retainerName}'s inventory is completely full!";
            chatGui?.Print($"[OnionSquire] {retainerName}'s inventory is full!");
            return false;
        }
    }

    private unsafe bool TryFindDestinationSlot(InventoryManager* im, uint itemId, out InventoryType page, out ushort slot)
    {
        page = InventoryType.RetainerPage1;
        slot = 0;

        uint maxStack = itemCategorizer.GetItemStackSize(itemId);

        // 1. Look for existing stack
        if (maxStack > 1)
        {
            foreach (var p in RetainerPages)
            {
                var c = im->GetInventoryContainer(p);
                if (c == null || !c->IsLoaded) continue;

                for (int s = 0; s < c->Size; s++)
                {
                    var item = c->GetInventorySlot(s);
                    if (item != null && item->ItemId == itemId && item->Quantity < maxStack)
                    {
                        page = p;
                        slot = (ushort)s;
                        return true;
                    }
                }
            }
        }

        // 2. Look for empty slot
        foreach (var p in RetainerPages)
        {
            var c = im->GetInventoryContainer(p);
            if (c == null || !c->IsLoaded) continue;

            for (int s = 0; s < c->Size; s++)
            {
                var item = c->GetInventorySlot(s);
                if (item == null || item->ItemId == 0)
                {
                    page = p;
                    slot = (ushort)s;
                    return true;
                }
            }
        }

        return false;
    }

    private unsafe void OnFrameworkUpdate(IFramework fw)
    {
        if (moveQueue.Count == 0) return;

        // Throttle transfers: execute 1 item move every 8 frames (~133ms at 60fps)
        if (++throttleTicks % 8 != 0) return;

        var im = InventoryManager.Instance();
        if (im == null) return;

        // Check if an operation is currently pending
        try
        {
            if (im->PendingOperations.Length > 0) return;
        }
        catch
        {
        }

        var (srcBag, srcSlot, dstPage, dstSlot, itemId, itemName) = moveQueue.Dequeue();

        try
        {
            // Verify item is still in source slot
            var srcContainer = im->GetInventoryContainer(srcBag);
            if (srcContainer != null && srcContainer->IsLoaded)
            {
                var currentItem = srcContainer->GetInventorySlot(srcSlot);
                if (currentItem != null && currentItem->ItemId == itemId)
                {
                    im->MoveItemSlot(srcBag, srcSlot, dstPage, dstSlot, false);
                    sessionDepositedCount++;
                }
            }
        }
        catch (Exception ex)
        {
            log?.Error(ex, $"Failed to move {itemName} from {srcBag}:{srcSlot} to {dstPage}:{dstSlot}.");
        }

        if (moveQueue.Count == 0)
        {
            LastDepositedCount = sessionDepositedCount;
            StatusMessage = $"Finished depositing {sessionDepositedCount} items into {activeProcessingRetainer}!";
            chatGui?.Print($"[OnionSquire] {StatusMessage}");

            // Notify AutoRetainer if in postprocess step
            autoRetainerIpc.FinishRetainerPostProcess();
            activeProcessingRetainer = string.Empty;
        }
    }
}
