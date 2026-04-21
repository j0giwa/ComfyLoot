/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

using ComfyLoot.Managers;

namespace ComfyLoot.Windows;

public class SortState {
	public int Column = 1;
	public bool Ascending = false;
}

/// <summary>
/// Mainplugin UI
/// </summary>
/* TODO: Need rework */
public class MainWindow : Window, IDisposable {

	private bool hideItems;
	private SortState? globalSort;

	private readonly ComfyLoot plugin;
	private readonly LootManager loot;
	private readonly List<uint> hidenItems;
	private readonly List<string> hidenZones;
	private readonly Dictionary<string, SortState> sortStates;

	/// <summary>
	/// MainWindow:ctor
	/// </summary>
	/// <param name="plugin">Reference to the parent <see cref="ComfyLoot"/> plugin.</param>
	/// <param name="loot">Reference the active loot manager instance.</param>
	public MainWindow(ComfyLoot plugin, LootManager loot)
		: base("Loottracker###comfyloot_ui", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(260, 330),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};

		TitleBarButtons = [
			new TitleBarButton() {
				Icon = FontAwesomeIcon.Cog,
				Click = (msg) => {
					if(this.plugin == null)
						return;
					this.plugin.ToggleConfigUI();
				},
				IconOffset = new(2,1),
				ShowTooltip = () => {
					ImGui.BeginTooltip();
					ImGui.Text("Open Settings");
					ImGui.EndTooltip();
				}
			},
			new TitleBarButton() {
				Icon = FontAwesomeIcon.Eye,
				Click = (msg) => {
					hideItems = !hideItems;
				},
				IconOffset = new(2,1),
				ShowTooltip = () => {
	    				ImGui.BeginTooltip();
					if (hideItems)
						ImGui.Text("Unhide Items");
					else
						ImGui.Text("Hide Items");
	    				ImGui.EndTooltip();
				}
    			}
		];
#if DEBUG
		TitleBarButtons.Add(new TitleBarButton() {
			Icon = FontAwesomeIcon.Code,
			Click = async (msg) => {
				if (this.loot == null)
					return;
				await Populate(this.loot);
			},
			IconOffset = new(2, 1),
			ShowTooltip = () => {
				Vector4 color = ImGuiColors.ParsedGrey;
				uint territoryId = ComfyLoot.ClientState.TerritoryType;

				ImGui.BeginTooltip();
				ImGui.Text("Debug (Click me to Populate)");

				ImGui.Separator();

				ImGui.TextColored(color, $"Homeworld: {Util.GetHomeWorld()}");
				ImGui.TextColored(color, $"Current_zone: {Util.GetZoneName(territoryId)} ({territoryId})");
				ImGui.TextColored(color, $"Is_Target_Mail: {Util.IsTargetMail()}");
				ImGui.TextColored(color, $"Is_Target_Marketboard: {Util.IsTargetMarketboard()}");
				ImGui.TextColored(color, $"Last Tradepartner: {Util.GetTradePartner()}");

				ImGui.EndTooltip();
			}
		});
#endif //* DEBUG */

		this.plugin = plugin;
		this.loot = loot;

		globalSort = null;
		sortStates = new Dictionary<string, SortState>();

