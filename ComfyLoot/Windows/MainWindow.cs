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

	private bool hideItems = true;
	private SortState globalSort = null;
	private Dictionary<uint, SortState> sortStates = new();

	private readonly ComfyLoot plugin;
	private readonly LootManager loot;

	private readonly List<uint> hidenItems;
	private readonly List<uint> hidenZones;

	public MainWindow(ComfyLoot plugin, LootManager loot)
		: base("ComfyLoot###comfyloot_ui", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(375, 330),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};

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
			Click = async (msg) => { await Populate(this.loot); },
			IconOffset = new(2, 1),
			ShowTooltip = () => {
				ImGui.BeginTooltip();
				ImGui.Text("Debug Populate");
				ImGui.EndTooltip();
			}
		});
#endif

		this.plugin = plugin;
		this.loot = loot;

		hidenItems = new List<uint>();
		hidenZones = new List<uint>();
	}

	public override void 
	Draw()
	{
		uint headerBg;
		int col;
		string label;
		Vector2 labelSize;
		Vector2 cursorPos;
		Vector2 windowSize;
		Vector2 textSize;
		ImGuiTableFlags tableFlags;
		List<LootItem> items;
		IEnumerable<KeyValuePair<uint, List<LootItem>>> zones;
		
		if (plugin.LootManager.Loot.Count == 0) {
			ImGui.Spacing();

			label = "You have not received any loot yet";
			windowSize = ImGui.GetWindowSize();
			textSize = ImGui.CalcTextSize(label);

			ImGui.SetCursorPos(new Vector2(
			    (windowSize.X - textSize.X) * 0.5f,
			    (windowSize.Y - textSize.Y) * 0.5f
			));

			ImGui.TextColored(ImGuiColors.DalamudGrey, label);
			return;
		}

		ImGui.BeginChild("LootCountersChild", new Vector2(0, 55), true, ImGuiWindowFlags.NoScrollbar);
		DrawItemCounter();
		DrawValueDisplay(plugin.LootManager.GetTotalItemValue());
		ImGui.EndChild();
		ImGui.Spacing();

		tableFlags = ImGuiTableFlags.RowBg |
			     ImGuiTableFlags.BordersOuter |
			     ImGuiTableFlags.BordersInnerV |
			     ImGuiTableFlags.SizingStretchProp;

		if (ImGui.BeginTable("lootheader", 4, tableFlags)) {
			ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 20.0f);
			ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, 80.0f);
			ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 80.0f);

			ImGui.TableNextRow();
			headerBg = ImGui.GetColorU32(ImGuiCol.Tab);

			for (col = 1; col <= 3; col++) {
				ImGui.TableSetColumnIndex(col);
				ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, headerBg, ImGui.TableGetRowIndex());

				switch (col) {
				case 1:
					label = "Zone Name";
					break;
				case 2:
					label = "Total Items";
					break;
				case 3:
					label = "Total Value";
					break;
				default:
					label = "";
					break;
				}

				labelSize = ImGui.CalcTextSize(label);
				cursorPos = ImGui.GetCursorPos();

				if (ImGui.InvisibleButton($"##HeaderBtn{col}", labelSize)) {
					if (globalSort == null)
						globalSort = new SortState();

					if (globalSort.Column == col) {
						globalSort.Ascending = !globalSort.Ascending;
					} else {
						globalSort.Column = col;
						globalSort.Ascending = true;
					}
				}

				ImGui.SetCursorPos(cursorPos);
				ImGui.TextUnformatted(label);

				if (globalSort != null && globalSort.Column == col) {
					ImGui.SameLine();
					DrawSortingArrow(globalSort.Ascending);
				}
			}

			ImGui.EndTable();
		}

		zones = plugin.LootManager.Loot;
		if (globalSort != null) {
			switch (globalSort.Column) {
			case 1:
				if (globalSort.Ascending)
					zones = zones.OrderBy(z => Util.GetZoneName(z.Key)).ToList();
				else
					zones = zones.OrderByDescending(z => Util.GetZoneName(z.Key)).ToList();
				break;
			case 2:
				if (globalSort.Ascending)
					zones = zones.OrderBy(z => loot.GetZoneItemQuantity(z.Key)).ToList();
				else
					zones = zones.OrderByDescending(z => loot.GetZoneItemQuantity(z.Key)).ToList();
				break;
			case 3:
				if (globalSort.Ascending)
					zones = zones.OrderBy(z => loot.GetZoneItemValue(z.Key)).ToList();
				else
					zones = zones.OrderByDescending(z => loot.GetZoneItemValue(z.Key)).ToList();
				break;
			}
		}

		foreach (var kvp in zones) {
			items = kvp.Value;
			if (items == null)
				continue;

			if (!(hideItems && hidenZones.Contains(kvp.Key)))
				DrawItemList(kvp.Key, items);
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
		DrawItemContext(item, itemName);
		DrawItemTooltip(item, itemName);
		ImGui.PopID();

		ImGui.TableNextColumn();
		ImGui.TextUnformatted(Util.FormatNumber(item.Quantity));
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(item.Value == 0 ? "N/A" : Util.FormatGil(item.Value * item.Quantity));
	}

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

	private void 
	DrawItemList(uint zone, List<LootItem> items)
	{
		int col;
		uint headerBg;
		bool zoneOpen;
		string tableId;
		string label;
		SortState sort;
		Vector2 cursorPos;
		Vector2 labelSize;
		ImGuiTableFlags tableFlags;
		Comparison<LootItem> comparison;

		tableId = $"LootTableZone_{zone}";
		tableFlags = ImGuiTableFlags.RowBg |
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
				ImGui.PushID((int)zone);
				zoneOpen = ImGui.TreeNodeEx("##zone", ImGuiTreeNodeFlags.DefaultOpen);
				DrawItemListContext(zone); // right-click menu
				ImGui.PopID();
				continue; // skip the rest of the loop for this column
			case 1: /* Zone Name */
				ImGui.PushID("HeaderZoneName");
				label = Util.GetZoneName(zone);
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

		comparison = sort.Column switch {
			1 => (a, b) => string.Compare(
			    ItemUtil.GetItemName(a.ItemId, true).ToString(),
			    ItemUtil.GetItemName(b.ItemId, true).ToString(),
			    StringComparison.OrdinalIgnoreCase),
			2 => (a, b) => a.Quantity.CompareTo(b.Quantity),
			3 => (a, b) => ((long)a.Value * a.Quantity).CompareTo((long)b.Value * b.Quantity),
			_ => (a, b) => 0
		};
		items.Sort(sort.Ascending ? comparison : (a, b) => comparison(b, a));

		if (zoneOpen) {
			foreach (LootItem item in items) {
				if (!(hideItems && hidenItems.Contains(item.ItemId)))
					DrawItem(item);
			}
			ImGui.TreePop();
		}

		ImGui.EndTable();
	}

	private void 
	DrawItemListContext(uint zone)
	{
		if (ImGui.BeginPopupContextItem($"##ZoneContextName_{zone}")) {
			if (ImGui.MenuItem("Ignore Zone")) {
				plugin.Configuration.IgnoredZoneIds.Add(zone);
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
				ImGui.SetClipboardText(Util.GetZoneName(zone));
			ImGui.EndPopup();
		}
	}

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