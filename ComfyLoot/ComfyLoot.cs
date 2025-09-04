
using ComfyLoot.Servive;
using ComfyLoot.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ComfyLoot;

public sealed class ComfyLoot : IDalamudPlugin
{
	[PluginService]
	internal static IDalamudPluginInterface Dalamud { get; private set; } = null!;
	[PluginService]
	internal static ITextureProvider TextureProvider { get; private set; } = null!;
	[PluginService]
	internal static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService]
	internal static IClientState ClientState { get; private set; } = null!;
	[PluginService]
	internal static IDataManager DataManager { get; private set; } = null!;
	[PluginService]
	internal static IPluginLog Log { get; private set; } = null!;
	[PluginService]
	internal static IGameInventory GameInventory { get; private set; } = null!;

	private const string CommandName = "/loot";

	public Configuration Configuration { get; init; }
	public readonly WindowSystem WindowSystem = new("ComfyLoot");
	private ConfigWindow ConfigWindow { get; init; }
	private MainWindow MainWindow { get; init; }

	private InventoryWatcher watcher;

	/// <summary>
	/// Plugin:ctor
	/// </summary>
	public ComfyLoot()
	{
		object? rawConfig;
		Configuration config;

		rawConfig = Dalamud.GetPluginConfig();
		if (rawConfig != null && rawConfig is Configuration)
			config = (Configuration)rawConfig;
		else
			config = new Configuration();
		Configuration = config;

		// You might normally want to embed resources and load them from the manifest stream
		//var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

		ConfigWindow = new ConfigWindow(this);
		MainWindow = new MainWindow(this);

		WindowSystem.AddWindow(ConfigWindow);
		WindowSystem.AddWindow(MainWindow);

		CommandManager.AddHandler(CommandName,
			new CommandInfo(OnCommand){
				HelpMessage = "A useful message to display in /xlhelp"
		});

		watcher = new InventoryWatcher(GameInventory, Log);

		Dalamud.UiBuilder.Draw += DrawUI;

		// This adds a button to the plugin installer entry of this plugin which allows
		// toggling the display status of the configuration ui
		Dalamud.UiBuilder.OpenConfigUi += ToggleConfigUI;

		// Adds another button doing the same but for the main ui of the plugin
		Dalamud.UiBuilder.OpenMainUi += ToggleMainUI;
	}

	public void
	Dispose()
	{
		WindowSystem.RemoveAllWindows();

		ConfigWindow.Dispose();
		MainWindow.Dispose();

		CommandManager.RemoveHandler(CommandName);
	}

	private void
	OnCommand(string command, string args)
	{
		// In response to the slash command, toggle the display status of our main ui
		ToggleMainUI();
	}

	private void DrawUI() => WindowSystem.Draw();

	public void ToggleConfigUI() => ConfigWindow.Toggle();
	public void ToggleMainUI() => MainWindow.Toggle();
}