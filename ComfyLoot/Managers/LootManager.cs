/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComfyLoot.Models;
using Lumina.Excel.Sheets;

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

	public bool IsDisposed { get; private set; }

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
		IsDisposed = false;

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
	CheckIgnored(uint itemId)
	{
		foreach (uint id in plugin.Configuration.IgnoredItemIds)
			if (id == itemId)
				return true;

		return false;
	}

	/// <summary>
	/// Check if an Item already exists in a given zone
	/// </summary>
	/// <param name="zone">Zone to check</param>
	/// <param name="id">item id</param>
	/// <returns>true, if the item</returns>
	private bool
	CheckIgnored(string zonename)
	{
		foreach (string zone in plugin.Configuration.IgnoredZones)
			if (zone == zonename)
				return true;

		return false;
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
		int value;
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
		/* marketboard value (if eligible) */
		default:
			if (!config.UniversalisEnabled)
				return 0;

			worldname = plugin.HomeworldName;

			/* prevent unnessary api calls that will fail anyway */
			if (worldname == null
			|| worldname.Equals("Dev")
			|| worldname.Equals("???")
			|| !Util.IsTradable(itemId)) {
				return 0;
			}

			value = await Universalis.GetValue(
				itemId,
				worldname,
				hq);

			return value;
		}
	}

	/// <summary>
	/// Add or update an item in the droplist.
	/// </summary>
	public async Task 
	AddItem(uint id, int amount, string zoneName, bool hq)
	{
		int itemValue;
		int quantity;
		LootItem item;
		LootItem? existing;
		List<LootItem>? items;

		if(CheckIgnored(id)
		|| CheckIgnored(zoneName))
			return;

		itemValue = await GetItemGilValue(Util.GetBaseId(id), hq);
		item = new LootItem(
		    id,
		    Util.GetRarity(id),
		    amount,
		    itemValue
		);

		lock (lootLock) {
			if (!loot.TryGetValue(zoneName, out items))
				items = new List<LootItem>();

			existing = items.FirstOrDefault(x => x.ItemId == id);
			if (existing != null) {
				quantity = existing.Quantity + amount;

				items.Remove(existing);
				items.Add(new LootItem(
					id,
					Util.GetRarity(id),
					quantity,
					existing.Value
				));

				loot[zoneName] = items;

				plugin.UpdateDtrBar();
				ComfyLoot.Log.Information(
					"[TRACK] {Quantity}x {ItemId} in {Zone}",
					quantity,
					id,
					zoneName);
				return;
			}

			items.Add(item);
			loot[zoneName] = items;

			plugin.UpdateDtrBar();
			ComfyLoot.Log.Information(
				"[TRACK] {Quantity}x {ItemId} in {Zone}",
				amount,
				id,
				zoneName);
		}
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
		ComfyLoot.Log.Verbose("[LootManager] Disposing Service");

		/* Cleanup */
		Clear();

		IsDisposed = true;
	}
}