/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;

namespace ComfyLoot.Managers;

/// <summary>
/// Monitors player inventory
/// </summary>
public class InventoryWatcher : IDisposable {

	private readonly Lock debounceLock;
	private readonly LootManager loot;
	private readonly List<InventoryEventArgs> eventBuffer;
	/* rather cumbersome, but i helps prevent multiple anomalys */
	private readonly HashSet<(uint itemId, GameInventoryType inventory, uint slot)> seenItems;
	private CancellationTokenSource debounceCts;

	/// <summary>
	/// InventoryWatcher:ctor
	/// </summary>
	public InventoryWatcher(LootManager loot)
	{
		this.loot = loot;
		seenItems = new HashSet<(uint itemId, GameInventoryType inventory, uint slot)>();
		debounceLock = new Lock();
		eventBuffer = new List<InventoryEventArgs>();
		debounceCts = null;
		_ = DelayedSubscribe(); /* HACK: delay prevents issues with serverhoppin/logon */
		loot.Clear(); /* HACK: forcefully reset after login*/
	}

	/// <summary>
	/// Delays subcription to events
	/// </summary>
	private async Task
	DelayedSubscribe()
	{
		const int delay = 1;

		await Task.Delay(TimeSpan.FromSeconds(delay));

		if (ComfyLoot.ClientState.IsLoggedIn)
			ComfyLoot.GameInventory.InventoryChanged += OnInventoryChanged;

		ComfyLoot.Log.Debug("[Inventory] event registered");
	}

	/// <summary>
	/// Hande add item event
	/// </summary>
	private void
	HandleAddItem(InventoryItemAddedArgs args, string zone)
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
			_ = Task.Run(() => loot.AddItem(
				args.Item.ItemId,
				args.Item.Quantity,
				zone,
				args.Item.IsHq
			));
			break;
		default:
			break;
		}
	}

	/// <summary>
	/// Hande change item event
	/// </summary>
	private void
	HandleChangeItem(InventoryItemChangedArgs args, string zone)
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

				/* HACK: force add if the item is in the inventory, 
				   but not in the lootlist */
				if (!seenItems.Contains(key)) {
					seenItems.Add(key);
					_ = Task.Run(() => loot.AddItem(
						args.Item.ItemId,
						addedAmount,
						zone,
						args.Item.IsHq
					));
					return;
				}

				loot.UpdateItem(
					args.Item.ItemId,
					addedAmount,
					zone
				);
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
		ComfyLoot.Log.Debug("[Inventory] event triggered");
		lock (debounceLock) {
			eventBuffer.AddRange(events);
			debounceCts?.Cancel();
			debounceCts = new CancellationTokenSource();
			_ = QueueEvents(debounceCts.Token);
		}
	}

	/// <summary>
	/// Queues the eventbuffer
	/// </summary>
	/// <param name="token"></param>
	/// <returns></returns>
	private async Task
	QueueEvents(CancellationToken token)
	{
		const int delay = 100;
		List<InventoryEventArgs> events;

		try {
			await Task.Delay(delay, token);
		} catch (TaskCanceledException) {
			return;
		}

		lock (debounceLock) {
			events = new List<InventoryEventArgs>(eventBuffer);
			eventBuffer.Clear();
		}

		ProcessQueuedEvents(events);
	}

	private void 
	ProcessQueuedEvents(List<InventoryEventArgs> events)
	{
		string zone;

		zone = Util.GetCurrentZoneName();

		foreach (var evt in events)
			switch (evt) {
			case InventoryItemAddedArgs added:
				HandleAddItem(added, zone);
				break;
			case InventoryItemChangedArgs changed:
				HandleChangeItem(changed, zone);
				break;
			default:
				break;
			}
	}

	public void
	Dispose()
	{
		debounceCts.Dispose();
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void
	Dispose(bool disposing)
	{
		/* Cleanup */
	}
}
