/* See LICENSE file for copyright and license details. */
using System;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Inventory;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using ComfyLoot.Managers;
using ComfyLoot.Windows;
using Dalamud.Game.Chat;

namespace ComfyLoot;

/// <summary>
/// ComfyLoot plugin core
/// </summary>
public sealed class ComfyLoot : IDalamudPlugin {
	private const string CommandName = "/loot";

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
	internal static ITargetManager TargetManager { get; private set; } = null!;

	private readonly WindowSystem WindowSystem;
	private readonly ComfyLootIPC ipc;
	private MainWindow MainWindow { get; init; }
	private ConfigWindow ConfigWindow { get; init; }
	private IDtrBarEntry? dtrEntry;
	
	public string HomeworldName { get; private set; }
	public string? TradeParterName { get; private set; }
	public LootManager LootManager { get; set; }
	public required InventoryWatcher Watcher { get; set; }
	public required Config Configuration { get; init; }
	public required IContextMenu ContextMenu { private get; set; } 

	/// <summary>
	/// ComfyLoot:ctor
	/// </summary>
	public ComfyLoot(IContextMenu contextMenu)
	{
		IPluginConfiguration? rawConfig;
		Config config;

		rawConfig = Dalamud.GetPluginConfig();
		if (rawConfig is Config configuration)
			config = configuration;
		else
			config = new Config();
		Configuration = config;

#if DEBUG
		HomeworldName = "Dev";
#endif //* DEBUG */

		LootManager = new LootManager(this);
		ipc = new ComfyLootIPC(Dalamud, this);

		/* HACK: Force initalisation in case of restart
		   actuall save init in OnLogin() */
		if (ClientState.IsLoggedIn) {
			Watcher = new InventoryWatcher(this, LootManager);
			HomeworldName = Util.GetHomeWorld();
		}

		WindowSystem = new WindowSystem("ComfyLoot");
		ConfigWindow = new ConfigWindow(this);
		MainWindow = new MainWindow(this);

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

		if (!Config.STABLE) {
			this.ContextMenu = contextMenu;
			this.ContextMenu.OnMenuOpened += OnMenuOpened;

			InitializeDtrBar();
		}
	}

	private void DrawUI() => WindowSystem.Draw();

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

	/* HACK: Abusing chat event as a trade event */
	private void
	OnChatMessage(IHandleableChatMessage message)
	{
		/* Trademessagess are on unamed channels (Id's may break on patch) */
		const int tradeChannelOut = 569;
		const int tradeChannelIn = 313;
		string text;
		string? name;

		/* We only care about trade messages */
		if (!((int)message.LogKind == tradeChannelIn
		|| (int)message.LogKind == tradeChannelOut
		|| message.LogKind == XivChatType.SystemMessage))
			return;

		text = message.Message.ToString();

		if (text.Contains("wishes to trade with you.")
		|| text.Contains("Trade request sent to")) {
			name = Util.GetTradePartner();
			Log.Debug($"[TRADE] message=\"{message}\" partner=\"{name}\"");
			if (name != null)
				TradeParterName = name;
		}

		if (text.Equals("Trade complete."))
			TradeParterName = null;
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
	OnMenuOpened(IMenuOpenedArgs args)
	{
		if (!Configuration.ItemContextMenu
		|| args.MenuType != ContextMenuType.Inventory
		|| args.Target is not MenuTargetInventory target)
			return;

		args.AddMenuItem(new MenuItem {
			Name = "Ignore Item",
			PrefixChar = 'C',
			PrefixColor = 0,
			OnClicked = _ => OnItemClicked(target)
		});
	}

	private void
	OnItemClicked(MenuTargetInventory target)
	{
		GameInventoryItem? item;

		item = target.TargetItem;

		if (item == null)
			return;

		Configuration.IgnoredItemIds.Add(item.Value.ItemId);
	}

	private void 
	OnTerritoryChanged(uint obj)
	{
		if (!Config.STABLE)
			UpdateDtrBar();
	}

	public void ToggleConfigUI() => ConfigWindow.Toggle();
	public void ToggleMainUI() => MainWindow.Toggle();

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
		dtrEntry.Tooltip = "Click to toggle overlay\nRightclick to cycle through options";

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

	public void
	Dispose()
	{
		Log.Verbose("[ComfyLoot] Disposing Plugin");

		WindowSystem.RemoveAllWindows();

		ConfigWindow.Dispose();
		MainWindow.Dispose();
		LootManager?.Dispose();
		Watcher?.Dispose();
		ipc.Dispose();

		Commands.RemoveHandler(CommandName);

		ClientState.Login -= OnLogin;
		ClientState.Logout -= OnLogout;
		ClientState.TerritoryChanged -= OnTerritoryChanged;
		ChatGui.ChatMessage -= OnChatMessage;

		if (!Config.STABLE)
			ContextMenu.OnMenuOpened -= OnMenuOpened;
	}
}