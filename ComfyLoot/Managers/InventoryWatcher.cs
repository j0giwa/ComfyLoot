
using System;
using System.Collections.Generic;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;
using ComfyLoot.Service;

namespace ComfyLoot.Servive;

public class InventoryWatcher : IDisposable
{
	private readonly IGameInventory _inventory;
	private readonly IPluginLog _log;
	private readonly LootManager _loot;

	public InventoryWatcher(IGameInventory inventory, IPluginLog log, LootManager loot)
	{
		_inventory = inventory;
		_log = log;
		_loot = loot;

		_inventory.InventoryChanged += OnInventoryChanged;
	}

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
				$"[ADD] {args.Item.ItemId} x{args.Item.Quantity} " +
				$"in {args.Inventory} (slot {args.Slot})");
			_loot.AddItem(args);
			break;
		default:
			break;
		}
	}

	private void
	HandleChangeItem(InventoryItemChangedArgs args)
	{
		switch (args.Inventory){
		case GameInventoryType.Inventory1:
		case GameInventoryType.Inventory2:
		case GameInventoryType.Inventory3:
		case GameInventoryType.Inventory4:
		case GameInventoryType.Crystals:
		case GameInventoryType.Currency:
			{
				int previousQty = args.OldItemState.Quantity;
				int addedAmount = args.Item.Quantity - previousQty;
				if (args.Item.Quantity > previousQty) {
					_log.Information(
						$"[CHANGE] {args.Item.ItemId} x{addedAmount} in {args.Inventory} (slot {args.Slot})");
					_loot.UpdateItem(args.Item.ItemId, addedAmount);
				}
			}
			break;
		default:
			break;
		}
	}

	private void
	OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
	{
		foreach (var evt in events) {
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
	}

	public void
	Dispose()
	{
		throw new NotImplementedException();
	}
}	