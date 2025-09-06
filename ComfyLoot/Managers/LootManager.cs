
using System;
using System.Collections.Generic;
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

	public int
	GetTotalItemQuantity()
	{
		int totalQuantity = 0;
		foreach (var zoneList in itemsByZone.Values)
		{
			foreach (var tracked in zoneList) {
				/* TODO: Evil magic numbers */
				switch (tracked.ItemId) {
					case 28:
					case 20:
					case 21:
					case 22:
					case 25:
					case 36656:
					case 27:
					case 10307:
					case 26533:
					case 26807:
					case 25199:
					case 25200:
            				case 33913:
            				case 33914:
            				case 28063:
						/* skip Currencys */
						continue;
					default:
						/* everything not a currency*/
						totalQuantity += tracked.Quantity;
						break;
				}
			}
		}
		return totalQuantity;
	}

	public void Dispose()
	{
		throw new NotImplementedException();
	}
}