		hideItems = true;
		hidenItems = new List<uint>();
		hidenZones = new List<string>();
	}

	/// <summary>
	/// Renders the main UI window.
	/// </summary>
	/* TODO: Item list list should be moved to its own function */
	public override void
	Draw()
	{
		const float HeaderHeightMultiplier = 1.1f;

		uint headerBg;
		float headerHeight;
		float minHeaderHeight;
		float scrollY;
		string text;
		Vector2 cursorPos;
		Vector2 childPos;
		Vector2 childSize;
		Vector2 headerPos;
		Vector2 textSize;
		ImGuiTableFlags tableFlags;
		IEnumerable<KeyValuePair<string, List<LootItem>>> zones;

		/* NOTE: if no loot at all, we can skip the render */
		if (plugin.LootManager.Loot.Count == 0) {
			Vector2 windowSize; /* NOTE: only used in this specific case*/

			ImGui.Spacing();

			text = "You have not received any loot yet";
			textSize = ImGui.CalcTextSize(text);
			windowSize = ImGui.GetWindowSize();

			ImGui.SetCursorPos(new Vector2(
			    (windowSize.X - textSize.X) * 0.5f,
			    (windowSize.Y - textSize.Y) * 0.5f
			));

			ImGui.TextColored(ImGuiColors.DalamudGrey, text);
			return;
		}

		ImGui.BeginChild("LootCountersChild", new Vector2(0, 55), true);
		DrawItemCounter();
		DrawValueDisplay(plugin.LootManager.GetTotalItemValue());
		ImGui.EndChild();

		ImGui.Spacing();

		ImGui.BeginChild("LootZonesChild", new Vector2(0, 0), false);

		tableFlags =
			ImGuiTableFlags.RowBg |
			ImGuiTableFlags.BordersOuter |
			ImGuiTableFlags.BordersInnerV |
			ImGuiTableFlags.SizingStretchProp;

		/* NOTE: draw at absolute positon for sticky header */
		childPos = ImGui.GetWindowPos();
		childSize = ImGui.GetWindowSize();
		scrollY = ImGui.GetScrollY();
		ImGui.SetCursorScreenPos(childPos);

		headerPos = ImGui.GetCursorScreenPos();

		if (ImGui.BeginTable("lootheader", 4, tableFlags)) {
			/* NOTE: lables will get set later */
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 20.0f);
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 55.0f);
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 55.0f);

			ImGui.TableNextRow();
			headerBg = ImGui.GetColorU32(ImGuiCol.Tab);

			for (int col = 1; col <= 3; col++) {
				ImGui.TableSetColumnIndex(col);
				ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, headerBg);

				text = col switch {
					1 => "Item",
					2 => "Amount",
					3 => "Value",
					_ => "#"
				};

				textSize = ImGui.CalcTextSize(text);
				cursorPos = ImGui.GetCursorPos();

				if (ImGui.InvisibleButton($"##HeaderBtn{col}", textSize)) {
					globalSort ??= new SortState();
					if (globalSort.Column == col)
						globalSort.Ascending = !globalSort.Ascending;
					else {
						globalSort.Column = col;
						globalSort.Ascending = true;
					}
				}

				ImGui.SetCursorPos(cursorPos);
				ImGui.TextUnformatted(text);

				if (globalSort != null && globalSort.Column == col) {
					ImGui.SameLine();
					DrawSortingArrow(globalSort.Ascending);
				}
			}

			ImGui.EndTable();
		}

		/* NOTE: sticky header math */
		headerHeight = ImGui.GetCursorScreenPos().Y - headerPos.Y;
		minHeaderHeight = ImGui.GetFrameHeight() * HeaderHeightMultiplier;
		if (headerHeight < minHeaderHeight)
			headerHeight = minHeaderHeight;

		ImGui.PushClipRect(
		    new Vector2(childPos.X, childPos.Y + headerHeight),
		    new Vector2(childPos.X + childSize.X, childPos.Y + childSize.Y),
		    true
		);

		ImGui.SetCursorScreenPos(new Vector2(
		    childPos.X,
		    childPos.Y + headerHeight - scrollY
		));

		zones = SortZones(plugin.LootManager.Loot, globalSort);

		foreach (KeyValuePair<string, List<LootItem>> kvp in zones) {
			if (kvp.Value == null)
				continue;

			if (!(hideItems && hidenZones.Contains(kvp.Key)))
				DrawItemList(kvp.Key, kvp.Value);
		}

		ImGui.PopClipRect();
		ImGui.EndChild();
	}

	/// <summary>
	/// Draws the game icon for the specified item, if valid.
	/// </summary>
	/// <param name="itemId">The item ID to draw an icon for.</param>
	private static void
	DrawIcon(uint itemId)
	{
		Vector2 iconSize = new Vector2(20, 20);
		ISharedImmediateTexture? sharedTexture = GetIcon(itemId);

		if (sharedTexture == null) {
			ImGui.TextUnformatted("");
			return;
		}

		using IDalamudTextureWrap? wrap = sharedTexture.GetWrapOrEmpty();
		if (wrap != null) {
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 20f);
			ImGui.Image(wrap.Handle, iconSize);
		} else {
			ImGui.TextUnformatted("");
		}
	}

	/// <summary>
	/// Draws the total item counter.
	/// </summary>
	private void
	DrawItemCounter()
	{
		ImGui.TextUnformatted($"Total count: {plugin.LootManager.GetTotalItemQuantity()}");
		ImGui.SameLine();

		using (ImRaii.PushFont(UiBuilder.IconFont))
			ImGui.TextDisabled($"{FontAwesomeIcon.QuestionCircle.ToIconString()}");

		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
			ImGui.TextUnformatted("Only traditional items are counted.");
			ImGui.TextUnformatted("Currencies such as Gil, Scrips, or Tomestones are ignored.");
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
	}

	/// <summary>
	/// Draws a single loot item row inside a zone table:
	/// </summary>
	/// <param name="item">The loot item to draw.</param>
	private void
	DrawItem(LootItem item)
	{
		ReadOnlySeString itemName;
		ImGui.TableNextRow();
		ImGui.TableSetColumnIndex(0);
		DrawIcon(item.ItemId);

		ImGui.TableNextColumn();
		itemName = ItemUtil.GetItemName(item.ItemId, true);
		ImGui.PushID((int)item.ItemId);
		ImGui.TextColored(GetRarityColor(item.Rarity), itemName.ToString());
		DrawItemContext(item, itemName);
		DrawItemTooltip(item, itemName);
		ImGui.PopID();

		ImGui.TableNextColumn();
		ImGui.TextUnformatted(Util.FormatNumber(item.Quantity));
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(item.Value == 0 ? "N/A" : Util.FormatGil(item.Value * item.Quantity));
	}

	/// <summary>
	/// Draws the right-click context menu for a loot item.
	/// </summary>
	/// <param name="item">The loot item the context applies to.</param>
	/// <param name="itemName">The resolved item name.</param>
	private void
	DrawItemContext(LootItem item, ReadOnlySeString itemName)
	{
		if (ImGui.BeginPopupContextItem("##ItemContext")) {
			if (ImGui.MenuItem("Ignore Item")) {
				plugin.Configuration.IgnoredItemIds.Add(item.ItemId);
				plugin.Configuration.Save();
			}
			if (ImGui.MenuItem("Hide Item"))
				hidenItems.Add(item.ItemId);
			if (ImGui.MenuItem("Copy Name"))
				ImGui.SetClipboardText(itemName.ToString());
			ImGui.EndPopup();
		}
	}

	/// <summary>
	/// Draws a tooltip for the hovered loot item.
	/// </summary>
	/// <param name="item">The loot item being hovered.</param>
	/// <param name="itemName">The readable name of the item.</param>
	private static void
	DrawItemTooltip(LootItem item, ReadOnlySeString itemName)
	{
		byte rarity;
		int amount;
		int value;
		string name;

		if (!ImGui.IsItemHovered())
			return;

		rarity = item.Rarity;
		amount = item.Quantity;
		value = item.Value;
		name = itemName.ToString();

		ImGui.BeginTooltip();
		ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);

		ImGui.TextColored(GetRarityColor(rarity), name);
		ImGui.SameLine();
		ImGui.TextUnformatted($"x {Util.FormatNumber(amount)}");

		if (value != 0)
			ImGui.TextUnformatted($"MB: {Util.FormatGil(value)} (total: {Util.FormatGil(amount * value)})");

