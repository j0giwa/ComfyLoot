/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ComfyLoot.Models;

namespace ComfyLoot.Managers;

/// <summary>
/// A drop picked up by the player
/// </summary>
public record LootItem(
	uint ItemId,
	byte Rarity,
	int Quantity,
	int Value
);

/// <summary>
/// Kees track of Player loot
/// </summay>
public class LootManager : IDisposable {

	private readonly ComfyLoot plugin;
	private readonly Dictionary<string, List<LootItem>> loot;
	private readonly Lock lootLock;
	private readonly Configuration config;

	/// <summary>
	/// Droplist, contains everything the player collected
	/// </summary>
	public IReadOnlyDictionary<string, List<LootItem>> Loot {
		get {
			Dictionary<string, List<LootItem>> snapshot;

			lock (lootLock) {
				snapshot = new Dictionary<string, List<LootItem>>();
				foreach (KeyValuePair<string, List<LootItem>> kvp in loot)
					snapshot[kvp.Key] = new List<LootItem>(kvp.Value);
				return snapshot;
			}
		}
	}

	/// <summary>
	/// LootManager:ctor
	/// </summary>
	public LootManager(ComfyLoot plugin)
	{
		this.plugin = plugin;
		config = plugin.Configuration;
		loot = new Dictionary<string, List<LootItem>>();
		lootLock = new Lock();
	}

