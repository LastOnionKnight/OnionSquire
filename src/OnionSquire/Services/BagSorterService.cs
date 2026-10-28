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

public sealed class ArmoireFoundItem
{
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string RetainerName { get; set; } = string.Empty;
    public ulong RetainerId { get; set; }
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
    private readonly ArmoireResolver armoireResolver;
    private readonly RetainerService retainerService;
    private readonly AutoRetainerIpc autoRetainerIpc;
    private readonly IFramework? framework;
    private readonly IChatGui? chatGui;
    private readonly IPluginLog? log;

    private readonly Queue<(InventoryType srcContainer, ushort srcSlot, InventoryType dstContainer, ushort dstSlot, uint itemId, string itemName, bool isArmoireWithdrawal)> moveQueue = new();
    private string activeProcessingRetainer = string.Empty;
    private string lastObservedRetainer = string.Empty;
    private int throttleTicks = 0;
    private int sessionMovedCount = 0;

    public bool IsSorting => moveQueue.Count > 0;
    public string StatusMessage { get; private set; } = "Ready";
    public int LastMovedCount { get; private set; } = 0;

    public BagSorterService(
        PluginConfiguration configuration,
        ItemCategorizer itemCategorizer,
        ArmoireResolver armoireResolver,
        RetainerService retainerService,
        AutoRetainerIpc autoRetainerIpc,
        IFramework? framework = null,
        IChatGui? chatGui = null,
        IPluginLog? log = null)
    {
        this.configuration = configuration;
        this.itemCategorizer = itemCategorizer;
        this.armoireResolver = armoireResolver;
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
    /// Gets the total number of empty slots in the player's inventory bags.
    /// </summary>
    public unsafe int GetFreePlayerInventorySlots()
    {
        try
        {
            var im = InventoryManager.Instance();
            if (im != null)
            {
                return (int)im->GetEmptySlotsInBag();
            }
        }
        catch
        {
        }
        return 0;
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
    /// Finds all Armoire-eligible items currently in the active retainer's inventory pages.
    /// </summary>
    public unsafe List<(InventoryType page, ushort slot, uint itemId, string itemName)> GetActiveRetainerArmoireItems()
    {
        var list = new List<(InventoryType page, ushort slot, uint itemId, string itemName)>();
        try
        {
            var im = InventoryManager.Instance();
            if (im == null) return list;

            foreach (var page in RetainerPages)
            {
                var container = im->GetInventoryContainer(page);
                if (container == null || !container->IsLoaded) continue;

                for (int s = 0; s < container->Size; s++)
                {
                    var slot = container->GetInventorySlot(s);
                    if (slot != null && slot->ItemId > 0 && armoireResolver.IsArmoireEligible(slot->ItemId))
                    {
                        list.Add((page, (ushort)s, slot->ItemId, itemCategorizer.GetItemName(slot->ItemId)));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log?.Debug(ex, "Failed to scan active retainer for Armoire items.");
        }

        return list;
    }

    /// <summary>
    /// Withdraws Armoire-eligible items from the active retainer into player inventory bags.
    /// </summary>
    public unsafe int DrawArmoireFromActiveRetainer()
    {
        if (moveQueue.Count > 0) return 0;

        string activeRetainer = GetActiveRetainerName();
        if (string.IsNullOrEmpty(activeRetainer)) return 0;

        var armoireItems = GetActiveRetainerArmoireItems();
        if (armoireItems.Count == 0) return 0;

        var im = InventoryManager.Instance();
        if (im == null) return 0;

        var claimedSlots = new HashSet<(InventoryType bag, ushort slot)>();
        int queued = 0;

        foreach (var (srcPage, srcSlot, itemId, itemName) in armoireItems)
        {
            if (TryFindEmptyPlayerSlot(im, claimedSlots, out var dstBag, out var dstSlot))
            {
                claimedSlots.Add((dstBag, dstSlot));
                moveQueue.Enqueue((srcPage, srcSlot, dstBag, dstSlot, itemId, itemName, true));
                queued++;
            }
            else
            {
                chatGui?.Print($"[OnionSquire] Player inventory is full! Could only withdraw {queued} Armoire items.");
                break;
            }
        }

        if (queued > 0)
        {
            activeProcessingRetainer = activeRetainer;
            sessionMovedCount = 0;
            StatusMessage = $"Pulling {queued} Armoire items from {activeRetainer}...";
            chatGui?.Print($"[OnionSquire] Pulling {queued} Armoire items from {activeRetainer} into player inventory...");
        }

        return queued;
    }

    /// <summary>
    /// Executes sorting bags into retainers. If a retainer is open, deposits matching items.
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
    /// Enqueues item transfers from player bags into the specified retainer while its inventory is loaded.
    /// </summary>
    public unsafe bool SortIntoRetainer(string retainerName)
    {
        if (moveQueue.Count > 0)
        {
            StatusMessage = "Operation already in progress...";
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

        activeProcessingRetainer = retainerName;
        sessionMovedCount = 0;
        int planned = 0;

        foreach (var (srcBag, srcSlot, itemId, name) in itemsToMove)
        {
            if (TryFindDestinationRetainerSlot(im, itemId, out var dstPage, out var dstSlot))
            {
                moveQueue.Enqueue((srcBag, srcSlot, dstPage, dstSlot, itemId, name, false));
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

    private unsafe bool TryFindDestinationRetainerSlot(InventoryManager* im, uint itemId, out InventoryType page, out ushort slot)
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

    private unsafe bool TryFindEmptyPlayerSlot(
        InventoryManager* im,
        HashSet<(InventoryType bag, ushort slot)> claimedSlots,
        out InventoryType bag,
        out ushort slot)
    {
        bag = InventoryType.Inventory1;
        slot = 0;
        foreach (var b in PlayerBags)
        {
            var c = im->GetInventoryContainer(b);
            if (c == null || !c->IsLoaded) continue;

            for (int s = 0; s < c->Size; s++)
            {
                if (claimedSlots.Contains((b, (ushort)s))) continue;
                var item = c->GetInventorySlot(s);
                if (item == null || item->ItemId == 0)
                {
                    bag = b;
                    slot = (ushort)s;
                    return true;
                }
            }
        }
        return false;
    }

    private unsafe void OnFrameworkUpdate(IFramework fw)
    {
        string currentActive = GetActiveRetainerName();

        // Detect newly opened retainer
        if (!string.IsNullOrEmpty(currentActive) && currentActive != lastObservedRetainer)
        {
            lastObservedRetainer = currentActive;

            // Automatically draw armoire items if enabled
            if (configuration.AutoDrawArmoireOnRetainerOpen && moveQueue.Count == 0)
            {
                DrawArmoireFromActiveRetainer();
            }
        }
        else if (string.IsNullOrEmpty(currentActive))
        {
            lastObservedRetainer = string.Empty;
        }

        if (moveQueue.Count == 0) return;

        // Throttle transfers: 1 item move every 8 frames (~133ms)
        if (++throttleTicks % 8 != 0) return;

        var im = InventoryManager.Instance();
        if (im == null) return;

        // Check if game is currently busy with another operation
        try
        {
            if (im->PendingOperations.Length > 0) return;
        }
        catch
        {
        }

        var (srcContainer, srcSlot, dstContainer, dstSlot, itemId, itemName, isArmoireWithdrawal) = moveQueue.Dequeue();

        try
        {
            var srcC = im->GetInventoryContainer(srcContainer);
            if (srcC != null && srcC->IsLoaded)
            {
                var cur = srcC->GetInventorySlot(srcSlot);
                if (cur != null && cur->ItemId == itemId)
                {
                    im->MoveItemSlot(srcContainer, srcSlot, dstContainer, dstSlot, false);
                    sessionMovedCount++;
                }
            }
        }
        catch (Exception ex)
        {
            log?.Error(ex, $"Failed to move {itemName} from {srcContainer}:{srcSlot} to {dstContainer}:{dstSlot}.");
        }

        if (moveQueue.Count == 0)
        {
            LastMovedCount = sessionMovedCount;
            if (isArmoireWithdrawal)
            {
                StatusMessage = $"Finished pulling {sessionMovedCount} Armoire items from {activeProcessingRetainer} into your inventory!";
            }
            else
            {
                StatusMessage = $"Finished depositing {sessionMovedCount} items into {activeProcessingRetainer}!";
            }

            chatGui?.Print($"[OnionSquire] {StatusMessage}");

            // Notify AutoRetainer if in postprocess step
            autoRetainerIpc.FinishRetainerPostProcess();
            activeProcessingRetainer = string.Empty;
        }
    }
}