#if DEBUG
		ImGui.Separator();
		ImGui.TextUnformatted($"Id: {item.ItemId}");
		ImGui.TextUnformatted($"BaseId: {Util.GetItemBaseId(item.ItemId)}");
		ImGui.TextUnformatted($"Rarity: {item.Rarity}");
		ImGui.TextUnformatted($"Tradable: {Util.IsTradable(item.ItemId)}");
		ImGui.TextUnformatted($"IsCurrency: {Util.IsCurrency(item.ItemId)}");
#endif //* DEBUG */

		ImGui.PopTextWrapPos();
		ImGui.EndTooltip();
	}

	/// <summary>
	/// Draws a collapsible table representing all items obtained in a specific zone.
	/// Supports sorting and hiding behavior.
	/// </summary>
	/// <param name="zone">The territory ID for the zone.</param>
	/// <param name="items">The list of loot items in that zone.</param>
	private void
	DrawItemList(string zone, List<LootItem> items)
	{
		int col;
		uint headerBg;
		bool zoneOpen;
		string tableId;
		string label;
		SortState? sort;
		Vector2 cursorPos;
		Vector2 labelSize;
		ImGuiTableFlags tableFlags;

		tableId = $"LootTableZone_{zone}";
		tableFlags = ImGuiTableFlags.RowBg |
			     ImGuiTableFlags.BordersOuter |
			     ImGuiTableFlags.BordersInnerV |
			     ImGuiTableFlags.SizingStretchProp;

		if (!ImGui.BeginTable(tableId, 4, tableFlags))
			return;

		ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 20.0f);
		ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, 55.0f);
		ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 55.0f);

		ImGui.TableNextRow();
		headerBg = ImGui.GetColorU32(ImGuiCol.Tab);

		if (!sortStates.TryGetValue(zone, out sort)) {
			sort = new SortState();
			sortStates[zone] = sort;
		}

		zoneOpen = false;
		for (col = 0; col <= 3; col++) {
			ImGui.TableSetColumnIndex(col);
			ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, headerBg, ImGui.TableGetRowIndex());
			switch (col) {
			case 0: /* List collapser */
				ImGui.PushID(zone);
				zoneOpen = ImGui.TreeNodeEx("##zone", ImGuiTreeNodeFlags.DefaultOpen);
				DrawItemListContext(zone); // right-click menu
				ImGui.PopID();
				continue; // skip the rest of the loop for this column
			case 1: /* Zone Name */
				ImGui.PushID("HeaderZoneName");
				label = zone;
				break;
			case 2: /* Item Amount */
				ImGui.PushID("HeaderAmount");
				label = Util.FormatNumber(loot.GetZoneItemQuantity(zone)) + " x";
				break;
			case 3: /* Item Value */
				ImGui.PushID("HeaderValue");
				label = Util.FormatGil(loot.GetZoneItemValue(zone));
				break;
			default:
				label = "";
				break;
			}

			labelSize = ImGui.CalcTextSize(label);
			cursorPos = ImGui.GetCursorPos();
			if (ImGui.InvisibleButton($"##HeaderBtn{col}", labelSize)) {
				if (sort.Column == col)
					sort.Ascending = !sort.Ascending;
				else { sort.Column = col; sort.Ascending = true; }
			}

			ImGui.SetCursorPos(cursorPos);
			ImGui.TextUnformatted(label);

			if (sort.Column == col) {
				ImGui.SameLine();
				DrawSortingArrow(sort.Ascending);
			}

			DrawItemListContext(zone);
			ImGui.PopID();
		}

		SortLootItems(items, sort);

		if (zoneOpen) {
			foreach (LootItem item in items) {
				if (!(hideItems && hidenItems.Contains(item.ItemId)))
					DrawItem(item);
			}
			ImGui.TreePop();
		}

		ImGui.EndTable();
	}

	/// <summary>
	/// Draws the right-click context menu for a zone entry.
	/// </summary>
	/// <param name="zone">The territory ID of the zone being interacted with.</param>
	private void
	DrawItemListContext(string zone)
	{
		if (ImGui.BeginPopupContextItem($"##ZoneContextName_{zone}")) {
			if (ImGui.MenuItem("Ignore Zone")) {
				plugin.Configuration.IgnoredZones.Add(zone);
				plugin.Configuration.Save();
			}
			if (ImGui.MenuItem("Hide Loot")) {
				hidenZones.Add(zone);
				/* NOTE: Hide all Items in other zones */
				foreach (LootItem item in loot.Loot[zone])
					hidenItems.Add(item.ItemId);
			}
			if (ImGui.MenuItem("Reset")) {
				loot.ClearZone(zone);
				plugin.UpdateDtrBar();
			}
			if (ImGui.MenuItem("Copy Name"))
				ImGui.SetClipboardText(zone);
			ImGui.EndPopup();
		}
	}

	/// <summary>
	/// Draws a small arrow (▲ / ▼) indicating sorting direction.
	/// </summary>
	/// <param name="asc">If true, a down arrow is drawn; otherwise an up arrow.</param>
	private static void
	DrawSortingArrow(bool asc)
	{
		const float scale = 0.65f;

		string glyph;
		float fontsize;
		float offsetY;
		ImDrawListPtr drawList;
		Vector2 position;

		drawList = ImGui.GetWindowDrawList();
		position = ImGui.GetCursorScreenPos();
		fontsize = ImGui.GetFontSize() * scale;
		offsetY = (ImGui.GetTextLineHeight() - fontsize) * 0.5f;
		position.Y += offsetY;

		if (asc)
			glyph = "▲";
		else
			glyph = "▼";

		drawList.AddText(
			ImGui.GetFont(),
			fontsize,
			position,
			ImGui.GetColorU32(ImGuiCol.Text),
			glyph
		);
	}

	/// <summary>
	/// Draws a meter indicating total accumulated value (gil).
	/// </summary>
	/// <param name="totalValue">The accumulated total value across all loot items.</param>
	private static void
	DrawValueDisplay(int totalValue)
	{
		ImGui.TextUnformatted($"Total Value: {Util.FormatGil(totalValue)}");
		ImGui.SameLine();
		using (ImRaii.PushFont(UiBuilder.IconFont))
			ImGui.TextDisabled($"{FontAwesomeIcon.QuestionCircle.ToIconString()}");

		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
			ImGui.TextUnformatted("Rough estimate");
			ImGui.TextUnformatted("Actual value may differ.");
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
	}

	/// <summary>
	/// Retrieves the icon texture for the given item ID.
	/// Returns <c>null</c> if the icon cannot be resolved.
	/// </summary>
	/// <param name="itemId">The item ID whose icon should be loaded.</param>
	/// <returns>A shared texture containing the icon, or <c>null</c> on failure.</returns>
	private static ISharedImmediateTexture?
	GetIcon(uint itemId)
	{
		uint baseId;
		bool hq;
		ExcelSheet<Item>? items;
		GameIconLookup lookup;
		ISharedImmediateTexture? sharedTexture;

		hq = false;
		items = ComfyLoot.DataManager.GetExcelSheet<Item>();
		if (items == null) {
			ComfyLoot.Log.Fatal("[Lumina] Cannot determine Icon, Item-sheet cannot be resolved");
			return null;
		}

		baseId = Util.GetItemBaseId(itemId);
		if (!items.TryGetRow(baseId, out var item))
			return null;

		if (itemId >= 1000000)
			hq = true;

		lookup = new GameIconLookup(item.Icon, hq);
		if (!ComfyLoot.Textures.TryGetFromGameIcon(in lookup, out sharedTexture)
		|| sharedTexture == null)
			return null;
		return sharedTexture;
	}

	/// <summary>
	/// Returns the UI color associated with an item's rarity level.
	/// </summary>
	/// <param name="rarity">The rarity rank of the item.</param>
	/// <returns>An <see cref="Vector4"/> representing the color.</returns>
	private static Vector4
	GetRarityColor(int rarity)
	{
		switch (rarity) {
		case 1:
			return ImGuiColors.DalamudWhite;
		case 2:
			return ImGuiColors.ParsedGreen;
		case 3:
			return ImGuiColors.ParsedBlue;
		case 4:
			return ImGuiColors.ParsedPurple;
		case 7:
			return ImGuiColors.ParsedPink;
		default:
			return ImGuiColors.DalamudGrey;
		}
	}

