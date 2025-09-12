
using System;
using System.Collections.Generic;
using ComfyLoot.Data;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace ComfyLoot.Service;

public record TrackedItem(uint ItemId, int Quantity);

public class LootManager : IDisposable
{
	public Dictionary<string, List<string>> Loot { get; set; }
	public IReadOnlyDictionary<string, List<TrackedItem>> ItemsByZone => itemsByZone;

	private readonly IClientState clientState;
	private readonly IDataManager dataManager;
	private readonly IPluginLog log;

	private readonly Dictionary<string, List<TrackedItem>> itemsByZone = new();

	public LootManager(IClientState clientState, IDataManager dataManager, IPluginLog log)
	{
		this.clientState = clientState;
		this.dataManager = dataManager;
		this.log = log;
	}

	private string
	GetCurrentZoneName()
	{
		uint territoryId = clientState.TerritoryType;
		if (dataManager.GetExcelSheet<TerritoryType>()!.TryGetRow(territoryId, out TerritoryType territoryRow)) {
			return territoryRow.PlaceName.Value.Name.ToString() ?? "Unknown Zone";
		} else {
			return "Invalid territory";
		}
	}

	public void
	AddItem(InventoryItemAddedArgs added)
	{
		string zoneName = GetCurrentZoneName();

		TrackedItem tracked = new TrackedItem(
		    added.Item.ItemId,
		    added.Item.Quantity
		);

		if (!itemsByZone.TryGetValue(zoneName, out List<TrackedItem>? list)) {
			list = new List<TrackedItem>();
			itemsByZone[zoneName] = list;
		}

		list.Add(tracked);

		log.Information("[TRACK] {Quantity}x {ItemId} in {Zone}", tracked.Quantity, tracked.ItemId, zoneName);
	}

	/// <summary>
	/// Updates an item's quantity if the new quantity is higher than the currently tracked one.
	/// </summary>
	public void
	UpdateItem(uint itemId, int addedAmount)
	{
		string zoneName = GetCurrentZoneName();

		if (!itemsByZone.TryGetValue(zoneName, out List<TrackedItem>? list)) {
			list = new List<TrackedItem>();
			itemsByZone[zoneName] = list;
		}

		TrackedItem? existing = list.Find(t => t.ItemId == itemId);
		if (existing != null) {
			list.Remove(existing);
			list.Add(new TrackedItem(itemId, existing.Quantity + addedAmount));
			log.Information("[TRACK] {ItemId} x{Quantity} in {Zone} (previous {PreviousQuantity})",
			    addedAmount, itemId, zoneName, existing.Quantity);

		} else {
			list.Add(new TrackedItem(itemId, addedAmount));
			log.Information("[TRACK] {ItemId} x{Quantity} in {Zone} (new item)", addedAmount, itemId, zoneName);
		}
	}

	/// <summary>
	/// Returns the total quantity of a tracked item across all zones.
	/// </summary>
	public int
	GetItemQuantity(uint itemId)
	{
		int total = 0;
		foreach (var list in itemsByZone.Values)
		{
			foreach (TrackedItem tracked in list)
			{
				if (tracked.ItemId == itemId)
					total += tracked.Quantity;
			}
		}
		return total;
	}

	/// <summary>
	/// Counts items across all zone
	/// </summary>
	/// <returns>Total number of Items gatherd</returns>
	public int
	GetTotalItemQuantity()
	{
		int totalQuantity = 0;
		/* TODO: remove var bullshit */
		foreach (var zoneList in itemsByZone.Values) {
			foreach (var tracked in zoneList) {
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

	public void
	Dispose()
	{
		throw new NotImplementedException();
	}
}