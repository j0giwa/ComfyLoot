/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.Inventory.InventoryEventArgTypes;

using ComfyLoot.Models;

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

	private readonly ComfyLoot plugin;
	private readonly Dictionary<string, List<LootItem>> loot;
	private readonly Configuration config;

	/// <summary>
	/// Droplist, contains everything the player collected
	/// </summary>
	public IReadOnlyDictionary<string, List<LootItem>> Loot => loot;

	/// <summary>
	/// LootManager:ctor
	/// </summary>
	public LootManager(ComfyLoot plugin)
	{
		this.plugin = plugin;
		config = plugin.Configuration;
		loot = new Dictionary<string, List<LootItem>>();
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
			if (config.UniversalisEnabled) {
				worldname = Util.GetHomeWorld();
				if (!worldname.Equals("???"))
					value = await GetUniveralisValue(itemId, worldname, hq);
			}
			break;
		}

		return value;
	}

	/// <summary>
	/// Fetches itmes marketboard value form universalis
	/// </summary>
	/// <param name="itemId">Item identifier</param>
	/// <param name="worldname">World to fecht mb data from</param>
	/// <param name="hq">high quality or no</param>
	/// <returns>The itmes value in gil</returns>
	private static async Task<int>
	GetUniveralisValue(uint itemId, string worldname, bool hq)
	{
		const string endpoint = "https://universalis.app";

		int value;
		string uri;
		MarketBoardData? data;

		if (worldname.Equals("???")){
			ComfyLoot.Log.Error("[Universalis] Failed to retrieve data: Unknown world");
			return 0;
		}

		uri = $"{endpoint}/api/v2/aggregated/{worldname}/{itemId}";
		data = await HttpHelper.GetAsync<MarketBoardData>(uri);

		if (data == null
		|| data.Results == null
		|| data.Results.Count == 0) {
			ComfyLoot.Log.Error("[Universalis] Failed to retrieve data: Invalid response");
			return 0;
		}

		value = GetMarketValue(data, hq);

		return value;
	}

	private static int
	GetMarketValue(MarketBoardData data, bool hq)
	{
		AggregatedResult? result;
		QualityData? qualityData;
		double price = 0;

		if (data == null
		|| data.Results == null
		|| data.Results.Count == 0)
			return 0;

		result = data.Results[0];
		if (result == null)
			return 0;

		if (hq)
			qualityData = result.HQ;
		else
			qualityData = result.NQ;

		if (qualityData == null)
			return 0;


		if (qualityData.MinListing != null
		&& qualityData.MinListing.World != null)
			price = qualityData.MinListing.World.Price;

		return (int)price;
	}

	/// <summary>
	/// Determines if the given item ID represents a currency.
	/// </summary>
	private static bool
	IsCurrency(uint itemId)
	{
		/* TODO: Lumina lookup instead of hardcoding*/
		
		switch (itemId) {
		case (int)Currency.GIL: 
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
		string zone;

		id = addedItem.Item.ItemId;
		quantity = addedItem.Item.Quantity;
		zone = Util.GetCurrentZoneName();

		await AddItem(id, quantity, zone, addedItem.Item.IsHq);
	}

	/// <summary>
	/// Add an Item to the droplist
	/// </summary>
	/// <param name="id">Item identifier</param>
	/// <param name="quantity">Amount of items</param>
	/// <param name="zoneName">Zone where the item was gathered</param>
	/// <param name="hq">High quality or no</param>
	public async Task
	AddItem(uint id, int quantity, string zoneName, bool hq)
	{
		int itemValue;
		LootItem item;
		List<LootItem>? list;

		/* HACK: prevent duplicates in the same zone */
		if (CheckDuplicate(zoneName, id)) {
			UpdateItem(id, quantity);
			return;
		}

		itemValue = await GetItemValue(id, hq);

		item = new LootItem(
			id,
			quantity,
			itemValue
		);

		if (!loot.TryGetValue(zoneName, out list))
			list = new List<LootItem>();
		list.Add(item);

		ComfyLoot.Log.Information(
			"[TRACK] {Quantity}x {ItemId} in {Zone}",
			quantity,
			id,
			zoneName);

		loot[zoneName] = list;

		plugin.UpdateDtrBar();
	}

	/// <summary>
	/// Check if an Item already exists in a given zone
	/// </summary>
	/// <param name="zone">Zone to check</param>
	/// <param name="id">item id</param>
	/// <returns>true, if the item</returns>
	public bool
	CheckDuplicate(string zone, uint id)
	{
		List<LootItem>? zoneItems;

		if (string.IsNullOrEmpty(zone) 
		|| loot == null)
			return false;

		if (loot.TryGetValue(zone, out zoneItems))
			return false;

		if (zoneItems == null)
			return false;

		foreach (LootItem item in zoneItems)
			if (item.ItemId == id)
				return true;

		return false;
	}

	/// <summary>
	/// Calculate the combined item value across all zones.
	/// </summary>
	/// <param name="zoneItems">The list of items in the zone.</param>
	/// <returns>Total amount of gil.</returns>
	public int
	GetTotalItemValue()
	{
		int totalValue = 0;

		foreach (List<LootItem> zoneList in loot.Values)
			totalValue += GetZoneItemValue(zoneList);

		return totalValue;
	}

	/// <summary>
	/// Counts items across all zones.
	/// </summary>
	/// <returns>Total number of non-currency items gathered</returns>
	public int
	GetTotalItemQuantity()
	{
		int totalQuantity = 0;

		foreach (List<LootItem> zoneList in loot.Values)
			totalQuantity += GetZoneItemQuantity(zoneList);

		return totalQuantity;
	}

	/// <summary>
	/// Counts the total quantity of valid (non-currency) items within a single zone.
	/// </summary>
	/// <param name="zoneItems">The list of items in the zone</param>
	/// <returns>Total number of non-currency items in this zone</returns>
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
	/// Counts the total quantity of valid (non-currency) items within a single zone.
	/// </summary>
	/// <param name="Loot">Lootmanager Instance</param>
	/// <param name="zone">zonename</param>
	/// <returns>Total number of non-currency items in this zone</returns>
	public static int
	GetZoneItemQuantity(LootManager loot, string zone)
	{
		List<LootItem>? zoneItems;

		if (loot == null
		|| string.IsNullOrEmpty(zone))
			return 0;

		if (loot.Loot == null)
			return 0;

		loot.Loot.TryGetValue(zone, out zoneItems);

		if (zoneItems == null)
			return 0;

		return GetZoneItemQuantity(zoneItems);
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
			zoneTotal += item.Value * item.Quantity;

		return zoneTotal;
	}

	/// <summary>
	/// Calculate the combined item value within a single zone.
	/// </summary>
	/// <param name="Loot">Lootmanager Instance</param>
	/// <param name="zone">zonename</param>
	/// <returns>Total amount of gil.</returns>
	public static int
	GetZoneItemValue(LootManager loot, string zone)
	{
		List<LootItem>? zoneItems;

		if (loot == null
		|| string.IsNullOrEmpty(zone))
			return 0;

		if (loot.Loot == null)
			return 0;

		loot.Loot.TryGetValue(zone, out zoneItems);

		if (zoneItems == null)
			return 0;

		return GetZoneItemValue(zoneItems);
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

		/* TODO: this line gets repeapted a lot, could be passed as param instead */
		zoneName = Util.GetCurrentZoneName(); 

		if (!loot.TryGetValue(zoneName, out items))
			items = new List<LootItem>();

		item = items.Find(t => t.ItemId == itemId);
		if (item != null) {
			items.Remove(item);
			items.Add(new LootItem(
				itemId,
				item.Quantity + addedAmount,
				item.Value
			));
			ComfyLoot.Log.Information(
				"[TRACK] {ItemId} {Quantity}x in {Zone}",
				itemId,
				item.Quantity + addedAmount,
				zoneName);
		}

		loot[zoneName] = items;
		plugin.UpdateDtrBar();
	}

	public void
	Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void
	Dispose(bool disposing)
	{
		/* Cleanup */
	}
}