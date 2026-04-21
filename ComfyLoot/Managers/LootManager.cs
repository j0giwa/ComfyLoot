/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility;

using ComfyLoot.Models;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ComfyLoot.Test")] /* helps with testing */
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

	private readonly ComfyLoot? plugin; /* NOTE: nullable to help with testing  */
	private readonly Config config;
	private readonly Lock lootLock;
	private readonly Dictionary<string, List<LootItem>> loot;

	public bool IsDisposed { get; private set; }

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
<<<<<<< HEAD
	/// LootManager:ctor for testing
	/// </summary>
	/// <param name="config">Hotloaded Config</param>
	internal LootManager(Config config)
	{
		IsDisposed = false;
		plugin = null;
		this.config = config;
		loot = new Dictionary<string, List<LootItem>>();
		lootLock = new Lock();
	}

	/// <summary>
	/// LootManager:ctor for testing
	/// </summary>
	/// <param name="config">Hotloaded Config</param>
	/// <param name="loot">Hotloaded loot table</param>
	internal LootManager(Config config, Dictionary<string, List<LootItem>> loot)
	{
		IsDisposed = false;
		plugin = null;
		this.config = config;
		this.loot = loot;
		lootLock = new Lock();
	}

	/// <summary>
	/// Check if an Item already exists in a given zone
	/// </summary>
	/// <param name="zone">Zone to check</param>
	/// <param name="id">item id</param>
	/// <returns>true, if the item</returns>
	private bool
	CheckIgnoredItem(uint itemId)
	{
		foreach (uint id in config.IgnoredItemIds)
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
	CheckIgnoredZone(string zone)
	{
		foreach (string ignoredZone in config.IgnoredZones)
			if (zone == ignoredZone)
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
	/// 0, if none of the above applies
	/// </returns>
	private async Task<int>
	GetItemGilValue(uint itemId, bool hq)
	{
		int value;
		string? worldname;

		worldname = null;
		switch (itemId) {
		case (int)SpecialItems.GIL:
			return 1;
		case (int)SpecialItems.ALLAGAN_TIN_PIECE:
			return 25;
		case (int)SpecialItems.ALLAGAN_BRONZE_PIECE: /* FALLTHROUGH */
		case (int)SpecialItems.NIGHTWORLD_BRONZE_PIECE:
			return 100;
		case (int)SpecialItems.ALLAGAN_SILVER_PIECE: /* FALLTHROUGH */
		case (int)SpecialItems.NIGHTWORLD_SILVER_PIECE:
			return 500;
		case (int)SpecialItems.ALLAGAN_GOLD_PIECE:
			return 2500;
		case (int)SpecialItems.ALLAGAN_PLATINUM_PIECE:
			return 10000;
		default: /* marketboard value (if eligible) */
			if (!config.UniversalisEnabled)
				return 0;

			if (plugin != null)
				worldname = plugin.HomeworldName;

			/* prevent unnessary api calls that will fail anyway */
			if (worldname == null
			|| worldname.Equals("Dev")
			|| worldname.Equals("???")
			|| !Util.IsTradable(itemId)
			|| Util.IsCurrency(itemId))
				return 0;

			value = await Universalis.GetValue(
				itemId,
				worldname,
				hq);

			return value;
		}
	}

	/// <summary>
=======
>>>>>>> 226da65 (setup testing)
	/// Add or update an item in the droplist.
	/// </summary>
	/// <param name="id"> Item identifyer</summary>
	/// <param name="amount">Ammount gained</summary>
	/// <param name="zone">Zone identifyer (igoneored if zoneName is set)</summary>
	/// <param name="zoneName">Zonename override</summary>
	public async Task
	AddItem(uint id, int amount, uint zone, string zoneName = "")
	{
		int itemValue;
		int quantity;
		string name;
		LootItem item;
		LootItem? existing;
		List<LootItem>? items;
		
		name = zoneName;
		if (name == "")
			name = Util.GetZoneName(zone);

		if (CheckIgnoredItem(id)
		|| CheckIgnoredZone(name))
			return;

		itemValue = await GetItemGilValue(
			Util.GetItemBaseId(id),
			ItemUtil.IsHighQuality(id)
		);
		item = new LootItem(
		    id,
		    Util.GetRarity(id),
		    amount,
		    itemValue
		);

		lock (lootLock) {
			if (!loot.TryGetValue(name, out items))
				items = new List<LootItem>();

			existing = items.Find(x => x.ItemId == id);

			if (existing != null) {
				quantity = existing.Quantity + amount;

				items.Remove(existing);
				items.Add(new LootItem(
					id,
					Util.GetRarity(id),
					quantity,
					existing.Value
				));

				loot[name] = items;

				plugin?.UpdateDtrBar();
				ComfyLoot.Log.Information(
					"[TRACK] {Quantity}x {ItemId} in zone: {zoneName} ({zone})",
					quantity,
					id,
					name,
					zone);
				return;
			}

			items.Add(item);
			loot[name] = items;

<<<<<<< HEAD
			if (!Config.STABLE)
				plugin?.UpdateDtrBar();
=======
			if (!Configuration.STABLE)
				plugin.UpdateDtrBar();

			ComfyLoot.Log.Information(
				"[TRACK] {Quantity}x {ItemId} in zone: {zoneName} ({zone})",
				amount,
				id,
				name,
				zone);
>>>>>>> 226da65 (setup testing)
		}
	}

	/// <summary>
	/// Check if an Item already exists in a given zone
	/// </summary>
	/// <param name="itemId">item id</param>
	/// <returns>true, if the item</returns>
	private bool
	CheckIgnoredItem(uint itemId)
	{
		foreach (uint id in plugin.Configuration.IgnoredItemIds)
			if (id == itemId)
				return true;

		return false;
	}

	/// <summary>
	/// Check if an Item already exists in a given zone
	/// </summary>
	/// <param name="zoneId">item id</param>
	/// <returns>true, if the item</returns>
	private bool
	CheckIgnoredZone(uint zoneId)
	{
		foreach (uint id in plugin.Configuration.IgnoredZoneIds)
			if (id == zoneId)
				return true;

		return false;
	}

	/// <summary>
	/// Resets the loot list
	/// </summary>
	public void
	Clear()
	{
		lock (lootLock) {
			loot.Clear();
		}
	}

	/// <summary>
	/// Resets the loot list for a given zone.
	/// </summary>
	public void
	ClearZone(string zone)
	{
		lock (lootLock) {
			loot.Remove(zone);
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
	/// 0, if none of the above applies
	/// </returns>
	private async Task<int>
	GetItemGilValue(uint itemId, bool hq)
	{
		int value;
		string worldname;

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
			if (worldname == null
			|| worldname.Equals("Dev")
			|| worldname.Equals("???")
			|| !Util.IsTradable(itemId)
			|| Util.IsCurrency(itemId))
				return 0;

			value = await Universalis.GetValue(
				itemId,
				worldname,
				hq);

			return value;
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
			if (loot == null)
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
	/// <param name="zone">zonename</param>
	/// <returns>Total number of non-currency items in this zone</returns>
	public int
	GetZoneItemValue(string zone)
	{
		int zoneTotal = 0;
		List<LootItem>? items;

		lock (lootLock) {
			if (loot == null)
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