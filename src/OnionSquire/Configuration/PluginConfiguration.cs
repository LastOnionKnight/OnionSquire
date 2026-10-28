using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using OnionSquire.Models;

namespace OnionSquire.Configuration;

[Serializable]
public sealed class RetainerConfigEntry
{
    public ulong RetainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Index { get; set; }
    public byte Level { get; set; }
    public byte ClassJob { get; set; }
    public RetainerRole Role { get; set; } = RetainerRole.None;
}

[Serializable]
public sealed class PluginConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    /// <summary>
    /// Map of Retainer Name to their assigned role.
    /// </summary>
    public Dictionary<string, RetainerRole> RetainerRoles { get; set; } = new();

    /// <summary>
    /// Persisted list of known retainers discovered from game memory or caches.
    /// </summary>
    public List<RetainerConfigEntry> KnownRetainers { get; set; } = new();

    /// <summary>
    /// When true, armoire search will check across all known retainer inventories.
    /// </summary>
    public bool AutoCheckArmoireOnBellOpen { get; set; } = true;

    /// <summary>
    /// When true, automatically withdraws Armoire-eligible items into player inventory when a retainer is opened.
    /// </summary>
    public bool AutoDrawArmoireOnRetainerOpen { get; set; } = true;

    public void Save()
    {
        try
        {
            Plugin.PluginInterface?.SavePluginConfig(this);
        }
        catch
        {
        }
    }
}
