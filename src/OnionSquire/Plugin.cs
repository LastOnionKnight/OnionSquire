using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using OnionSquire.Configuration;
using OnionSquire.Services;
using OnionSquire.Windows;

namespace OnionSquire;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/onionsquire";
    private const string ShortCommandName = "/squire";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal PluginConfiguration Configuration { get; }
    internal ArmoireResolver ArmoireResolver { get; }
    internal ItemCategorizer ItemCategorizer { get; }

    internal readonly WindowSystem WindowSystem = new("OnionSquire");
    private readonly MainWindow mainWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as PluginConfiguration ?? new PluginConfiguration();

        ArmoireResolver = new ArmoireResolver(DataManager);
        ItemCategorizer = new ItemCategorizer(DataManager);

        mainWindow = new MainWindow(this);
        WindowSystem.AddWindow(mainWindow);

        var cmdInfo = new CommandInfo(OnCommand)
        {
            HelpMessage = "Open OnionSquire Retainer Organizer & Armoire Drawer."
        };

        CommandManager.AddHandler(CommandName, cmdInfo);
        CommandManager.AddHandler(ShortCommandName, cmdInfo);

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;

        Log.Information("OnionSquire initialized successfully.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(ShortCommandName);
    }

    private void OnCommand(string command, string args) => ToggleMainUi();

    internal void ToggleMainUi() => mainWindow.Toggle();
}
