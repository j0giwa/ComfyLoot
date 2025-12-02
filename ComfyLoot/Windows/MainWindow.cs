/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
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
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

using ComfyLoot.Managers;
using System.Linq;

namespace ComfyLoot.Windows;

public class SortState {
	public int Column = 1;
	public bool Ascending = true;
}

/// <summary>
/// Mainplugin UI
/// </summary>
public class MainWindow : Window, IDisposable {

	private SortState globalSort = null;
	private Dictionary<uint, SortState> sortStates = new();

	private readonly ComfyLoot plugin;
	private readonly LootManager loot;

	public MainWindow(ComfyLoot plugin, LootManager loot)
	    : base("ComfyLoot###comfyloot_ui", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(375, 330),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};

		this.plugin = plugin;
		this.loot = loot;

		TitleBarButtons = [
			new TitleBarButton() {
				Icon = FontAwesomeIcon.Cog,
				Click = (msg) => { this.plugin.ToggleConfigUI(); },
				IconOffset = new(2,1),
				ShowTooltip = () => {
					ImGui.BeginTooltip();
					ImGui.Text("Open Settings");
					ImGui.EndTooltip();
				}
	    		}
		];
#if DEBUG
		TitleBarButtons.Add(new TitleBarButton() {
			Icon = FontAwesomeIcon.Code,
			Click = async (msg) => { await Populate(this.loot); },
			IconOffset = new(2, 1),
			ShowTooltip = () => {
				ImGui.BeginTooltip();
				ImGui.Text("Debug Populate");
				ImGui.EndTooltip();
			}
		});
#endif //* DEBUG */
	}

	public override void 
	Draw()
	{
		List<LootItem> items;

		if (plugin.LootManager.Loot.Count == 0) {
			ImGui.Spacing();

			var text = "You have not received any loot yet";
			var windowSize = ImGui.GetWindowSize();
			var textSize = ImGui.CalcTextSize(text);

			ImGui.SetCursorPos(new Vector2(
			    (windowSize.X - textSize.X) * 0.5f,
			    (windowSize.Y - textSize.Y) * 0.5f
			));

			ImGui.TextColored(ImGuiColors.DalamudGrey, text);
			return;
		}

		ImGui.BeginChild("LootCountersChild", new Vector2(0, 55), true, ImGuiWindowFlags.NoScrollbar);
		DrawItemCounter();
		DrawValueDisplay(plugin.LootManager.GetTotalItemValue());
		ImGui.EndChild();
		ImGui.Spacing();

		ImGuiTableFlags tableFlags = ImGuiTableFlags.RowBg |
					     ImGuiTableFlags.BordersOuter |
					     ImGuiTableFlags.BordersInnerV |
					     ImGuiTableFlags.SizingStretchProp;

		if (ImGui.BeginTable("lootheader", 4, tableFlags)) {
			ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 20.0f);
			ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, 80.0f);
			ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 80.0f);

			ImGui.TableNextRow();
			ImGui.TableSetColumnIndex(1);
			string itemLabel = "Zone Name" + (globalSort?.Column == 1 ? (globalSort.Ascending ? " ▲" : " ▼") : "");
			Vector2 cursorPos = ImGui.GetCursorPos();
			if (ImGui.InvisibleButton("##ZoneNameSort", ImGui.CalcTextSize(itemLabel))) {
				if (globalSort == null)
					globalSort = new SortState();
				if (globalSort.Column == 1)
					globalSort.Ascending = !globalSort.Ascending;
				else { globalSort.Column = 1; globalSort.Ascending = true; }
			}
			ImGui.SetCursorPos(cursorPos);
			ImGui.Text(itemLabel);

			ImGui.TableNextColumn();
			string amountLabel = "Total Items" + (globalSort?.Column == 2 ? (globalSort.Ascending ? " ▲" : " ▼") : "");
			cursorPos = ImGui.GetCursorPos();
			if (ImGui.InvisibleButton("##ZoneAmountSort", ImGui.CalcTextSize(amountLabel))) {
				if (globalSort == null)
					globalSort = new SortState();
				if (globalSort.Column == 2)
					globalSort.Ascending = !globalSort.Ascending;
				else { globalSort.Column = 2; globalSort.Ascending = true; }
			}
			ImGui.SetCursorPos(cursorPos);
			ImGui.Text(amountLabel);

			ImGui.TableNextColumn();
			string valueLabel = "Total Value" + (globalSort?.Column == 3 ? (globalSort.Ascending ? " ▲" : " ▼") : "");
			cursorPos = ImGui.GetCursorPos();
			if (ImGui.InvisibleButton("##ZoneValueSort", ImGui.CalcTextSize(valueLabel))) {
				if (globalSort == null)
					globalSort = new SortState();
				if (globalSort.Column == 3)
					globalSort.Ascending = !globalSort.Ascending;
				else { globalSort.Column = 3; globalSort.Ascending = true; }
			}
			ImGui.SetCursorPos(cursorPos);
			ImGui.Text(valueLabel);

			ImGui.EndTable();
		}

		IEnumerable<KeyValuePair<uint, List<LootItem>>> zones = plugin.LootManager.Loot;

		if (globalSort != null) {
			switch (globalSort.Column) {
			case 1: /* Sort by zone name */
				zones = globalSort.Ascending
				    ? zones.OrderBy(z => Util.GetZoneName(z.Key))
				    : zones.OrderByDescending(z => Util.GetZoneName(z.Key));
				break;
			case 2: /* Sort by total items */
				zones = globalSort.Ascending
				    ? zones.OrderBy(z => loot.GetZoneItemQuantity(z.Key))
				    : zones.OrderByDescending(z => loot.GetZoneItemQuantity(z.Key));
				break;
			case 3: /* Sort by total value */
				zones = globalSort.Ascending
				    ? zones.OrderBy(z => loot.GetZoneItemValue(z.Key))
				    : zones.OrderByDescending(z => loot.GetZoneItemValue(z.Key));
				break;
			}
		}

		foreach (var kvp in zones) {
			items = kvp.Value;
			if (items == null)
				continue;

			DrawItemList(kvp.Key, items); // zone-level sorting is independent
		}
	}

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

	private void 
	DrawItem(LootItem item)
	{
		ReadOnlySeString itemName;
		ImGui.TableNextRow();
		ImGui.TableSetColumnIndex(0);
		DrawIcon(Util.GetBaseId(item.ItemId));

		ImGui.TableNextColumn();
		itemName = ItemUtil.GetItemName(item.ItemId, true);
		ImGui.PushID((int)item.ItemId);
		ImGui.TextColored(GetRarityColor(item.Rarity), itemName.ToString());

		if (ImGui.BeginPopupContextItem("##ItemContext")) {
			if (ImGui.MenuItem("Ignore Item")) {
				plugin.Configuration.IgnoredItemIds.Add(item.ItemId);
				plugin.Configuration.Save();
			}
			if (ImGui.MenuItem("Copy Name"))
				ImGui.SetClipboardText(itemName.ToString());
			ImGui.EndPopup();
		}

		DrawItemTooltip(item, itemName);
		ImGui.PopID();

		ImGui.TableNextColumn();
		ImGui.TextUnformatted(Util.FormatNumber(item.Quantity));
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(item.Value == 0 ? "N/A" : Util.FormatGil(item.Value * item.Quantity));
	}

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
		ImGui.TextUnformatted($"BaseId: {Util.GetBaseId(item.ItemId)}");
		ImGui.TextUnformatted($"Rarity: {item.Rarity}");
		ImGui.TextUnformatted($"Tradable: {Util.IsTradable(item.ItemId)}");
		ImGui.TextUnformatted($"IsCurrency: {Util.IsCurrency(item.ItemId)}");
