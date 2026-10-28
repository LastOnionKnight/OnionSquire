using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using OnionSquire.Configuration;
using OnionSquire.Models;

namespace OnionSquire.Services;

public class RetainerService
{
    private readonly IDataManager? dataManager;
    private readonly IPlayerState? playerState;
    private readonly PluginConfiguration configuration;
    private readonly IPluginLog? log;

    private readonly List<RetainerInfo> retainers = new();
    private readonly object lockObj = new();

    public IReadOnlyList<RetainerInfo> Retainers
    {
        get
        {
            lock (lockObj)
            {
                return retainers.ToList();
            }
        }
    }

    public int RetainerCount
    {
        get
        {
            lock (lockObj)
            {
                return retainers.Count;
            }
        }
    }

    public RetainerService(
        IDataManager? dataManager,
        IPlayerState? playerState,
        PluginConfiguration configuration,
        IPluginLog? log = null)
    {
        this.dataManager = dataManager;
        this.playerState = playerState;
        this.configuration = configuration;
        this.log = log;

        // Populate from saved configuration first
        LoadFromConfiguration();

        // Then attempt immediate refresh
        RefreshRetainers();
    }

    /// <summary>
    /// Scans live memory, AutoRetainer, and ARR to read all retainers.
    /// </summary>
    public void RefreshRetainers()
    {
        // 1. Try reading live game memory (RetainerManager)
        if (TryReadFromGameMemory(out var memRetainers))
        {
            ApplyRetainers(memRetainers);
            return;
        }

        // 2. Try reading from AutoRetainer config
        if (TryReadFromAutoRetainer(out var arRetainers))
        {
            ApplyRetainers(arRetainers);
            return;
        }

        // 3. Try reading from ARR config
        if (TryReadFromArr(out var arrRetainers))
        {
            ApplyRetainers(arrRetainers);
            return;
        }

        // 4. Fallback to saved config
        LoadFromConfiguration();
    }

    private unsafe bool TryReadFromGameMemory(out List<RetainerInfo> results)
    {
        results = new List<RetainerInfo>();
        try
        {
            var manager = RetainerManager.Instance();
            if (manager == null) return false;

            byte count = manager->GetRetainerCount();
            if (count == 0) return false;

            for (uint i = 0; i < count; i++)
            {
                var ret = manager->GetRetainerBySortedIndex(i);
                if (ret == null) continue;

                string name = ret->NameString;
                if (string.IsNullOrWhiteSpace(name)) continue;

                results.Add(new RetainerInfo
                {
                    RetainerId = ret->RetainerId,
                    Name = name,
                    Index = (int)i,
                    Level = ret->Level,
                    ClassJob = ret->ClassJob,
                    ItemCount = ret->ItemCount,
                    MarketItemCount = ret->MarketItemCount,
                    Available = ret->Available,
                    Source = "Live Game Memory"
                });
            }

            return results.Count > 0;
        }
        catch (Exception ex)
        {
            log?.Debug(ex, "Could not read RetainerManager from game memory.");
            return false;
        }
    }

