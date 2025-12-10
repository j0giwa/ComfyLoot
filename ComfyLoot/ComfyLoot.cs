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
using Dalamud.Game.ClientState.Objects;
using System;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace ComfyLoot;

/// <summary>
/// ComfyLoot plugin core
/// </summary>
public sealed class ComfyLoot : IDalamudPlugin
{
	private const string CommandName = "/loot";
	public readonly WindowSystem WindowSystem = new("ComfyLoot");

	[PluginService]
	internal static IChatGui ChatGui { get; private set; } = null!;
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

	[PluginService]
	public static ITargetManager TargetManager { get; private set; } = null!;

	private IDtrBarEntry? dtrEntry;
	private ConfigWindow ConfigWindow { get; init; }
	private MainWindow MainWindow { get; init; }
	public Configuration Configuration { get; init; }
	public LootManager LootManager { get; set; }
	public InventoryWatcher Watcher { get; set; }

	public string HomeworldName { get; private set; }
	public string TradeParterName { get; private set; }

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

#if DEBUG 
		HomeworldName = "Balmung";
#endif //* DEBUG */

		LootManager = new LootManager(this);

		/* HACK: Force initalisation in case of restart 
		   actuall save init in OnLogin() */
		if (ClientState.IsLoggedIn) {
			Watcher = new InventoryWatcher(this, LootManager);
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

		Dalamud.UiBuilder.Draw += DrawUI;
		Dalamud.UiBuilder.OpenMainUi += ToggleMainUI;
		Dalamud.UiBuilder.OpenConfigUi += ToggleConfigUI;

		ClientState.Login += OnLogin;
		ClientState.Logout += OnLogout;
		ClientState.TerritoryChanged += OnTerritoryChanged;
	
		ChatGui.ChatMessage += OnChatMessage;

		InitializeDtrBar();
	}

	private void 
	OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
	{
		string? name;

		if (type != XivChatType.SystemMessage)
			return;

		name = Util.GetTradePartner(message.ToString());

		if (name != null)
			TradeParterName = name;
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
		uint zone;
		string zoneName;

		if (dtrEntry == null
		|| LootManager == null)
			return;

		dtrEntry.Shown = Configuration.ShowDtrBar;
		dtrEntry.Tooltip = "Click to toggle overlay";

		zone = ClientState.TerritoryType;
		zoneName = Util.GetZoneName(zone);

		switch (Configuration.DtrBarOption) {
		case DtrBarOption.TOTAL_QUANTITY:
			number = LootManager.GetTotalItemQuantity();
			dtrEntry.Text = $"Total: {number}";
			break;
		case DtrBarOption.ZONE_QUANTITY:
			number = LootManager.GetZoneItemQuantity(zoneName);
			dtrEntry.Text = $"{zoneName}: {number}";
			break;
		case DtrBarOption.TOTAL_VALUE:
			number = LootManager.GetTotalItemValue();
			dtrEntry.Text = $"Total: {Util.FormatGil(number)}";
			break;
		case DtrBarOption.ZONE_VALUE:
			number = LootManager.GetZoneItemValue(zoneName);
			dtrEntry.Text = $"{zoneName}: {Util.FormatGil(number)}";
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
	OnDtrBarClick(DtrInteractionEvent e)
	{
		int index;
		DtrBarOption[]? values;

		if (e.ClickType == MouseClickType.Left)
			ToggleMainUI();

		if (e.ClickType == MouseClickType.Right) {
			values = (DtrBarOption[])Enum.GetValues(typeof(DtrBarOption));

			index = Array.IndexOf(values, Configuration.DtrBarOption);
			index = (index + 1) % values.Length;

			Configuration.DtrBarOption = values[index];
			Configuration.Save();

			UpdateDtrBar();
		}
	}

	private void 
	OnLogin()
	{
		Log.Verbose("[ComfyLoot] Initializing");

		HomeworldName = Util.GetHomeWorld();

		if (LootManager == null 
		|| LootManager.IsDisposed) {
			LootManager?.Dispose();
			LootManager = new LootManager(this);
		}

		if (Watcher == null 
		|| Watcher.IsDisposed) {
			Watcher?.Dispose();
			Watcher = new InventoryWatcher(this, LootManager);
		}

		UpdateDtrBar();
	}

	private void
	OnLogout(int type, int code)
	{
		Log.Verbose("[ComfyLoot] Cleaning up");

		/* Cleanling up after logout to prevent issues witch character switches */
		LootManager.Dispose();
		Watcher.Dispose();
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
		Log.Verbose("[ComfyLoot] Disposing Plugin");

		WindowSystem.RemoveAllWindows();

		ConfigWindow.Dispose();
		MainWindow.Dispose();
		LootManager?.Dispose();
		Watcher?.Dispose();

		Commands.RemoveHandler(CommandName);

		ClientState.Login -= OnLogin;
		ClientState.Logout -= OnLogout;
		ClientState.TerritoryChanged -= OnTerritoryChanged;
		ChatGui.ChatMessage -= OnChatMessage;
	}
}