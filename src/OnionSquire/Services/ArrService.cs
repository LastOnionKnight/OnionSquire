using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace OnionSquire.Services;

/// <summary>
/// Service to read and synchronize cached inventory data from A Realm Reorganized (ARR).
/// ARR maintains an offline cache of the Glamour Dresser, Armoire, and Retainers in its config file.
/// </summary>
public class ArrService
{
    private readonly IPluginLog? log;
    private readonly string arrConfigPath;

    public bool IsArrInstalled => File.Exists(arrConfigPath);

    public ArrService(IDalamudPluginInterface pluginInterface, IPluginLog? log = null)
    {
        this.log = log;
        string configDir = pluginInterface.GetPluginConfigDirectory();
        // ARR stores its configuration at %APPDATA%\XIVLauncher\pluginConfigs\ARealmReorganized.json
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        arrConfigPath = Path.Combine(appData, "XIVLauncher", "pluginConfigs", "ARealmReorganized.json");
    }

    /// <summary>
    /// Reads ARR's cached retainer inventories.
    /// Maps RetainerId -> (RetainerName, List of ItemIds).
    /// </summary>
    public Dictionary<ulong, (string Name, List<uint> ItemIds)> LoadCachedRetainers()
    {
        var result = new Dictionary<ulong, (string Name, List<uint> ItemIds)>();
        if (!IsArrInstalled) return result;

        try
        {
            string json = File.ReadAllText(arrConfigPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("CachedRetainers", out var retainersElem))
            {
                foreach (var prop in retainersElem.EnumerateObject())
                {
                    if (ulong.TryParse(prop.Name, out ulong retainerId))
                    {
                        string name = string.Empty;
                        var items = new List<uint>();

                        if (prop.Value.TryGetProperty("Name", out var nameProp))
                        {
                            name = nameProp.GetString() ?? string.Empty;
                        }

                        if (prop.Value.TryGetProperty("Entries", out var entriesProp) && entriesProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var entry in entriesProp.EnumerateArray())
                            {
                                if (entry.TryGetProperty("ItemId", out var itemProp))
                                {
                                    items.Add(itemProp.GetUInt32());
                                }
                            }
                        }

                        result[retainerId] = (name, items);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log?.Error(ex, "Failed to parse A Realm Reorganized cache.");
        }

        return result;
    }

    /// <summary>
    /// Reads item IDs already stored in the Armoire according to ARR.
    /// </summary>
    public HashSet<uint> LoadCachedArmoireIds()
    {
        var result = new HashSet<uint>();
        if (!IsArrInstalled) return result;

        try
        {
            string json = File.ReadAllText(arrConfigPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("CachedCabinet", out var cabinetElem) &&
                cabinetElem.TryGetProperty("StoredIds", out var idsElem) &&
                idsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var id in idsElem.EnumerateArray())
                {
                    result.Add(id.GetUInt32());
                }
            }
        }
        catch (Exception ex)
        {
            log?.Error(ex, "Failed to load cached cabinet IDs from ARR.");
        }

        return result;
    }

    /// <summary>
    /// Reads item IDs stored in the Glamour Dresser according to ARR.
    /// </summary>
    public HashSet<uint> LoadCachedDresserIds()
    {
        var result = new HashSet<uint>();
        if (!IsArrInstalled) return result;

        try
        {
            string json = File.ReadAllText(arrConfigPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("CachedDresser", out var dresserElem) &&
                dresserElem.TryGetProperty("Slots", out var slotsElem) &&
                slotsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var slot in slotsElem.EnumerateArray())
                {
                    if (slot.TryGetProperty("ItemId", out var idProp))
                    {
                        result.Add(idProp.GetUInt32());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log?.Error(ex, "Failed to load cached dresser IDs from ARR.");
        }

        return result;
    }
}
