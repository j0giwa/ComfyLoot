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
public class InventoryWatcher : IDisposable
{

	private readonly IPluginLog _log;
	private readonly LootManager _loot;
	private readonly HashSet<(uint itemId, GameInventoryType inventory, uint slot)> _seenItems;

	/// <summary>
	/// InventoryWatcher:ctor
	/// </summary>
	public InventoryWatcher(
		LootManager loot,
		IPluginLog log)
	{
		_log = log;
		_loot = loot;
		_seenItems = new HashSet<(uint itemId, GameInventoryType inventory, uint slot)>();

		ComfyLoot.GameInventory.InventoryChanged += OnInventoryChanged;
	}

	/// <summary>
	/// Hande add item event
	/// </summary>
	private void
	HandleAddItem(InventoryItemAddedArgs args)
	{
		switch (args.Inventory){
		case GameInventoryType.Inventory1: /* FALLTHROUGH */
		case GameInventoryType.Inventory2:
		case GameInventoryType.Inventory3:
		case GameInventoryType.Inventory4:
		case GameInventoryType.Crystals:
		case GameInventoryType.Currency:
			_log.Information(
				"[ADD] {Quantity}x {ItemId} in {Inventory} (slot {Slot})",
				args.Item.Quantity,
				args.Item.ItemId,
				args.Inventory,
				args.Slot);
			_ = Task.Run(() => _loot.AddItem(args));
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
			key = (args.Item.ItemId, args.Inventory, args.Slot);
			/* First time seeing this item — treat as "baseline", not an actual change */
			if (!_seenItems.Contains(key)) {
				_seenItems.Add(key);
				return; /* Ignore first update */
			}
			addedAmount = args.Item.Quantity - previousQty;
			if (args.Item.Quantity > 0) {
				_log.Information(
					"[CHANGE] {Quantity}x {ItemId} in {Inventory} (slot {Slot})",
					addedAmount,
					args.Item.ItemId,
					args.Inventory,
					args.Slot);
				_loot.UpdateItem(args.Item.ItemId, addedAmount);
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
		foreach (var evt in events)
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
		// Cleanup
	}
}
