
using System;
using System.Collections.Generic;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;

namespace ComfyLoot.Servive;

public sealed class InventoryWatcher : IDisposable
{
	private readonly IGameInventory _inventory;
	private readonly IPluginLog _log;

	public InventoryWatcher(IGameInventory inventory, IPluginLog log)
	{
		_inventory = inventory;
		_log = log;

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
			break;
		default:
			break;
		}
	}

	private void
	HandleChangeItem(InventoryItemChangedArgs args)
	{
		switch (args.Inventory) {
		case GameInventoryType.Inventory1: /* FALLTHROUGH */
		case GameInventoryType.Inventory2:
		case GameInventoryType.Inventory3:
		case GameInventoryType.Inventory4:
		case GameInventoryType.Crystals:
		case GameInventoryType.Currency:
			_log.Information(
				$"[CHANGE] {args.Item.ItemId} x{args.Item.Quantity} " +
				$"in {args.Inventory} (slot {args.Slot})");
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