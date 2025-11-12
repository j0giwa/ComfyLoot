/* See LICENSE file for copyright and license details. */
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using ComfyLoot.Managers;
using ComfyLoot.Windows;

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

	public string HomeworldName { get; private set; }

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

		/* HACK: Force initalisation in case of restart 
		   actuall save init in OnLogin() */
		if (ClientState.IsLoggedIn) {
			Watcher = new InventoryWatcher(LootManager);
			HomeworldName = Util.GetHomeWorld();
		}

		ConfigWindow = new ConfigWindow(this);
		MainWindow = new MainWindow(this, LootManager);

		WindowSystem.AddWindow(ConfigWindow);
		WindowSystem.AddWindow(MainWindow);

		Commands.AddHandler(CommandName,
			new CommandInfo(OnCommand) {
				HelpMessage = "Toggle ComfyLoot window\n/loot config → Open settings"
			});

		ClientState.Login += OnLogin;
		ClientState.Logout += OnLogout;
		ClientState.TerritoryChanged += OnTerritoryChanged;
		Dalamud.UiBuilder.Draw += DrawUI;
		Dalamud.UiBuilder.OpenMainUi += ToggleMainUI;
		Dalamud.UiBuilder.OpenConfigUi += ToggleConfigUI;

		InitializeDtrBar();
	}

	private void
	InitializeDtrBar()
	{
		dtrEntry = DtrBar.Get("ComfyLoot");

		if (dtrEntry != null) {
			dtrEntry.OnClick = OnDtrBarClick;
			UpdateDtrBar();
			dtrEntry.Shown = Configuration.ShowDtrBar;
		}
	}

	public void
	UpdateDtrBar()
	{
		int number;
		string zoneName;

		if (dtrEntry == null
		|| LootManager == null)
			return;

		dtrEntry.Shown = Configuration.ShowDtrBar;
		dtrEntry.Tooltip = "Click to toggle overlay";

		zoneName = Util.GetCurrentZoneName();

		switch (Configuration.DtrBarOption) {
		case 0:
			number = LootManager.GetTotalItemQuantity();
			dtrEntry.Text = $"Total: {number}";
			break;
		case 1:
			number = LootManager.GetZoneItemQuantity(zoneName);
			dtrEntry.Text = $"{zoneName}: {number}";
			break;
		case 2:
			number = LootManager.GetTotalItemValue();
			dtrEntry.Text = $"Total: {Util.FormatGilSting(number)}";
			break;
		case 3:
			number = LootManager.GetZoneItemValue(zoneName);
			dtrEntry.Text = $"{zoneName}: {Util.FormatGilSting(number)}";
			break;
		default:
			dtrEntry.Text = "ComfyLoot: N/A";
			break;
		}
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

	private void
	OnLogin()
	{
		/* safely initalizing services */
		HomeworldName = Util.GetHomeWorld();
		Watcher = new InventoryWatcher(LootManager);
		UpdateDtrBar();
	}

	private void
	OnLogout(int type, int code)
	{
		/* Cleanling up after logout to prevent issues witch character switches */
		LootManager.Clear();
	}

	private void
	OnTerritoryChanged(ushort obj)
	{
		UpdateDtrBar();
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