/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Numerics;
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
using System.Threading.Tasks;

namespace ComfyLoot.Windows;

/// <summary>
/// Mainplugin UI
/// </summary>
public class MainWindow : Window, IDisposable {

	private int sortColumn = 1; // default to "Item" column
	private bool sortAscending = true;

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
#if DEBUG
            		new TitleBarButton() {
				Icon = FontAwesomeIcon.Code,
				Click = async (msg) => { await Populate(this.loot); },
				IconOffset = new(2,1),
				ShowTooltip = () => { 
					ImGui.BeginTooltip(); 
					ImGui.Text("Debug Populate"); 
					ImGui.EndTooltip(); 
				}
	    		},
#endif //* DEBUG */ 
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
	DrawItemRow(LootItem item)
	{
		ReadOnlySeString itemName;
		ImGui.TableNextRow();
		ImGui.TableSetColumnIndex(0);
		DrawIcon(Util.GetBaseId(item.ItemId));

		ImGui.TableNextColumn();
		itemName = ItemUtil.GetItemName(item.ItemId, true);
		ImGui.PushID((int)item.ItemId);
		switch (item.Rarity) {
		case 1:
			ImGui.TextColored(ImGuiColors.DalamudWhite, itemName.ToString());
			break;
		case 2:
			ImGui.TextColored(ImGuiColors.ParsedGreen, itemName.ToString());
			break;
		case 3:
			ImGui.TextColored(ImGuiColors.ParsedBlue, itemName.ToString());
			break;
		case 4:
			ImGui.TextColored(ImGuiColors.ParsedPurple, itemName.ToString());
			break;
		case 7:
			ImGui.TextColored(ImGuiColors.ParsedPink, itemName.ToString());
			break;
		default:
			ImGui.TextUnformatted(itemName.ToString());
			break;
		}

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
		if (!ImGui.IsItemHovered())
			return;
		ImGui.BeginTooltip();
		ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
		ImGui.TextUnformatted($"Item: {itemName}");
		ImGui.TextUnformatted($"Id: {item.ItemId}");
		ImGui.TextUnformatted($"Value: {item.Value}");
#if DEBUG
		ImGui.Separator();
		ImGui.TextUnformatted($"BaseId: {Util.GetBaseId(item.ItemId)}");
		ImGui.TextUnformatted($"Rarity: {item.Rarity}");
		ImGui.TextUnformatted($"Tradable: {Util.IsTradable(item.ItemId)}");
		ImGui.TextUnformatted($"IsCurrency: {Util.IsCurrency(item.ItemId)}");
#endif
		ImGui.PopTextWrapPos();
		ImGui.EndTooltip();
	}

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

	// FIXME: a bit complex
	private void 
	DrawZoneSection(uint zone, List<LootItem> items)
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

		ImGui.TableSetColumnIndex(0);
		ImGui.PushID((int)zone);
		zoneOpen = ImGui.TreeNodeEx("##zone", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanFullWidth);
		ImGui.PopID();

		ImGui.TableNextColumn();
		ImGui.PushID("ItemHeader");
		itemLabel = Util.GetZoneName(zone) + (sortColumn == 1 ? (sortAscending ? " ▲" : " ▼") : "");
		Vector2 itemSize = ImGui.CalcTextSize(itemLabel);
		Vector2 cursorPos = ImGui.GetCursorPos();
		if (ImGui.InvisibleButton("##ItemBtn", itemSize)) {
			if (sortColumn == 1)
				sortAscending = !sortAscending;
			else { sortColumn = 1; sortAscending = true; }
		}
		ImGui.SetCursorPos(cursorPos);
		ImGui.Text(itemLabel);

		if (ImGui.BeginPopupContextItem("##ZoneContext")) {
			if (ImGui.MenuItem("Ignore Zone")) {
				plugin.Configuration.IgnoredZoneIds.Add(zone);
				plugin.Configuration.Save();
			}
			if (ImGui.MenuItem("Copy Name"))
				ImGui.SetClipboardText(Util.GetZoneName(zone));
			ImGui.EndPopup();
		}
		ImGui.PopID();

		// Column 2: Amount
		ImGui.TableNextColumn();
		ImGui.PushID("AmountHeader");
		amountLabel = Util.FormatNumber(loot.GetZoneItemQuantity(zone)) + " x" + (sortColumn == 2 ? (sortAscending ? " ▲" : " ▼") : "");
		Vector2 amountSize = ImGui.CalcTextSize(amountLabel);
		cursorPos = ImGui.GetCursorPos();
		if (ImGui.InvisibleButton("##AmountBtn", amountSize)) {
			if (sortColumn == 2)
				sortAscending = !sortAscending;
			else { sortColumn = 2; sortAscending = true; }
		}
		ImGui.SetCursorPos(cursorPos);
		ImGui.Text(amountLabel);
		ImGui.PopID();

		// Column 3: Value
		ImGui.TableNextColumn();
		ImGui.PushID("ValueHeader");
		valueLabel = Util.FormatGil(loot.GetZoneItemValue(zone)) + (sortColumn == 3 ? (sortAscending ? " ▲" : " ▼") : "");
		Vector2 valueSize = ImGui.CalcTextSize(valueLabel);
		cursorPos = ImGui.GetCursorPos();
		if (ImGui.InvisibleButton("##ValueBtn", valueSize)) {
			if (sortColumn == 3)
				sortAscending = !sortAscending;
			else { sortColumn = 3; sortAscending = true; }
		}
		ImGui.SetCursorPos(cursorPos);
		ImGui.Text(valueLabel);
		ImGui.PopID();

		Comparison<LootItem> comparison = sortColumn switch {
			1 => (a, b) => string.Compare(
					ItemUtil.GetItemName(a.ItemId, true).ToString(),
					ItemUtil.GetItemName(b.ItemId, true).ToString(),
					StringComparison.OrdinalIgnoreCase),
			2 => (a, b) => a.Quantity.CompareTo(b.Quantity),
			3 => (a, b) => ((long)a.Value * a.Quantity).CompareTo((long)b.Value * b.Quantity),
			_ => (a, b) => 0
		};
		items.Sort(sortAscending ? comparison : (a, b) => comparison(b, a));

		if (zoneOpen) {
			foreach (var item in items)
				DrawItemRow(item);
			ImGui.TreePop();
		}

		ImGui.EndTable();
	}
	
	public override void 
	Draw()
	{
		List<LootItem> items;

		// NOTE: If no loot at all, we can skip the rest of the code
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

		DrawItemCounter();
		DrawValueDisplay(plugin.LootManager.GetTotalItemValue());
		ImGui.Spacing();

		ImGuiTableFlags tableFlags = ImGuiTableFlags.RowBg |
					     ImGuiTableFlags.BordersOuter |
					     ImGuiTableFlags.BordersInnerV |
					     ImGuiTableFlags.SizingStretchProp;

		if (!ImGui.BeginTable("lootheader", 4, tableFlags))
			return;

		ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 20.0f);
		ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, 80.0f);
		ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 80.0f);
		ImGui.TableHeadersRow();
		ImGui.EndTable();

		foreach (KeyValuePair<uint, List<LootItem>> kvp in plugin.LootManager.Loot) {
			items = kvp.Value;
			if (items == null)
				continue;

			DrawZoneSection(kvp.Key, items);
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

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		ComfyLoot.Log.Verbose("[MainWindow] Disposing UI");
		/* nothing to clean */
	}
}