#if DEBUG
	/// <summary>
	/// Populates the loot manager with a set of predefined debug items,
	/// useful for UI testing without having to play content.
	/// </summary>
	/// <param name="loot">The loot manager instance to populate.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	private static async Task
	Populate(LootManager loot)
	{
		const uint aurum_vale = 172;

		await loot.AddItem(
			id: 1, /* gil */
			amount: 1000000,
			zone: 0,
			zoneName: "Delivery"
		);
		await loot.AddItem(
			id: 5823, /* allagan tin */
			amount: 20,
			zone: 0,
			zoneName: "Delivery"
		);
		await loot.AddItem(
			id: 5824, /* allagan bronze */
			amount: 20,
			zone: 0,
			zoneName: "Delivery"
		);
		await loot.AddItem(
			id: 27994, /* nightworld bronce */
			amount: 20,
			zone: 0,
			zoneName: "Delivery"
		);
		await loot.AddItem(
			id: 5825, /* allagan silver */
			amount: 20,
			zone: 0,
			zoneName: "Delivery"
		);
		await loot.AddItem(
			id: 28062, /* nightworld silver */
			amount: 20,
			zone: 0,
			zoneName: "Delivery"
		);
		await loot.AddItem(
			id: 5826, /* allagan gold */
			amount: 20,
			zone: 0,
			zoneName: "Delivery"
		);
		await loot.AddItem(
			id: 5827, /* allagan platinum */
			amount: 20,
			zone: 0,
			zoneName: "Delivery"
		);
		await loot.AddItem(
			id: 14, /* fire cluster */
			amount: 999,
			zone: 0,
			zoneName: "Delivery"
		);
		await loot.AddItem(
			id: 1046003, /* mate cookie (hq) */
			amount: 99,
			zone: 0,
			zoneName: "Marketboard"
		);
		await loot.AddItem(
			id: 46003, /* mate cookie (sq) */
			amount: 99,
			zone: 0,
			zoneName: "Marketboard"
		);
		await loot.AddItem(
			id: 2791, /* aetherial mythril circlet (rubellite) */
			amount: 1,
			zone: aurum_vale
		);
		await loot.AddItem(
			id: 3035, /* acolyte's robe */
			amount: 1,
			zone: aurum_vale
		);
		await loot.AddItem(
			id: 32418,/* cryptlurker sword */
			amount: 1,
			zone: aurum_vale
		);
		await loot.AddItem(
			id: 33475, /* blade's fealty */
			amount: 1,
			zone: aurum_vale
		);
	}
