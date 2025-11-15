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

	private static bool 
	IsRelevantInventory(GameInventoryType type)
	{
		switch (type) {
		case GameInventoryType.Inventory1: /* FALLTHROUGH */
		case GameInventoryType.Inventory2:
		case GameInventoryType.Inventory3:
		case GameInventoryType.Inventory4:
		case GameInventoryType.Crystals:
		case GameInventoryType.Currency:
			return true;

		default:
			return false;
		}
	}

	/// <summary>
	/// Delays subcription to events
	/// </summary>
	private async Task
	DelayedSubscribe()
	{
		const int delay = 2000; /* adjust if needed, delay might depend on client */

		await Task.Delay(delay);

		if (ComfyLoot.ClientState.IsLoggedIn)
			ComfyLoot.GameInventory.InventoryChanged += OnInventoryChanged;

		ComfyLoot.Log.Debug("[InventoryWatcher] InventoryChanged-event registered");
	}

	/// <summary>
	/// Hande add item event
	/// </summary>
	private void
	HandleAddItem(InventoryItemAddedArgs args, string zone)
	{
		ComfyLoot.Log.Debug(
			"[InventoryWatcher] ADD {Quantity}x {ItemId} in {Inventory} (slot {Slot})",
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
	}

	/// <summary>
	/// Hande change item event
	/// </summary>
	private void
	HandleChangeItem(InventoryItemChangedArgs args, string zone)
	{
		int previousQty;
		int addedAmount;

		previousQty = args.OldItemState.Quantity;
		addedAmount = args.Item.Quantity - previousQty;
		if (addedAmount > 0) {
			ComfyLoot.Log.Debug(
				"[InventoryWatcher] CHANGE {Quantity}x {ItemId} in {Inventory} (slot {Slot})",
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
	}

	/// <summary>
	/// Handle inventory change event
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
	private async Task 
	DebounceEvents(CancellationToken token)
	{
		const int delay = 250; /* adjust if needed, gatherers boon might break it */

		Queue<InventoryEventArgs> rawEventQueue;
		Queue<InventoryEventArgs> eventQueue;

		try {
			await Task.Delay(delay, token);
		} catch (TaskCanceledException) {
			return;
		}

		lock (debounceLock) {
			rawEventQueue = new Queue<InventoryEventArgs>(eventBuffer);
			eventBuffer.Clear();
		}

		/* clean up noise from irrelevant Inventorys */
		eventQueue = new Queue<InventoryEventArgs>();
		foreach (var evt in rawEventQueue) {

			if (!IsRelevantInventory(evt.Item.ContainerType))
				continue;

			eventQueue.Enqueue(evt);
		}

		if (eventQueue.Count == 0)
			return;

		ProcessEvents(eventQueue);
	}

	private void 
	ProcessEvents(Queue<InventoryEventArgs> events)
	{
		int eventnumber;
		int totalEvents;
		string zone;
		InventoryEventArgs evt;

		totalEvents = events.Count;
		zone = Util.GetCurrentZoneName();

		ComfyLoot.Log.Verbose("[InventoryWatcher] processing {count} InventoryEvent(s) in {zone}",
			totalEvents,
			zone
		);
		
		eventnumber = 1;
		while (events.Count > 0) {
			evt = events.Dequeue();

			ComfyLoot.Log.Verbose("[InventoryWatcher] event {number}/{total}: ({type}) Item: {item} ",
					eventnumber,
					totalEvents,
					evt.Type,
					evt.Item.ToString()
				);

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
			eventnumber++;
		}

		ComfyLoot.Log.Verbose("[InventoryWatcher] proccessed {count} InventoryEvent(s) in {zone}",
			totalEvents,
			zone
		);
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