/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Text.Json;
using ComfyLoot.Managers;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace ComfyLoot;

public record LootResponse(
	bool Universalis,
	int TotalItems,
	int TotalValue,
	IReadOnlyDictionary<string, List<LootItem>> Loot
);

public sealed class ComfyLootIPC : IDisposable {

	private const string ROOT = "ComfyLoot";

    	private readonly ComfyLoot plugin;

	private readonly ICallGateProvider<string> getLoot;
	private readonly ICallGateProvider<string, bool> resetLoot;
	private readonly ICallGateProvider<uint, bool> ignoreItem;
	private readonly ICallGateProvider<string, bool> ignoreSource;

	/// <summary>
	/// ComfyLootIPC:ctor
 	/// </summary>
	public ComfyLootIPC(IDalamudPluginInterface pluginInterface, ComfyLoot plugin)
	{
		this.plugin = plugin;

		getLoot = pluginInterface.GetIpcProvider<string>($"{ROOT}.GetLoot");
		getLoot.RegisterFunc(GetLoot);

		resetLoot = pluginInterface.GetIpcProvider<string, bool>($"{ROOT}.ResetLoot");
		resetLoot.RegisterFunc(ResetLoot);

		ignoreItem = pluginInterface.GetIpcProvider<uint, bool>($"{ROOT}.IgnoreItem");
		ignoreItem.RegisterFunc(IgnoreItem);

		ignoreSource = pluginInterface.GetIpcProvider<string, bool>($"{ROOT}.IgnoreSource");
		ignoreSource.RegisterFunc(IgnoreSource);
	}

	/// <summary>
	/// Returns the current loot data.
	/// Includes total item count, total value, Universalis status, and the complete loot dictionary.
	/// IPC: ComfyLoot.GetLoot
	/// </summary>
	/// <returns>
	/// A JSON string containing the current loot state.
	/// </returns>
	private string
	GetLoot()
	{
		LootResponse response = new LootResponse(
			Universalis: plugin.Configuration.UniversalisEnabled,
			TotalItems: plugin.LootManager.GetTotalItemQuantity(),
			TotalValue: plugin.LootManager.GetTotalItemValue(),
			plugin.LootManager.Loot
		);

		return JsonSerializer.Serialize(response);
	}

	/// <summary>
	/// Adds an item ID to the ignored item list.
	/// Ignored items will will no longer be tracked.
	/// IPC: ComfyLoot.IgnoreItem
	/// </summary>
	/// <param name="id">
	/// The ID of the Item to ignore.
	/// </param>
	/// <returns>
	/// <code>true</code>
	/// </returns>
	private bool 
	IgnoreItem(uint id)
	{
		plugin.Configuration.IgnoredItemIds.Add(id);
		plugin.Configuration.Save();
		return true; /* HACK: ICallGateProvider doesn't seem to accept voids */
	}

	/// <summary>
	/// Adds a loot source/zone name to the ignored source list.
	/// Loot from ignored sources will no longer be tracked.
	/// IPC: ComfyLoot.IgnoreSource
	/// </summary>
	/// <param name="name">
	/// The name of the source or zone to ignore.
	/// </param>
	/// <returns>
	/// <code>true</code>
	/// </returns>
	private bool 
	IgnoreSource(string name)
	{
		plugin.Configuration.IgnoredZones.Add(name);
		plugin.Configuration.Save();
		return true; /* HACK: ICallGateProvider doesn't seem to accept voids */
	}

	/// <summary>
	/// Removes all stored loot data for the specified key.
	/// IPC: ComfyLoot.ResetLoot
	/// </summary>
	/// <param name="key">
	/// The loot key/zone identifier to clear.
	/// </param>
	/// <returns>
	/// True when the reset operation has completed.
	/// </returns>
	private bool 
	ResetLoot(string key)
	{
		plugin.LootManager.ClearZone(key);
		return true; /* HACK: ICallGateProvider doesn't seem to accept voids */
	}
	
	public void
	Dispose()
	{
		getLoot.UnregisterFunc();
		resetLoot.UnregisterFunc();
		ignoreItem.UnregisterFunc();
		ignoreSource.UnregisterFunc();
    	}
}
