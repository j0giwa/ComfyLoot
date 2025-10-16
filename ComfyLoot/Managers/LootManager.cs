/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

using ComfyLoot.Data;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace ComfyLoot.Managers;

/// <summary>
/// A drop picked up by the player
/// </summary>
public record LootItem(
	uint ItemId,
	int Quantity,
	int Value
);

/// <summary>
/// Kees track of Player loot
/// </summay>
public class LootManager : IDisposable {

	public readonly object _lock;
	private readonly IPluginLog _log;
	private readonly Dictionary<string, List<LootItem>> _loot;

	/// <summary>
	/// Droplist, contains everything the player collected
	/// </summary>
	public IReadOnlyDictionary<string, List<LootItem>> Loot
	{
		get
		{
			lock (_lock)
			{
				return new Dictionary<string, List<LootItem>>(_loot);
			}
		}
	}

	/// <summary>
	/// LootManager:ctor
	/// </summary>
	/// <param name="log">Logger</param>
	public LootManager(IPluginLog log)
	{
		this._log = log;
		_loot = new Dictionary<string, List<LootItem>>();
		_lock = new();
	}

	/// <summary>
	/// Gets the name of the current zone.
	/// aka: Where is the player right now?
	/// </summary>
	/// <returns>Name of the current zone</returns>
	private static string
	GetCurrentZoneName()
	{
		uint id;
		string? name;
		ExcelSheet<TerritoryType> sheet;
		TerritoryType zoneRow;

		name = null;
		id = ComfyLoot.ClientState.TerritoryType;
		sheet = ComfyLoot.DataManager.GetExcelSheet<TerritoryType>();

		if (sheet != null
		&& sheet.TryGetRow(id, out zoneRow))
			name = zoneRow.PlaceName.Value.Name.ToString();

		if (name == null) /* In case for (unlikely) failures */
			name = "Unknown Zone";

		return name;
	}

	/// <summary>
	/// Gets the gil value of the given item
	/// </summary>
	/// <param name="itemId">Item identyfier</param>
	/// <returns>
	/// Item count, if the item is gil;
	/// Vendor value, if the item is meant to be sold to vendors;
	/// Univesalis value, if the item can be sold on the marketboard;
	/// 0, if none of the above applies</item>
	/// </returns>
	private async Task<int>
	GetItemValue(uint itemId, bool hq)
	{
		int value;
		string worldname;

		switch (itemId) {
		case (int)Currency.GIL:
			value = 1;
			break;
		case (int)SpecialItems.ALLAGAN_TIN_PIECE:
			value = 25;
			break;
		case (int)SpecialItems.ALLAGAN_BRONZE_PIECE:
		case (int)SpecialItems.NIGHTWORLD_BRONZE_PIECE:
			value = 100;
			break;
		case (int)SpecialItems.ALLAGAN_SILVER_PIECE:
		case (int)SpecialItems.NIGHTWORLD_SILVER_PIECE:
			value = 500;
			break;
		case (int)SpecialItems.ALLAGAN_GOLD_PIECE:
			value = 2500;
			break;
		case (int)SpecialItems.ALLAGAN_PLATINUM_PIECE:
			value = 10000;
			break;
			default:
				value = 0; /* fallback value */
				worldname = GetHomeWorld();
				_log.Information(worldname);
				value = await GetUniveralisValue(itemId, worldname, hq);
				break;
		}

		return value;
	}

	private unsafe string
	GetHomeWorld()
	{
		uint id;
		string? name;
		ExcelSheet<World> sheet;
		World worldRow;

		name = null;
		id = AgentLobby.Instance()->LobbyData.HomeWorldId;
		sheet = ComfyLoot.DataManager.GetExcelSheet<World>();

		if (sheet != null
		    && sheet.TryGetRow(id, out worldRow))
			name = worldRow.Name.ToString();

		if (name == null) /* In case of (unlikely) failures */
			name = "Unknown Homeworld";

		return name;
	}

	private static async Task<int>
	GetUniveralisValue(uint itemId, string worldname, bool hq)
	{
		const string endpoint = "https://universalis.app";

		int value;
		string uri;
		MarketBoardData? data;

		value = 0;

		if (worldname.Equals("Unknown World"))
			return value;

		uri = $"{endpoint}/api/v2/aggregated/{worldname}/{itemId}";
		data = await HttpHelper.GetAsync<MarketBoardData>(uri);

		if (data == null
		|| data.Results == null
		|| data.Results.Count == 0)
			return value;

		if (hq)
			value = (int)data.Results[0].HQ.
				MinListing.World.Price;
		else
			value = (int)data.Results[0].NQ.
				MinListing.World.Price;

		return value;
	}

