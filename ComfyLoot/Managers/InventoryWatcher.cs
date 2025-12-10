/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ComfyLoot.Models;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;

namespace ComfyLoot.Managers;

/// <summary>
/// Monitors player inventory
/// </summary>
public class InventoryWatcher : IDisposable {

	public bool IsDisposed { get; private set; }

	private readonly Lock debounceLock;
	private readonly Lock seenLock;
	private readonly ComfyLoot plugin;
	private readonly LootManager loot;

	private readonly List<InventoryEventArgs> eventBuffer;
	private readonly HashSet<uint> seenItems;

	private CancellationTokenSource? debounceCts;

	/// <summary>
	/// InventoryWatcher:ctor
	/// </summary>
	public InventoryWatcher(ComfyLoot plugin, LootManager loot)
	{
		IsDisposed = false;

		debounceLock = new Lock();
		seenLock = new Lock();
		this.plugin = plugin;
		this.loot = loot;

		eventBuffer = new List<InventoryEventArgs>();
		seenItems = new HashSet<uint>();
		debounceCts = null;

		_ = DelayedSubscribe(); /* HACK: delay prevents issues with serverhoppin/logon */
		loot.Clear(); /* HACK: forcefully reset after login*/
	}

	/// <summary>
	/// Queues the event buffer and returns a queue of events.
	/// </summary>
	/// <param name="token"></param>
	/// <returns>A queue containing the queued events.</returns>
	private async Task
	DebounceEvents(CancellationToken token, uint zone, string? name)
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
		foreach (InventoryEventArgs evt in rawEventQueue) {
			if (!IsRelevantInventory(evt.Item.ContainerType))
				continue;
			eventQueue.Enqueue(evt);
		}

		if (eventQueue.Count == 0)
			return;

		ProcessEvents(eventQueue, zone, name);
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
	/// Checks if the Inventory is relevant for proccessing
	/// </summary>
	/// <param name="type">Inventory to check</param>
	/// <returns>If the inventory is relevant, aka. if there is loot to expect</returns>
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
	/// Handle inventory change event
	/// </summary>
	private void
	OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
	{
		uint zone;
		string? overrideName;

		overrideName = null;
		zone = ComfyLoot.ClientState.TerritoryType;
		if (Util.IsTargetMarketboard()) {
			zone = 0;
			overrideName = "MarketBoard";
		}
			
		if (Util.IsTargetMail()) {
			zone = 0;
			overrideName = "Delivery";
		}

		if (plugin.TradeParterName != null) {
			zone = 0;
			overrideName = plugin.TradeParterName;
		}

		lock (debounceLock) {
			eventBuffer.AddRange(events);
			debounceCts?.Cancel();
			debounceCts = new CancellationTokenSource();
			_ = Task.Run(() => DebounceEvents(debounceCts.Token, zone, overrideName));
		}
	}

	/// <summary>
	/// Processes a queue of events
	/// </summary>
	/// <param name="events">events to process</param>
	/// <param name="zone">identifyer of the zone the events occured</param>
	private void
	ProcessEvents(Queue<InventoryEventArgs> events, uint zone, string? name)
	{
		int eventnumber;
		int totalEvents;
		InventoryEventArgs evt;

		totalEvents = events.Count;
		ComfyLoot.Log.Verbose("[InventoryWatcher] processing {count} InventoryEvent(s) in {zone}",
			totalEvents,
			zone
		);

		eventnumber = 1;
		while (events.Count > 0) {
			evt = events.Dequeue();

			if (evt.Type != GameInventoryEvent.Removed)
				ComfyLoot.Log.Debug("[InventoryWatcher] event {number}/{total}: ({type}) Item: {item} ",
					eventnumber,
					totalEvents,
					evt.Type,
					evt.Item.ToString()
				);

			if (evt.Type == GameInventoryEvent.Added
			|| evt.Type == GameInventoryEvent.Changed)
				ProccessEventItem(evt, zone, name);

			eventnumber++;
		}

		ComfyLoot.Log.Verbose("[InventoryWatcher] proccessed {count} InventoryEvent(s) in {zone}",
			totalEvents,
			zone
		);
	}

	/// <summary>
	/// Handle inventory item events (add or change)
	/// </summary>
	private void
	ProccessEventItem(object argsObj, uint zone, string? name)
	{
		int quantity;
		int addedAmount;

		if (name == null)
			name = "";

		switch (argsObj) {
		case InventoryItemAddedArgs addedArgs:
			quantity = addedArgs.Item.Quantity;

			ComfyLoot.Log.Debug(
				"[InventoryWatcher] ADD {Quantity}x {ItemId} in {Inventory} (slot {Slot})",
				quantity,
				addedArgs.Item.ItemId,
				addedArgs.Inventory,
				addedArgs.Slot);

			_ = Task.Run(() => loot.AddItem(
				addedArgs.Item.ItemId,
				quantity,
				zone,
				name
			));
			break;
		case InventoryItemChangedArgs changedArgs:
			quantity = changedArgs.OldItemState.Quantity;
			addedAmount = changedArgs.Item.Quantity - quantity;

			if (addedAmount <= 0)
				break;

			ComfyLoot.Log.Debug(
				"[InventoryWatcher] CHANGE {Quantity}x {ItemId} in {Inventory} (slot {Slot})",
				addedAmount,
				changedArgs.Item.ItemId,
				changedArgs.Inventory,
				changedArgs.Slot);

			lock (seenLock) {
				if (!seenItems.Contains(changedArgs.Item.ItemId))
					seenItems.Add(changedArgs.Item.ItemId);
			}

			_ = Task.Run(() => loot.AddItem(
				changedArgs.Item.ItemId,
				addedAmount,
				zone,
				name
			));
			break;
		default:
			ComfyLoot.Log.Warning(
				"[InventoryWatcher] Unknown event type: {Type}",
				argsObj.GetType());
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
		ComfyLoot.Log.Verbose("[InventoryWatcher] Disposing Events");

		/* Cleanup */
		debounceCts?.Dispose();
		ComfyLoot.GameInventory.InventoryChanged -= OnInventoryChanged;

		IsDisposed = true;
	}
}