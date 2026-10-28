using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using OnionSquire.Configuration;
using OnionSquire.Models;
using OnionSquire.Services;

namespace OnionSquire.Windows;

public sealed class MainWindow : Window
{
    private readonly Plugin plugin;
    private readonly PluginConfiguration configuration;
    private readonly ArmoireResolver armoireResolver;
    private readonly ItemCategorizer itemCategorizer;

    private static readonly string[] RoleNames =
    {
        "None / Manual",
        "Crafting Materials",
        "Gathering",
        "Food & Potions",
        "Gear & Glamour",
        "Miscellaneous"
    };

    public MainWindow(Plugin plugin) : base("OnionSquire###OnionSquireMainWindow")
    {
        this.plugin = plugin;
        this.configuration = plugin.Configuration;
        this.armoireResolver = plugin.ArmoireResolver;
        this.itemCategorizer = plugin.ItemCategorizer;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 420),
            MaximumSize = new Vector2(900, 800),
        };
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("OnionSquire — Smart Retainer & Armoire Steward");
        ImGui.SameLine();

        if (plugin.AutoRetainerIpc.IsAvailable)
        {
            ImGui.TextColored(new Vector4(0.3f, 1f, 0.4f, 1f), "[AutoRetainer Connected]");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1f), "[AutoRetainer Standby]");
        }

        ImGui.SameLine();
        if (plugin.ArrService.IsArrInstalled)
        {
            ImGui.TextColored(new Vector4(0.3f, 0.9f, 1f, 1f), "[ARR Linked]");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "[ARR Not Detected]");
        }

        ImGui.SameLine();
        int retainerCount = plugin.RetainerService.RetainerCount;
        if (retainerCount > 0)
        {
            ImGui.TextColored(new Vector4(1f, 0.84f, 0.0f, 1f), $"[{retainerCount} Retainers Active]");
        }

        ImGui.Separator();

        if (ImGui.BeginTabBar("OnionSquireTabs"))
        {
            if (ImGui.BeginTabItem("Retainer Roles"))
            {
                DrawRetainerRolesTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Armoire Drawer"))
            {
                DrawArmoireTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Settings"))
            {
                DrawSettingsTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawRetainerRolesTab()
    {
        ImGui.Spacing();
        ImGui.TextWrapped("Assign roles to retainers so your inventory can automatically deposit matching items when you interact with the Summoning Bell.");
        ImGui.Spacing();

        var retainers = plugin.RetainerService.Retainers;

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), $"Configured Retainers ({retainers.Count} detected):");
        ImGui.SameLine();

        if (ImGui.SmallButton("🔄 Refresh Retainers"))
        {
            plugin.RetainerService.RefreshRetainers();
        }

        if (retainers.Count > 0 && !string.IsNullOrEmpty(retainers[0].Source))
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"({retainers[0].Source})");
        }

        ImGui.Separator();
        ImGui.Spacing();

        if (retainers.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1f, 0.75f, 0.3f, 1f), "No retainers detected yet.");
            ImGui.Spacing();
            ImGui.TextWrapped("Interact with any Summoning Bell in-game, or make sure AutoRetainer or A Realm Reorganized is enabled. OnionSquire will automatically detect and populate your retainers.");
            ImGui.Spacing();
            if (ImGui.Button("🔍 Scan for Retainers", new Vector2(180, 28)))
            {
                plugin.RetainerService.RefreshRetainers();
            }
            return;
        }

        for (int i = 0; i < retainers.Count; i++)
        {
            var ret = retainers[i];

            if (!configuration.RetainerRoles.TryGetValue(ret.Name, out var currentRole))
            {
                currentRole = RetainerRole.None;
            }

            ImGui.AlignTextToFramePadding();

            // Retainer number and Name
            string nameDisplay = $"#{i + 1}  {ret.Name}";
            ImGui.TextUnformatted(nameDisplay);

            // Job & Level pill
            if (ret.ClassJob > 0)
            {
                ImGui.SameLine(180);
                string jobAbbrev = plugin.RetainerService.GetJobAbbreviation(ret.ClassJob);
                ImGui.TextColored(new Vector4(0.7f, 0.85f, 1f, 1f), $"[{jobAbbrev} {ret.Level}]");
            }

            // Role selector
            ImGui.SameLine(260);
            int roleInt = (int)currentRole;

            ImGui.SetNextItemWidth(210);
            if (ImGui.Combo($"##role_{ret.Name}_{i}", ref roleInt, RoleNames, RoleNames.Length))
            {
                configuration.RetainerRoles[ret.Name] = (RetainerRole)roleInt;
                configuration.Save();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("📥 Sort Bags into Retainers", new Vector2(230, 32)))
        {
            // Plan and execution queue
        }
    }

    private void DrawArmoireTab()
    {
        ImGui.Spacing();
        ImGui.TextWrapped("Scan your retainers for items that can be stored in the Inn Armoire (seasonal event gear, job artifact armor, achievements). Armoire items do not consume inventory or glamour dresser space.");
        ImGui.Spacing();

        ImGui.TextUnformatted($"Known Armoire catalog entries: {armoireResolver.TotalEligibleArmoireItems}");

        var cachedArmoire = plugin.ArrService.LoadCachedArmoireIds();
        if (cachedArmoire.Count > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), $"({cachedArmoire.Count} already stored in Armoire)");
        }

        ImGui.Spacing();

        if (ImGui.Button("🧳 Scan & Draw Armoire Items", new Vector2(240, 32)))
        {
            // Scan retainer caches and enqueue withdrawals
        }

        ImGui.Spacing();
        ImGui.Separator();

        var retainers = plugin.ArrService.LoadCachedRetainers();
        if (retainers.Count > 0)
        {
            int armoireFound = 0;
            foreach (var kvp in retainers)
            {
                foreach (var itemId in kvp.Value.ItemIds)
                {
                    if (armoireResolver.IsArmoireEligible(itemId) && !cachedArmoire.Contains(itemId))
                    {
                        armoireFound++;
                    }
                }
            }

            if (armoireFound > 0)
            {
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), $"Found {armoireFound} armoire-eligible items across {retainers.Count} retainers ready to draw!");
            }
            else
            {
                ImGui.TextDisabled($"No un-stored armoire items found across {retainers.Count} cached retainers.");
            }
        }
        else
        {
            ImGui.TextDisabled("No armoire items currently queued for withdrawal. (Visit Summoning Bell with ARR enabled to populate cache)");
        }
    }

    private void DrawSettingsTab()
    {
        ImGui.Spacing();

        bool autoCheck = configuration.AutoCheckArmoireOnBellOpen;
        if (ImGui.Checkbox("Automatically check for Armoire items when summoning bell opens", ref autoCheck))
        {
            configuration.AutoCheckArmoireOnBellOpen = autoCheck;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "Retainer Discovery Status:");
        var retainers = plugin.RetainerService.Retainers;
        ImGui.TextUnformatted($"Total Retainers: {retainers.Count}");
        if (retainers.Count > 0)
        {
            ImGui.TextDisabled($"Discovery Source: {retainers[0].Source}");
            for (int i = 0; i < retainers.Count; i++)
            {
                var r = retainers[i];
                string job = plugin.RetainerService.GetJobAbbreviation(r.ClassJob);
                ImGui.BulletText($"{r.Name} (ID: {r.RetainerId}) - {job} Lv {r.Level}");
            }
        }
    }
}