    public bool TryReadFromAutoRetainer(out List<RetainerInfo> results)
    {
        results = new List<RetainerInfo>();
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string arPath = Path.Combine(appData, "XIVLauncher", "pluginConfigs", "AutoRetainer", "DefaultConfig.json");
            if (!File.Exists(arPath)) return false;

            string json = File.ReadAllText(arPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("OfflineData", out var offlineData) || offlineData.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            ulong localCid = playerState?.ContentId ?? 0;
            string localName = playerState?.CharacterName ?? string.Empty;

            JsonElement? targetChar = null;
            foreach (var charElem in offlineData.EnumerateArray())
            {
                if (localCid != 0 && charElem.TryGetProperty("CID", out var cidProp) && cidProp.GetUInt64() == localCid)
                {
                    targetChar = charElem;
                    break;
                }

                if (!string.IsNullOrEmpty(localName) && charElem.TryGetProperty("Name", out var nameProp) && nameProp.GetString() == localName)
                {
                    targetChar = charElem;
                    break;
                }
            }

            // If neither CID nor character name matched (or offline), default to first character entry
            if (targetChar == null && offlineData.GetArrayLength() > 0)
            {
                targetChar = offlineData[0];
            }

            if (targetChar.HasValue && targetChar.Value.TryGetProperty("RetainerData", out var retainerData) && retainerData.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var ret in retainerData.EnumerateArray())
                {
                    string name = ret.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    ulong id = ret.TryGetProperty("RetainerID", out var idProp) ? idProp.GetUInt64() : 0;
                    byte lvl = ret.TryGetProperty("Level", out var lvlProp) ? lvlProp.GetByte() : (byte)0;
                    byte job = ret.TryGetProperty("Job", out var jobProp) ? jobProp.GetByte() : (byte)0;
                    byte mb = ret.TryGetProperty("MBItems", out var mbProp) ? mbProp.GetByte() : (byte)0;

                    results.Add(new RetainerInfo
                    {
                        RetainerId = id,
                        Name = name,
                        Index = index++,
                        Level = lvl,
                        ClassJob = job,
                        MarketItemCount = mb,
                        Available = true,
                        Source = "AutoRetainer Cache"
                    });
                }
            }

            return results.Count > 0;
        }
        catch (Exception ex)
        {
            log?.Debug(ex, "Failed to read retainers from AutoRetainer configuration.");
            return false;
        }
    }

    public bool TryReadFromArr(out List<RetainerInfo> results)
    {
        results = new List<RetainerInfo>();
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string arrPath = Path.Combine(appData, "XIVLauncher", "pluginConfigs", "ARealmReorganized.json");
            if (!File.Exists(arrPath)) return false;

            string json = File.ReadAllText(arrPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("CachedRetainers", out var retainersElem) || retainersElem.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            int index = 0;
            foreach (var prop in retainersElem.EnumerateObject())
            {
                if (prop.Name.StartsWith("$") || prop.Value.ValueKind != JsonValueKind.Object) continue;

                ulong.TryParse(prop.Name, out ulong retainerId);
                string name = prop.Value.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                byte itemCount = 0;
                if (prop.Value.TryGetProperty("Entries", out var entriesProp) && entriesProp.ValueKind == JsonValueKind.Array)
                {
                    itemCount = (byte)Math.Min(entriesProp.GetArrayLength(), 255);
                }

                results.Add(new RetainerInfo
                {
                    RetainerId = retainerId,
                    Name = name,
                    Index = index++,
                    ItemCount = itemCount,
                    Available = true,
                    Source = "ARR Cache"
                });
            }

            return results.Count > 0;
        }
        catch (Exception ex)
        {
            log?.Debug(ex, "Failed to read retainers from ARR configuration.");
            return false;
        }
    }

    private void ApplyRetainers(List<RetainerInfo> newRetainers)
    {
        lock (lockObj)
        {
            retainers.Clear();
            retainers.AddRange(newRetainers);

            // Persist to configuration
            configuration.KnownRetainers.Clear();
            for (int i = 0; i < retainers.Count; i++)
            {
                var r = retainers[i];
                var role = RetainerRole.None;

                if (configuration.RetainerRoles.TryGetValue(r.Name, out var existingRole))
                {
                    role = existingRole;
                }
                else if (i == 0 && configuration.RetainerRoles.TryGetValue("Retainer 1", out var legacyRole1))
                {
                    // Migrate legacy role
                    role = legacyRole1;
                    configuration.RetainerRoles[r.Name] = legacyRole1;
                }
                else if (i == 1 && configuration.RetainerRoles.TryGetValue("Retainer 2", out var legacyRole2))
                {
                    role = legacyRole2;
                    configuration.RetainerRoles[r.Name] = legacyRole2;
                }

                configuration.KnownRetainers.Add(new RetainerConfigEntry
                {
                    RetainerId = r.RetainerId,
                    Name = r.Name,
                    Index = r.Index,
                    Level = r.Level,
                    ClassJob = r.ClassJob,
                    Role = role
                });
            }

            configuration.Save();
        }

        log?.Information($"OnionSquire loaded {newRetainers.Count} retainers ({newRetainers.FirstOrDefault()?.Source}).");
    }

    private void LoadFromConfiguration()
    {
        lock (lockObj)
        {
            if (configuration.KnownRetainers.Count > 0)
            {
                retainers.Clear();
                foreach (var kr in configuration.KnownRetainers)
                {
                    retainers.Add(new RetainerInfo
                    {
                        RetainerId = kr.RetainerId,
                        Name = kr.Name,
                        Index = kr.Index,
                        Level = kr.Level,
                        ClassJob = kr.ClassJob,
                        Available = true,
                        Source = "Saved Configuration"
                    });
                }
            }
        }
    }

    /// <summary>
    /// Gets the 3-letter abbreviation of a class/job (e.g. PLD, MIN, BTN, FSH).
    /// </summary>
    public string GetJobAbbreviation(byte classJobId)
    {
        if (dataManager != null)
        {
            try
            {
                var sheet = dataManager.GetExcelSheet<ClassJob>();
                if (sheet != null && sheet.TryGetRow(classJobId, out var row))
                {
                    string abbrev = row.Abbreviation.ToString();
                    if (!string.IsNullOrEmpty(abbrev)) return abbrev;
                }
            }
            catch
            {
            }
        }

        return classJobId switch
        {
            1 => "GLA", 2 => "PGL", 3 => "MRD", 4 => "LNC", 5 => "ARC", 6 => "CNJ", 7 => "THM",
            8 => "CRP", 9 => "BSM", 10 => "ARM", 11 => "GSM", 12 => "LTW", 13 => "WVR", 14 => "ALC", 15 => "CUL",
            16 => "MIN", 17 => "BTN", 18 => "FSH",
            19 => "PLD", 20 => "MNK", 21 => "WAR", 22 => "DRG", 23 => "BRD", 24 => "WHM", 25 => "MCH",
            26 => "BLM", 27 => "SMN", 28 => "SCH", 30 => "NIN", 32 => "DRK", 33 => "AST", 34 => "SAM",
            35 => "RDM", 37 => "GNB", 38 => "DNC", 39 => "RPR", 40 => "SGE", 41 => "VPR", 42 => "PCT",
            _ => classJobId > 0 ? $"Job {classJobId}" : "None"
        };
    }
}