	/// <summary>
	/// Determines if the given item ID represents a currency.
	/// </summary>
	private static bool
	IsCurrency(uint itemId)
	{
		switch (itemId)
		{
			case (int)Currency.GIL: /* FALLTHROUGH */
			case (int)Currency.STORM_SEAL:
			case (int)Currency.SERPENT_SEAL:
			case (int)Currency.FLAME_SEAL:
			case (int)Currency.ALLIED_SEALS:
			case (int)Currency.WOLF_MARKS:
			case (int)Currency.MGP:
			case (int)Currency.TROPHY_CRYSTALS:
			case (int)Currency.TOMESTONE_POETICS:
			case (int)Currency.TOMESTONE_AESTETICS:
			case (int)Currency.TOMESTONE_MATHEMATICS:
			case (int)Currency.TOMESTONE_HELIOMETRY:
			case (int)Currency.CENTURIO_SEALS:
			case (int)Currency.SACK_OF_NUTS:
			case (int)Currency.BICOLOR_GEMSTONES:
			case (int)Currency.WHITE_CRAFTER_SCRIPS:
			case (int)Currency.PURPLE_CRAFTER_SCRIPS:
			case (int)Currency.ORANGE_CRAFTER_SCRIPS:
			case (int)Currency.WHITE_GATHERER_SCRIPS:
			case (int)Currency.PURPLE_GATHERER_SCRIPS:
			case (int)Currency.ORANGE_GATHERER_SCRIPS:
			case (int)Currency.SKYBUILDER_SCRIPS:
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// Add an Item to the droplist
	/// </summary>
	/// <param name="addedItem">The Item</param>
	public async Task
	AddItem(InventoryItemAddedArgs addedItem)
	{
		uint id;
		int quantity;
		int itemValue;
		string zone;
		LootItem item;
		List<LootItem>? list;

		id = addedItem.Item.ItemId;
		quantity = addedItem.Item.Quantity;
		zone = GetCurrentZoneName();

		itemValue = await GetItemValue(id, addedItem.Item.IsHq);

		item = new LootItem(
			id,
		    	quantity,
			itemValue
		);

		lock (_lock) {
			if (!_loot.TryGetValue(zone, out list))
				list = new List<LootItem>();

			list.Add(item);
		}

		_log.Information(
			"[TRACK] {Quantity}x {ItemId} in {Zone}",
			quantity,
			id,
			zone);

		_loot[zone] = list;
	}

	/// <summary>
	/// Calculate the combined item value across all zones.
	/// </summary>
	/// <param name="zoneItems">The list of items in the zone.</param>
	/// <returns>Total amount of gil.</returns>
	public int
	GetTotalItemValue()
	{
		int totalQuantity = 0;

		foreach (List<LootItem> zoneList in _loot.Values)
			totalQuantity += GetZoneItemValue(zoneList);

		return totalQuantity;
	}

	/// <summary>
	/// Counts items across all zones.
	/// </summary>
	/// <returns>Total number of non-currency items gathered.</returns>
	public int
	GetTotalItemQuantity()
	{
		int totalQuantity = 0;

		foreach (List<LootItem> zoneList in _loot.Values)
			totalQuantity += GetZoneItemQuantity(zoneList);

		return totalQuantity;
	}

	/// <summary>
	/// Counts the total quantity of valid (non-currency) items within a single zone.
	/// </summary>
	/// <param name="zoneItems">The list of items in the zone.</param>
	/// <returns>Total number of non-currency items in this zone.</returns>
	public static int
	GetZoneItemQuantity(IEnumerable<LootItem> zoneItems)
	{
		int zoneTotal = 0;

		foreach (var tracked in zoneItems) {
			if (IsCurrency(tracked.ItemId))
				continue;
			zoneTotal += tracked.Quantity;
		}

		return zoneTotal;
	}

	/// <summary>
	/// Calculate the combined item value within a single zone.
	/// </summary>
	/// <param name="zoneItems">The list of items in the zone.</param>
	/// <returns>Total amount of gil.</returns>
	public static int
	GetZoneItemValue(IEnumerable<LootItem> zoneItems)
	{
		int zoneTotal = 0;

		foreach (LootItem item in zoneItems)
			zoneTotal += item.Value;

		return zoneTotal;
	}

	/// <summary>
	/// Updates an item's quantity.
	/// </summary>
	public void
	UpdateItem(uint itemId, int addedAmount)
	{
		string zoneName;
		LootItem? item;
		List<LootItem>? items;

		zoneName = GetCurrentZoneName();
		if (!_loot.TryGetValue(zoneName, out items))
			items = new List<LootItem>();

		item = items.Find(t => t.ItemId == itemId);
		if (item != null) {
			items.Remove(item);
			items.Add(new LootItem(
				itemId,
				item.Quantity + addedAmount,
				item.Value
			));
			_log.Information(
				"[TRACK] {ItemId} x{Quantity} in {Zone} (previous {PreviousQuantity})",
				itemId,
				addedAmount,
				zoneName,
				item.Quantity
			);
		}

		_loot[zoneName] = items;
	}

	public void
	Dispose()
	{}
}