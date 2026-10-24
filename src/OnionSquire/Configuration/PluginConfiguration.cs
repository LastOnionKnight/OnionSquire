using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace OnionSquire.Configuration;

[Serializable]
public sealed class PluginConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>
    /// Map of Retainer ContentId/Name to their assigned role.
    /// </summary>
    public Dictionary<string, Models.RetainerRole> RetainerRoles { get; set; } = new();

    /// <summary>
    /// When true, armoire search will check across all known retainer inventories.
    /// </summary>
    public bool AutoCheckArmoireOnBellOpen { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
