
using System;
using System.Collections.Generic;
using Dalamud.Game.Inrentory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;
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

	private readonly IClientState client;
	private readonly IDataManager data;
	private readonly IPluginLog log;

	/// <summary>
	/// Droplist,
	/// contains everything the player collected
	/// </summary>
	private readonly Dictionary<string, List<LootItem>> loot;
	public IReadOnlyDictionary<string, List<LootItem>> Loot => loot;

	/// <summary>
	/// LootManager:ctor
	/// </summary>
	/// <param name="clientState"></param>
	/// <param name="dataManager"></param>
	/// <param name="log"></param>
	public LootManager(
		IClientState clientState,
		IDataManager dataManager,
		IPluginLog log)
	{
		this.log = log;
		client = clientState;
		data = dataManager;

		loot = new Dictionary<string, List<LootItem>>();
	}

	/// <summary>
	/// Add an Item to the droplist
	/// </summary>
	/// <param name="added">Added item</param>
	public void
	AddItem(InventoryItemAddedArgs added)
	{
		string zone;
		List<LootItem>? list;

		zone = GetCurrentZoneName();

		LootItem tracked = new LootItem(
		    added.Item.ItemId,
		    added.Item.Quantity,
		    GetItemValue(added.Item.ItemId),
		    //0 /* placeholder till we got universalis value*/
		);

		if (!loot.TryGetValue(zone, out list)) {
			list = new List<LootItem>();
			loot[zone] = list;
		}

		list.Add(tracked);
		log.Information(
			"[TRACK] {Quantity}x {ItemId} in {Zone}",
			tracked.Quantity,
			tracked.ItemId,
			zone);
	}

	/// <summary>
	/// Gets the name of the current zone.
	/// Aka: Where is the player right now?
	/// </summary>
	/// <returns>Name of the current zone,</returns>
	private string
	GetCurrentZoneName()
	{
		uint id;
		string name;
		ExcelSheet<TerritoryType> sheet;
		TerritoryType zoneRow;

		id = client.TerritoryType;
		name = "Unknown Zone"; /* In case for (unlikely) failures */

		sheet = data.GetExcelSheet<TerritoryType>();
		if (sheet != null) {
			zoneRow = sheet.GetRow(id);

			if (zoneRow != null
			&& zoneRow.PlaceName != null
			&& zoneRow.PlaceName.Value != null)
				name = zoneRow.PlaceName.Value.Name.ToString();
		}

		return name;
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
	GetItemValue(uint itemId)
	{
		int value;

		switch (itemId) {
		case (int)Currencys.GIL:
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
		case default:
			value = 1; /* TODO: Get universalis data */
			/* TODO: return 0 when not not sellable on MB */
			break;
		}

		return value;
	}

	/// <summary>
	/// Counts items across all zone
	/// </summary>
	/// <returns>Total number of Items gatherd</returns>
	public int
	GetTotalItemQuantity()
	{
		int totalQuantity = 0;
		foreach (List<LootItem> zoneList in loot.Values) {
			foreach (LootItem tracked in zoneList) {
				switch (tracked.ItemId) {
				case (int)Currencys.GIL:
				case (int)Currencys.STORM_SEAL:
				case (int)Currencys.SERPENT_SEAL:
				case (int)Currencys.FLAME_SEAL:
				case (int)Currencys.ALLIED_SEALS:
				case (int)Currencys.WOLF_MARKS:
				case (int)Currencys.MGP:
				case (int)Currencys.TROPHY_CRYSTALS:
				case (int)Currencys.TOMESTONE_POETICS:
				case (int)Currencys.TOMESTONE_AESTETICS:
				case (int)Currencys.TOMESTONE_MATHEMATICS:
				case (int)Currencys.TOMESTONE_HELIOMETRY:
				case (int)Currencys.CENTURIO_SEALS:
				case (int)Currencys.SACK_OF_NUTS:
				case (int)Currencys.BICOLOR_GEMSTONES:
				case (int)Currencys.WHITE_CRAFTER_SCRIPS:
				case (int)Currencys.PURPLE_CRAFTER_SCRIPS:
				case (int)Currencys.ORANGE_CRAFTER_SCRIPS:
				case (int)Currencys.WHITE_GATHERER_SCRIPS:
				case (int)Currencys.PURPLE_GATHERER_SCRIPS:
				case (int)Currencys.ORANGE_GATHERER_SCRIPS:
				case (int)Currencys.SKYBUILDER_SCRIPS:
					continue; /* Currencys are not items */
				default:
					totalQuantity += tracked.Quantity;
					break;
				}
			}
		}
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
		if (!loot.TryGetValue(zoneName, out items)) {
			items = new List<LootItem>();
			loot[zoneName] = items;
		}

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
	}

	public void
	Dispose()
	{}
}