	/// <summary>
	/// Check if an Item already exists in a given zone
	/// </summary>
	/// <param name="zone">Zone to check</param>
	/// <param name="id">item id</param>
	/// <returns>true, if the item</returns>
	private bool
	CheckDuplicate(string zone, uint id)
	{
		List<LootItem>? zoneItems;

		lock (lootLock) {
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
	GetItemGilValue(uint itemId, bool hq)
	{
		string worldname;

		/* currencys (except gil) don't have a "value", skipping */
		if (Util.IsCurrency(itemId) && itemId != 1)
			return 0;

		switch (itemId) {
		case (int)SpecialItems.GIL:
			return 1;
		case (int)SpecialItems.ALLAGAN_TIN_PIECE:
			return 25;
		case (int)SpecialItems.ALLAGAN_BRONZE_PIECE:
		case (int)SpecialItems.NIGHTWORLD_BRONZE_PIECE:
			return 100;
		case (int)SpecialItems.ALLAGAN_SILVER_PIECE:
		case (int)SpecialItems.NIGHTWORLD_SILVER_PIECE:
			return 500;
		case (int)SpecialItems.ALLAGAN_GOLD_PIECE:
			return 2500;
		case (int)SpecialItems.ALLAGAN_PLATINUM_PIECE:
			return 10000;
		default: /* marketboard value (if eligible) */
			if (!config.UniversalisEnabled)
				return 0;

			worldname = plugin.HomeworldName;

			/* prevent unnessary api calls that will fail anyway */
			if (worldname.Equals("???")) {
				ComfyLoot.Log.Error("[Universalis] Unknown world");
				return 0;
			}

			if (Util.IsUntradable(itemId)) {
				ComfyLoot.Log.Warning("[Universalis] Item Untradable");
				return 0;
			}

			return await GetUniveralisValue(itemId, worldname, hq);
		}
	}

	/// <summary>
	/// Fetches itmes marketboard value from universalis
	/// </summary>
	/// <param name="itemId">Item identifier</param>
	/// <param name="worldname">World to fetch marketboarddata</param>
	/// <param name="hq">high quality or no</param>
	/// <returns>The items markerboardvalue, will return 0 on errors or invalid data</returns>
	private static async Task<int>
	GetUniveralisValue(uint itemId, string worldname, bool hq)
	{
		const string endpoint = "https://universalis.app/api/v2";

		string uri;
		MarketBoardData? data;

		if (worldname.Equals("???")) {
			ComfyLoot.Log.Error("[Universalis] Cannot call api because Homeworld is unknown.");
			return 0;
		}

		uri = $"{endpoint}/aggregated/{worldname}/{itemId}";
		try {
			ComfyLoot.Log.Verbose(
				"[Universalis] Attemting to get data for ItemId: {itemId} ({wordname})",
				itemId,
				worldname);
			data = await HttpHelper.GetAsync<MarketBoardData>(uri);
		} catch (Exception ex) {
			ComfyLoot.Log.Error(
				ex,
				"[Universalis] Cannot recieve data for ItemId: {itemId}.",
				itemId);
			return 0;
		}

		if (data == null
		|| data.Results == null
		|| data.Results.Count == 0) {
			ComfyLoot.Log.Error("[Universalis] Failed to retrieve data: Invalid response");
			return 0;
		}

		return GetMarketValue(data, hq);
	}

	/// <summary>
	/// Extracts Itemvalue from Unveralis response
	/// </summary>
	/// <param name="data">Universalis response</param>
	/// <param name="hq">HQ item or not</param>
	/// <returns>Itemvalue in gil</returns>
	private static int
	GetMarketValue(MarketBoardData data, bool hq)
	{
		double price;
		AggregatedResult? result;
		QualityData? qualityData;

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

		price = 0;
		if (qualityData.MinListing != null
		&& qualityData.MinListing.World != null)
			price = qualityData.MinListing.World.Price;

		ComfyLoot.Log.Debug(
			"[Universalis] ItemId: {itemId} Value: {price}",
			result.ItemId,
			price);

		return (int)price;
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

		itemValue = await GetItemGilValue(id, hq);
		item = new LootItem(
			id,
			Util.GetRarity(id),
			quantity,
			itemValue
		);

		lock (lootLock) {
			/* HACK: prevent duplicates in the same zone */
			if (CheckDuplicate(zoneName, id)) {
				UpdateItem(id, quantity, zoneName);
				return;
			}

			if (!loot.TryGetValue(zoneName, out list))
				list = new List<LootItem>();

			list.Add(item);
			loot[zoneName] = list;
		}

		plugin.UpdateDtrBar();
		ComfyLoot.Log.Information(
			"[TRACK] {Quantity}x {ItemId} in {Zone}",
			quantity,
			id,
			zoneName);
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
		List<string> zones;

		lock (lootLock) {
			zones = new List<string>(loot.Keys);
			foreach (string zone in zones)
				totalValue += GetZoneItemValue(zone);

			return totalValue;
		}
	}

	/// <summary>
	/// Counts items across all zones.
	/// </summary>
	/// <returns>Total number of non-currency items gathered</returns>
	public int
	GetTotalItemQuantity()
	{
		int totalQuantity = 0;
		List<string> zones;

		lock (lootLock) {
			zones = new List<string>(loot.Keys);
			foreach (string zone in zones)
				totalQuantity += GetZoneItemQuantity(zone);

			return totalQuantity;
		}
	}

	/// <summary>
	/// Counts the total quantity of valid (non-currency) items within a single zone.
	/// </summary>
	/// <param name="zoneItems">The list of items in the zone</param>
	/// <returns>Total number of non-currency items in this zone</returns>
	public int
	GetZoneItemQuantity(string zone)
	{
		int zoneTotal = 0;
		List<LootItem>? items;

		lock (lootLock) {
			if (loot == null
			|| string.IsNullOrEmpty(zone))
				return 0;

			if (!loot.TryGetValue(zone, out items))
				return 0;

			if (items == null)
				return 0;

			foreach (LootItem item in items) {
				if (Util.IsCurrency(item.ItemId))
					continue;
				zoneTotal += item.Quantity;
			}

			return zoneTotal;
		}
	}

	/// <summary>
	/// Counts the total quantity of valid (non-currency) items within a single zone.
	/// </summary>
	/// <param name="Loot">Lootmanager Instance</param>
	/// <param name="zone">zonename</param>
	/// <returns>Total number of non-currency items in this zone</returns>
	public int
	GetZoneItemValue(string zone)
	{
		int zoneTotal = 0;
		List<LootItem>? items;

		lock (lootLock) {
			if (loot == null
			|| string.IsNullOrEmpty(zone))
				return 0;

			if (!loot.TryGetValue(zone, out items))
				return 0;

			if (items == null)
				return 0;

			foreach (LootItem item in items)
				zoneTotal += item.Value * item.Quantity;

			return zoneTotal;
		}
	}

	/// <summary>
	/// Updates an item's quantity.
	/// </summary>
	public void
	UpdateItem(uint itemId, int addedAmount, string zoneName)
	{
		LootItem? item;
		List<LootItem>? items;

		lock (lootLock) {
			if (!loot.TryGetValue(zoneName, out items))
				items = new List<LootItem>();

			item = null;
			foreach (LootItem entry in items) {
				if (entry.ItemId == itemId) {
					item = entry;
					break;
				}
			}

			if (item == null)
				return;

			items.Remove(item);
			items.Add(new LootItem(
				itemId,
				Util.GetRarity(itemId),
				item.Quantity + addedAmount,
				item.Value
			));

			loot[zoneName] = items;
		}

		plugin.UpdateDtrBar();
		ComfyLoot.Log.Information(
			"[TRACK] {ItemId} {Quantity}x in {Zone}",
			itemId,
			item.Quantity + addedAmount,
			zoneName);
	}

	public void
	Clear()
	{
		lock (lootLock) {
			loot.Clear();
		}
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