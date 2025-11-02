/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;

namespace ComfyLoot.Managers;

/// <summary>
/// Monitors player inventory
/// </summary>
public class InventoryWatcher : IDisposable {

	private readonly LootManager loot;
	private readonly HashSet<(uint itemId, GameInventoryType inventory, uint slot)> seenItems;

	/// <summary>
	/// InventoryWatcher:ctor
	/// </summary>
	public InventoryWatcher(LootManager loot)
	{
		this.loot = loot;
		seenItems = new HashSet<(uint itemId, GameInventoryType inventory, uint slot)>();
		_ = DelayedSubscribe();
	}

	private async Task 
	DelayedSubscribe()
	{
		const long delay = 5;

		await Task.Delay(TimeSpan.FromSeconds(delay));

		if (ComfyLoot.ClientState.IsLoggedIn)
			ComfyLoot.GameInventory.InventoryChanged += OnInventoryChanged;
	}

	/// <summary>
	/// Hande add item event
	/// </summary>
	private void
	HandleAddItem(InventoryItemAddedArgs args)
	{
		switch (args.Inventory) {
		case GameInventoryType.Inventory1: /* FALLTHROUGH */
		case GameInventoryType.Inventory2:
		case GameInventoryType.Inventory3:
		case GameInventoryType.Inventory4:
		case GameInventoryType.Crystals:
		case GameInventoryType.Currency:
			ComfyLoot.Log.Debug(
				"[ADD] {Quantity}x {ItemId} in {Inventory} (slot {Slot})",
				args.Item.Quantity,
				args.Item.ItemId,
				args.Inventory,
				args.Slot);
			_ = Task.Run(() => loot.AddItem(args));
			break;
		default:
			break;
		}
	}

	/// <summary>
	/// Hande change item event
	/// </summary>
	private void
	HandleChangeItem(InventoryItemChangedArgs args)
	{
		int previousQty;
		int addedAmount;
		(uint ItemId, GameInventoryType Inventory, uint Slot) key;

		switch (args.Inventory) {
		case GameInventoryType.Inventory1:
		case GameInventoryType.Inventory2:
		case GameInventoryType.Inventory3:
		case GameInventoryType.Inventory4:
		case GameInventoryType.Crystals:
		case GameInventoryType.Currency:
			previousQty = args.OldItemState.Quantity;
			addedAmount = args.Item.Quantity - previousQty;
			key = (args.Item.ItemId, args.Inventory, args.Slot);

			if (addedAmount > 0) {
				ComfyLoot.Log.Debug(
					"[CHANGE] {Quantity}x {ItemId} in {Inventory} (slot {Slot})",
					addedAmount,
					args.Item.ItemId,
					args.Inventory,
					args.Slot);

				/* First time seeing this item
				 * set as "baseline", not an actual change */
				if (!seenItems.Contains(key)) {
					seenItems.Add(key);
					_ = Task.Run(() => loot.AddItem(
						args.Item.ItemId,
						addedAmount,
						Util.GetCurrentZoneName(),
						args.Item.IsHq
					));
					return;
				}
				loot.UpdateItem(args.Item.ItemId, addedAmount);
			}
			break;
		default:
			break;
		}
	}

	/// <summary>
	/// Hande iventory change event
	/// </summary>
	private void
	OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
	{
		foreach (InventoryEventArgs evt in events)
			switch (evt) {
			case InventoryItemAddedArgs added:
				HandleAddItem(added);
				break;
			case InventoryItemChangedArgs changed:
				HandleChangeItem(changed);
				break;
			default:
				break;
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
