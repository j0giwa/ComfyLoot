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
	private readonly Lock seenLock;
	private readonly LootManager loot;

	private readonly List<InventoryEventArgs> eventBuffer;
	private readonly HashSet<uint> seenItems;

	private CancellationTokenSource debounceCts;

	/// <summary>
	/// InventoryWatcher:ctor
	/// </summary>
	public InventoryWatcher(LootManager loot)
	{
		debounceLock = new Lock();
		seenLock = new Lock();
		this.loot = loot;

		eventBuffer = new List<InventoryEventArgs>();
		seenItems = new HashSet<uint>();
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
		const int delay = 1000; /* adjust if needed, delay might depend on client */

		await Task.Delay(delay);

		if (ComfyLoot.ClientState.IsLoggedIn)
			ComfyLoot.GameInventory.InventoryChanged += OnInventoryChanged;

		ComfyLoot.Log.Debug("[Inventory] InventoryChanged-event registered");
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

		switch (args.Inventory) {
		case GameInventoryType.Inventory1:
		case GameInventoryType.Inventory2:
		case GameInventoryType.Inventory3:
		case GameInventoryType.Inventory4:
		case GameInventoryType.Crystals:
		case GameInventoryType.Currency:
			previousQty = args.OldItemState.Quantity;
			addedAmount = args.Item.Quantity - previousQty;

			if (addedAmount > 0) {
				ComfyLoot.Log.Debug(
					"[CHANGE] {Quantity}x {ItemId} in {Inventory} (slot {Slot})",
					addedAmount,
					args.Item.ItemId,
					args.Inventory,
					args.Slot);

				/* HACK: force add if the item is in the inventory,
				   but not in the lootlist */
				lock (seenLock) {
					if (!seenItems.Contains(args.Item.ItemId)) {
						seenItems.Add(args.Item.ItemId);
						_ = Task.Run(() => loot.AddItem(
							args.Item.ItemId,
							addedAmount,
							zone,
							args.Item.IsHq
						));
						return;
					}
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
		lock (debounceLock) {
			eventBuffer.AddRange(events);
			debounceCts?.Cancel();
			debounceCts = new CancellationTokenSource();
			_ = Task.Run(() => DebounceEvents(debounceCts.Token));
		}
	}

	/// <summary>
	/// Queues the event buffer and returns a queue of events.
	/// </summary>
	/// <param name="token"></param>
	/// <returns>A queue containing the queued events.</returns>
	private async Task DebounceEvents(CancellationToken token)
	{
		const int delay = 100;
		Queue<InventoryEventArgs> eventsToProcess;

		try {
			await Task.Delay(delay, token);
		} catch (TaskCanceledException) {
			return;
		}

		lock (debounceLock) {
			eventsToProcess = new Queue<InventoryEventArgs>(eventBuffer);
			eventBuffer.Clear();
		}

		ProcessEvents(eventsToProcess);
	}

	private void 
	ProcessEvents(Queue<InventoryEventArgs> events)
	{
		int eventnumber;
		int totalEvents;
		string zone;

		totalEvents = events.Count;
		zone = Util.GetCurrentZoneName();

		ComfyLoot.Log.Verbose("[Inventory] processing {count} InventoryChanged-event(s) in {zone}",
			totalEvents,
			zone
		);

		eventnumber = 1;
		while (events.Count > 0) {
			var evt = events.Dequeue();

			ComfyLoot.Log.Verbose("[Inventory] processing event {number} ({type}) off {total}",
				eventnumber,
				evt.Type,
				totalEvents
			);

			switch (evt) {
			case InventoryItemAddedArgs added:
				HandleAddItem(added, zone);
				break;
			case InventoryItemChangedArgs changed:
				HandleChangeItem(changed, zone);
				break;
			}
			eventnumber++;
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