#endif

		ImGui.PopTextWrapPos();
		ImGui.EndTooltip();
	}

	private void DrawItemList(uint zone, List<LootItem> items)
	{
		bool zoneOpen;
		uint headerBg;
		string tableId;
		string itemLabel;
		string amountLabel;
		string valueLabel;

		tableId = $"LootTableZone_{zone}";
		ImGuiTableFlags tableFlags = ImGuiTableFlags.RowBg |
					     ImGuiTableFlags.BordersOuter |
					     ImGuiTableFlags.BordersInnerV |
					     ImGuiTableFlags.SizingStretchProp;

		if (!ImGui.BeginTable(tableId, 4, tableFlags))
			return;

		ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 20.0f);
		ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, 80.0f);
		ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 80.0f);

		ImGui.TableNextRow();
		headerBg = ImGui.GetColorU32(ImGuiCol.Tab);
		for (int col = 0; col < 4; col++)
			ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, headerBg, ImGui.TableGetRowIndex());

		// TreeNode / collapse column
		ImGui.TableSetColumnIndex(0);
		ImGui.PushID((int)zone);
		zoneOpen = ImGui.TreeNodeEx("##zone", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanFullWidth);
		ImGui.PopID();

		// ----------------------------
		// Per-zone sort state
		// ----------------------------
		if (!sortStates.TryGetValue(zone, out var sort)) {
			sort = new SortState();  // defaults: Column = 1, Ascending = true
			sortStates[zone] = sort;
		}

		// Column 1: Item
		ImGui.TableNextColumn();
		ImGui.PushID("ItemHeader");
		itemLabel = Util.GetZoneName(zone) + (sort.Column == 1 ? (sort.Ascending ? " ▲" : " ▼") : "");
		Vector2 itemSize = ImGui.CalcTextSize(itemLabel);
		Vector2 cursorPos = ImGui.GetCursorPos();
		if (ImGui.InvisibleButton("##ItemBtn", itemSize)) {
			if (sort.Column == 1)
				sort.Ascending = !sort.Ascending;
			else { sort.Column = 1; sort.Ascending = true; }
		}
		ImGui.SetCursorPos(cursorPos);
		ImGui.Text(itemLabel);

		// Context menu
		if (ImGui.BeginPopupContextItem("##ZoneContext")) {
			if (ImGui.MenuItem("Ignore Zone")) {
				plugin.Configuration.IgnoredZoneIds.Add(zone);
				plugin.Configuration.Save();
			}
			if (ImGui.MenuItem("Copy Name"))
				ImGui.SetClipboardText(Util.GetZoneName(zone));
			if (ImGui.MenuItem("Reset")) {
				loot.ClearZone(zone);
				plugin.UpdateDtrBar();
			}
			ImGui.EndPopup();
		}
		ImGui.PopID();

		// Column 2: Amount
		ImGui.TableNextColumn();
		ImGui.PushID("AmountHeader");
		amountLabel = Util.FormatNumber(loot.GetZoneItemQuantity(zone)) + " x" +
			      (sort.Column == 2 ? (sort.Ascending ? " ▲" : " ▼") : "");
		cursorPos = ImGui.GetCursorPos();
		if (ImGui.InvisibleButton("##AmountBtn", ImGui.CalcTextSize(amountLabel))) {
			if (sort.Column == 2)
				sort.Ascending = !sort.Ascending;
			else { sort.Column = 2; sort.Ascending = true; }
		}
		ImGui.SetCursorPos(cursorPos);
		ImGui.Text(amountLabel);
		ImGui.PopID();

		// Column 3: Value
		ImGui.TableNextColumn();
		ImGui.PushID("ValueHeader");
		valueLabel = Util.FormatGil(loot.GetZoneItemValue(zone)) +
			     (sort.Column == 3 ? (sort.Ascending ? " ▲" : " ▼") : "");
		cursorPos = ImGui.GetCursorPos();
		if (ImGui.InvisibleButton("##ValueBtn", ImGui.CalcTextSize(valueLabel))) {
			if (sort.Column == 3)
				sort.Ascending = !sort.Ascending;
			else { sort.Column = 3; sort.Ascending = true; }
		}
		ImGui.SetCursorPos(cursorPos);
		ImGui.Text(valueLabel);
		ImGui.PopID();

		// ----------------------------
		// Sort items per zone
		// ----------------------------
		Comparison<LootItem> comparison = sort.Column switch {
			1 => (a, b) => string.Compare(ItemUtil.GetItemName(a.ItemId, true).ToString(),
						      ItemUtil.GetItemName(b.ItemId, true).ToString(),
						      StringComparison.OrdinalIgnoreCase),
			2 => (a, b) => a.Quantity.CompareTo(b.Quantity),
			3 => (a, b) => ((long)a.Value * a.Quantity).CompareTo((long)b.Value * b.Quantity),
			_ => (a, b) => 0
		};
		items.Sort(sort.Ascending ? comparison : (a, b) => comparison(b, a));

		if (zoneOpen) {
			foreach (var item in items)
				DrawItem(item);
			ImGui.TreePop();
		}

		ImGui.EndTable();
	}

	/// <summary>
	/// Draws a meter indicating the accumutated value of all items
	/// </summary>
	/// <param name="totalValue"></param>
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

	private static ISharedImmediateTexture?
	GetIcon(uint itemId)
	{
		var items = ComfyLoot.DataManager.GetExcelSheet<Item>();
		if (items == null) {
			ComfyLoot.Log.Fatal("[Lumina] Cannot determine Icon, Item-sheet cannot be resolved");
			return null;
		}
		if (!items.TryGetRow(itemId, out var item))
			return null;

		var lookup = new GameIconLookup(item.Icon);
		if (!ComfyLoot.Textures.TryGetFromGameIcon(in lookup, out var sharedTexture) || sharedTexture == null)
			return null;
		return sharedTexture;
	}

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
	private static async Task
	Populate(LootManager loot)
	{
		const uint marketboard = 1;
		const uint delivery = 2;

		await loot.AddItem(
			id: 1, /* gil */
			amount: 1000000,
			zone: delivery,
			hq: false
		);
		await loot.AddItem(
			id: 14, /* fire cluster */
			amount: 999,
			zone: marketboard,
			hq: false
		);
		await loot.AddItem(
			id: 1046003, /* mate cookie (hq) */
			amount: 99,
			zone: marketboard,
			hq: true
		);
		await loot.AddItem(
			id: 2791, /* aetherial mythril circlet (rubellite) */
			amount: 1,
			zone: delivery,
			hq: false
		);
		await loot.AddItem(
			id: 3035, /* acolyte's robe */
			amount: 1,
			zone: delivery,
			hq: true
		);
		await loot.AddItem(
			id: 32418,/* cryptlurker sword */
			amount: 1,
			zone: delivery,
			hq: false
		);
		await loot.AddItem(
			id: 33475, /* blade's fealty */
			amount: 1,
			zone: delivery,
			hq: false
		);
	}
#endif //* DEBUG*/

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