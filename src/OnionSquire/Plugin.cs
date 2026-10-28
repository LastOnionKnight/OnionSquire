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
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal PluginConfiguration Configuration { get; }
    internal ArmoireResolver ArmoireResolver { get; }
    internal ItemCategorizer ItemCategorizer { get; }
    internal AutoRetainerIpc AutoRetainerIpc { get; }
    internal ArrService ArrService { get; }
    internal RetainerService RetainerService { get; }
    internal BagSorterService BagSorterService { get; }

    internal readonly WindowSystem WindowSystem = new("OnionSquire");
    private readonly MainWindow mainWindow;
    private int frameworkTicks = 0;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as PluginConfiguration ?? new PluginConfiguration();

        ArmoireResolver = new ArmoireResolver(DataManager);
        ItemCategorizer = new ItemCategorizer(DataManager);
        AutoRetainerIpc = new AutoRetainerIpc(PluginInterface);
        ArrService = new ArrService(PluginInterface, Log);
        RetainerService = new RetainerService(DataManager, PlayerState, Configuration, Log);
        BagSorterService = new BagSorterService(Configuration, ItemCategorizer, ArmoireResolver, RetainerService, AutoRetainerIpc, Framework, ChatGui, Log);

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

        Framework.Update += OnFrameworkUpdate;

        Log.Information("OnionSquire initialized successfully.");
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // Periodically refresh retainers if count is 0 or when near summoning bell
        if (++frameworkTicks % 120 == 0)
        {
            if (RetainerService.RetainerCount == 0 || mainWindow.IsOpen)
            {
                RetainerService.RefreshRetainers();
            }
        }
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(ShortCommandName);

        BagSorterService.Dispose();
        AutoRetainerIpc.Dispose();
    }

    private void OnCommand(string command, string args) => ToggleMainUi();

    internal void ToggleMainUi() => mainWindow.Toggle();
}
