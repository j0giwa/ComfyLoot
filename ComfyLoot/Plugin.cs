using System.IO;
using Dalamud.Game.Command;
using Dalamud.Game.Inventory;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ComfyLoot.Windows;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using System;
using Lumina.Excel.Sheets;

namespace ComfyLoot;

public sealed class Plugin : IDalamudPlugin
{
	[PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
	[PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] internal static IClientState ClientState { get; private set; } = null!;
	[PluginService] internal static IDataManager DataManager { get; private set; } = null!;
	[PluginService] internal static IPluginLog Log { get; private set; } = null!;

	[PluginService] internal static IGameInventory GameInventory { get; private set; } = null!; // move somewhere else

	private const string CommandName = "/loot";

	public Configuration Configuration { get; init; }
	public readonly WindowSystem WindowSystem = new("ComfyLoot");
	private ConfigWindow ConfigWindow { get; init; }
	private MainWindow MainWindow { get; init; }

	//private InventoryManager inventoryManager; 

	/// <summary>
	/// Plugin:ctor
	/// </summary>
	public Plugin()
	{
		Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		// You might normally want to embed resources and load them from the manifest stream
		//var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

		ConfigWindow = new ConfigWindow(this);
		MainWindow = new MainWindow(this);

		WindowSystem.AddWindow(ConfigWindow);
		WindowSystem.AddWindow(MainWindow);

		CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
		{
			HelpMessage = "A useful message to display in /xlhelp"
		});

		PluginInterface.UiBuilder.Draw += DrawUI;

		// This adds a button to the plugin installer entry of this plugin which allows
		// toggling the display status of the configuration ui
		PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

		// Adds another button doing the same but for the main ui of the plugin
		PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

		// Add a simple message to the log with level set to information
		// Use /xllog to open the log window in-game
		// Example Output: 00:57:54.959 | INF | [SamplePlugin] ===A cool log message from Sample Plugin===
		//Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");

		GameInventory.InventoryChanged += OnInventoryChanged;
	}

	private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
	{
		Log.Information($"=== Inventory changed ===");
		foreach (var evt in events)
		{
			Log.Information($"type = {evt.Type}");

			if (evt is InventoryItemAddedArgs added)
			{
				Log.Information($"===Item added {added.Item.ItemId}===");
			}
		}
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