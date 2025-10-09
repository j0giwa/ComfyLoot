
using System;
using System.Collections.Generic;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

using ComfyLoot.Data;

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

	private readonly IPluginLog log;
	private readonly Dictionary<string, List<LootItem>> loot;

	/// <summary>
	/// Droplist, contains everything the player collected
	/// </summary>
	public IReadOnlyDictionary<string, List<LootItem>> Loot => loot;

	/// <summary>
	/// LootManager:ctor
	/// </summary>
	/// <param name="log"></param>
	public LootManager(IPluginLog log)
	{
		this.log = log;
		//data = dataManager;

		loot = new Dictionary<string, List<LootItem>>();
	}

	/// <summary>
	/// Gets the gil value of the given item 
	/// </summary>
	/// <param name="itemId"></param>
	/// <returns>
	/// <list type="bullet">
	/// <item>Item count, if the item is gil</item>
	/// <item>Vendor value, if the item is meant to be sold to vendors</item>
	/// <item>Univesalis value, if the item can be sold on the marketboard</item>
	/// <item>0, if none of the above applies</item>
	/// </list>
	/// </returns>
	private static int
	GetItemValue(uint itemId)
	{
		int value;

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
			value = 1; /* TODO: Get universalis data */
			/* TODO: return 0 when not not sellable on MB */
			break;
		}

		return value;
	}

	/// <summary>
	/// Counts the total quantity of valid (non-currency) items within a single zone.
	/// </summary>
	/// <param name="zoneItems">The list of items in the zone.</param>
	/// <returns>Total number of non-currency items in this zone.</returns>
	private static int
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
	/// Gets the name of the current zone.
	/// Aka: Where is the player right now?
	/// </summary>
	/// <returns>Name of the current zone</returns>
	private string
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
	/// <param name="addedItem"></param>
	public void
	AddItem(InventoryItemAddedArgs addedItem)
	{
		uint id;
		int quantity;
		string zone;
		List<LootItem>? list;

		id = addedItem.Item.ItemId;
		quantity = addedItem.Item.Quantity;
		zone = GetCurrentZoneName();

		LootItem item = new LootItem(
			id,
		    	quantity,
		    	GetItemValue(id)
		);

		if (!loot.TryGetValue(zone, out list))
			list = new List<LootItem>();

		list.Add(item);
		log.Information(
			"[TRACK] {Quantity}x {ItemId} in {Zone}",
			quantity,
			id,
			zone);

		loot[zone] = list;
	}

	/// <summary>
	/// Returns the total quantity of a tracked item across all zones.
	/// </summary>
	public int
	GetItemQuantity(uint itemId)
	{
		int total = 0;
		foreach (List<LootItem> list in loot.Values)
			foreach (LootItem tracked in list)
				if (tracked.ItemId == itemId)
					total += tracked.Quantity;
		return total;
	}

	public int
	GetZoneItemValue(IEnumerable<LootItem> zoneItems)
	{
		int zoneTotal = 0;

		foreach (LootItem item in zoneItems)
			zoneTotal += item.Value;

		return zoneTotal;
	}

	public int
	GetTotalItemValue()
	{
		int totalQuantity = 0;

		foreach (List<LootItem> zoneList in loot.Values)
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

		foreach (List<LootItem> zoneList in loot.Values)
			totalQuantity += GetZoneItemQuantity(zoneList);

		return totalQuantity;
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
			log.Information(
				"[TRACK] {ItemId} x{Quantity} in {Zone} (previous {PreviousQuantity})",
				itemId,
				addedAmount,
				zoneName,
				item.Quantity
			);
		}

		loot[zoneName] = items;
	}

	public void
	Dispose()
	{ }
}