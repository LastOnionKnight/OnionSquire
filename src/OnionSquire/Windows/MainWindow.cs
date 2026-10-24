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

    public MainWindow(Plugin plugin) : base("OnionSquire###OnionSquireMainWindow")
    {
        this.plugin = plugin;
        this.configuration = plugin.Configuration;
        this.armoireResolver = plugin.ArmoireResolver;
        this.itemCategorizer = plugin.ItemCategorizer;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(480, 360),
            MaximumSize = new Vector2(800, 700),
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

        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "Configured Retainers:");
        ImGui.Separator();

        // Sample slots / configured retainers
        string[] sampleRetainers = { "Retainer 1", "Retainer 2", "Retainer 3", "Retainer 4" };

        foreach (var ret in sampleRetainers)
        {
            if (!configuration.RetainerRoles.TryGetValue(ret, out var currentRole))
            {
                currentRole = RetainerRole.None;
            }

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{ret}:");
            ImGui.SameLine(130);

            int roleInt = (int)currentRole;
            string[] roleNames = { "None / Manual", "Crafting Materials", "Gathering", "Food & Potions", "Gear & Glamour", "Miscellaneous" };

            ImGui.SetNextItemWidth(200);
            if (ImGui.Combo($"##role_{ret}", ref roleInt, roleNames, roleNames.Length))
            {
                configuration.RetainerRoles[ret] = (RetainerRole)roleInt;
                configuration.Save();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("📥 Sort Bags into Retainers", new Vector2(220, 30)))
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
        ImGui.Spacing();

        if (ImGui.Button("🧳 Scan & Draw Armoire Items", new Vector2(240, 32)))
        {
            // Scan retainer caches and enqueue withdrawals
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("No armoire items currently queued for withdrawal.");
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
    }
}