#endif //* DEBUG*/

	/// <summary>
	/// Sorts a list of <see cref="LootItem"/> based on the given <see cref="SortState"/>.
	/// </summary>
	/// <param name="items">The list of loot items to sort.</param>
	/// <param name="sort">The sort state specifying the column and direction.</param>
	private void
	SortLootItems(List<LootItem> items, SortState sort)
	{
		string nameA;
		string nameB;
		bool ascending;
		Comparison<LootItem> comparison;

		switch (sort.Column) {
		case 1: /* Name */
			comparison = (a, b) => {
				nameA = ItemUtil.GetItemName(a.ItemId, true).ToString();
				if (nameA == null)
					nameA = string.Empty;
				nameB = ItemUtil.GetItemName(b.ItemId, true).ToString();
				if (nameB == null)
					nameB = string.Empty;

				return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
			};
			break;
		case 2: /* Quantity */
			comparison = (a, b) => a.Quantity.CompareTo(b.Quantity);
			break;
		case 3: /* Total value */
			comparison = (a, b) => ((long)a.Value * a.Quantity).CompareTo((long)b.Value * b.Quantity);
			break;
		default:
			comparison = (a, b) => 0;
			break;
		}

		/* HACK: for some reason it gets flipped in the UI, unflipping */
		ascending = sort.Ascending;
		if (sort.Ascending)
			ascending = sort.Ascending;
		if (sort.Column == 1)
			ascending = !ascending;

		if (ascending)
			items.Sort(comparison);
		else
			items.Sort((a, b) => comparison(b, a));
	}

	/// <summary>
	/// Sorts zones (each a key-value pair of zone ID and loot list) based on the given <see cref="SortState"/>.
	/// </summary>
	/// <param name="zones">The zones to sort.</param>
	/// <param name="sort">The sort state specifying the column and direction.</param>
	/// <returns>The sorted zones.</returns>
	/* TODO: oof */
	private IEnumerable<KeyValuePair<string, List<LootItem>>>
	SortZones(IEnumerable<KeyValuePair<string, List<LootItem>>> zones, SortState? sort)
	{
		if (sort == null)
			return zones;

		/* NOTE: would be rediculously complex without System.LINQ, acceptable use. */
		switch (sort.Column) {
		case 1: /* Name */
			/* HACK: for some reason it gets flipped in the UI, so we do the opposite here */
			if (sort.Ascending)
				return zones.OrderByDescending(z => z.Key).ToList();
			else
				return zones.OrderBy(z => z.Key).ToList();
		case 2: /* Quantity */
			if (sort.Ascending)
				return zones.OrderBy(z => loot.GetZoneItemQuantity(z.Key)).ToList();
			else
				return zones.OrderByDescending(z => loot.GetZoneItemQuantity(z.Key)).ToList();
		case 3: /* Total value */
			if (sort.Ascending)
				return zones.OrderBy(z => loot.GetZoneItemValue(z.Key)).ToList();
			else
				return zones.OrderByDescending(z => loot.GetZoneItemValue(z.Key)).ToList();
		default:
			return zones;
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
		ComfyLoot.Log.Verbose("[MainWindow] Disposing UI");
		/* nothing to clean */
	}
}