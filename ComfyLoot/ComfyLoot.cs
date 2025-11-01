/* See LICENSE file for copyright and license details. */
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using ComfyLoot.Managers;
using ComfyLoot.Windows;
using Dalamud.Game.Gui.Dtr;

namespace ComfyLoot;

/// <summary>
/// ComfyLoot plugin core
/// </summary>
public sealed class ComfyLoot : IDalamudPlugin
{
	private const string CommandName = "/loot";
	public readonly WindowSystem WindowSystem = new("ComfyLoot");
	
	[PluginService]
	internal static IDalamudPluginInterface Dalamud { get; private set; } = null!;
	[PluginService]
	internal static ITextureProvider Textures { get; private set; } = null!;
	[PluginService]
	internal static ICommandManager Commands { get; private set; } = null!;
	[PluginService]
	internal static IClientState ClientState { get; private set; } = null!;
	[PluginService]
	internal static IDataManager DataManager { get; private set; } = null!;
	[PluginService]
	internal static IPluginLog Log { get; private set; } = null!;
	[PluginService]
	internal static IGameInventory GameInventory { get; private set; } = null!;
	[PluginService] 
	internal static IDtrBar DtrBar { get; private set; } = null!;

	private IDtrBarEntry? dtrEntry;
	private ConfigWindow ConfigWindow { get; init; }
	private MainWindow MainWindow { get; init; }
	public Configuration Configuration { get; init; }
	public LootManager LootManager { get; set; }
	public InventoryWatcher Watcher { get; set; }

	/// <summary>
	/// ComfyLoot:ctor
	/// </summary>
	public ComfyLoot()
	{
		IPluginConfiguration? rawConfig;
		Configuration config;

		rawConfig = Dalamud.GetPluginConfig();
		if (rawConfig is Configuration configuration)
			config = configuration;
		else
			config = new Configuration();
		Configuration = config;

		LootManager = new LootManager(this);
		Watcher = new InventoryWatcher(LootManager);

		ConfigWindow = new ConfigWindow(this);
		MainWindow = new MainWindow(this);

		WindowSystem.AddWindow(ConfigWindow);
		WindowSystem.AddWindow(MainWindow);

		Commands.AddHandler(CommandName,
			new CommandInfo(OnCommand) {
				HelpMessage = "Toggle ComfyLoot window\n/loot config → Open settings"
			});

		InitializeDtrBar();

		Dalamud.UiBuilder.Draw += DrawUI;
		Dalamud.UiBuilder.OpenMainUi += ToggleMainUI;
		Dalamud.UiBuilder.OpenConfigUi += ToggleConfigUI;
	}


	private void
	InitializeDtrBar()
	{
		dtrEntry = DtrBar.Get("ComfyLoot");

		if (dtrEntry != null) {
			
			dtrEntry.OnClick = OnDtrBarClick;
			UpdateDtrBar();
			dtrEntry.Shown = Configuration.ShowDtrBar;

			Log.Info("DTR bar entry initialized");
		}
	}

	public void
	UpdateDtrBar()
	{
		if (dtrEntry == null)
			return;
		dtrEntry.Shown = Configuration.ShowDtrBar;
		dtrEntry.Text = $"Loot: {LootManager.GetTotalItemQuantity()} items";
		dtrEntry.Tooltip = "Click to toggle overlay";
	}

	private void
	OnCommand(string command, string args)
	{
		switch (args.Trim().ToLower()) {
		case "config":
			ToggleConfigUI();
			break;
		default:
			ToggleMainUI();
			break;
		}
	}

	private void 
	OnDtrBarClick(DtrInteractionEvent _)
	{
		ToggleMainUI();
	}

	private void DrawUI() => WindowSystem.Draw();

	public void ToggleConfigUI() => ConfigWindow.Toggle();
	public void ToggleMainUI() => MainWindow.Toggle();

	public void
	Dispose()
	{
		WindowSystem.RemoveAllWindows();

		ConfigWindow.Dispose();
		MainWindow.Dispose();

		Commands.RemoveHandler(CommandName);
	}
}