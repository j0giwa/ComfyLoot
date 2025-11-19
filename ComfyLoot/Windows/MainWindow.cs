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
using Lumina.Excel;
using Lumina.Text.ReadOnly;

using ComfyLoot.Managers;
using FFXIVClientStructs;
using System.Threading.Tasks;

namespace ComfyLoot.Windows;

public class MainWindow : Window, IDisposable {

	private readonly ComfyLoot plugin;
	private readonly LootManager loot;

	/// <summary>
	/// MainWindow:ctor
	/// </summary>
	/// <param name="plugin">ComfyLoot plugin instance</param>
	public MainWindow(ComfyLoot plugin, LootManager loot)
		: base("ComfyLoot###comfyloot_ui", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(375, 330),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};

		this.plugin = plugin;
		this.loot = loot;
		
		TitleBarButtons = new() {
#if DEBUG
			new TitleBarButton() {
				Icon = FontAwesomeIcon.Code,
				Click = async (msg) => {
					await Populate(this.loot);
				},
				IconOffset = new(2,1),
				ShowTooltip = () => {
					ImGui.BeginTooltip();
					ImGui.Text("Debug Populate");
					ImGui.EndTooltip();
				}
			},
#endif //* DEBUG*/

			/* WARN:
		         * This snippet likely originated from an Epsteinsync
			 * cba to trace it's origins, assume yes
		 	 */
			new TitleBarButton() {
				Icon = FontAwesomeIcon.Cog,
				Click = (msg) => {
					this.plugin.ToggleConfigUI();
				},
				IconOffset = new(2,1),
				ShowTooltip = () => {
					ImGui.BeginTooltip();
					ImGui.Text("Open Settings");
					ImGui.EndTooltip();
				}
			}
		};
	}

#if DEBUG
	private static void
	DrawDebugTooltip(LootItem item, ReadOnlySeString itemName)
	{
		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
			ImGui.TextColored(ImGuiColors.DalamudRed, "DEBUG");
			ImGui.Separator();
			ImGui.TextUnformatted($"Item: {itemName}");
			ImGui.TextUnformatted($"Id: {item.ItemId}");
			ImGui.TextUnformatted($"BaseId: {Util.GetBaseId(item.ItemId)}");
			ImGui.TextUnformatted($"Rarity: {item.Rarity}");
			ImGui.TextUnformatted($"Tradable: {Util.IsTradable(item.ItemId)}");
			ImGui.TextUnformatted($"IsCurrency: {Util.IsCurrency(item.ItemId)}");
			ImGui.TextUnformatted($"Value: {item.Value}");
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
	}

	private static  async Task
	Populate(LootManager loot)
	{
		const string fakezonename = "Ligma Lominsa";

		await loot.AddItem(
			id: 1, /* gil */
			quantity: 1000000,
			zoneName: fakezonename,
			hq: false
		);
		await loot.AddItem(
			id: 14, /* fire cluster */
			quantity: 999,
			zoneName: fakezonename,
			hq: false
		);
		await loot.AddItem(
			id: 1046003, /* mate cookie (hq) */
			quantity: 99,
			zoneName: fakezonename,
			hq: true
		);
		await loot.AddItem(
			id: 2791, /* aetherial mythril circlet (rubellite) */
			quantity: 1,
			zoneName: fakezonename,
			hq: false
		);
		await loot.AddItem(
			id: 3035, /* acolyte's robe */
			quantity: 1,
			zoneName: fakezonename,
			hq: true
		);
		await loot.AddItem(
			id: 32418,/* cryptlurker sword */
			quantity: 1,
			zoneName: fakezonename,
			hq: false
		);
		await loot.AddItem(
			id: 33475, /* blade's fealty */
			quantity: 1,
			zoneName: fakezonename,
			hq: false
		);
	}
#endif //* DEBUG*/

	/// <summary>
	/// gets an items icon
	/// </summary>
	/// <returns>
	/// Item icon texture
	/// </returns>
	private static ISharedImmediateTexture?
	GetIcon(uint itemId)
	{
		GameIconLookup lookup;
		ISharedImmediateTexture? sharedTexture;
		ExcelSheet<Item> items;
		Item item;

		items = ComfyLoot.DataManager.GetExcelSheet<Item>();
		if (items == null) {
			ComfyLoot.Log.Fatal("[Lumina] Cannot determine Icon, Item-sheet can not be resolved");
			return null;
		}

		if (!items.TryGetRow(itemId, out item)) {
			return null;
		}

		lookup = new GameIconLookup(item.Icon);
		if (!ComfyLoot.Textures.TryGetFromGameIcon(in lookup, out sharedTexture)
		|| sharedTexture == null) {
			return null;
		}

		return sharedTexture;
	}

	/// <summary>
	/// Draws the item counter
	/// </summary>
	private void
	DrawItemCounter()
	{
		ImGui.TextUnformatted($"Total count: {plugin.LootManager.GetTotalItemQuantity()}");

		ImGui.SameLine();

		using (ImRaii.PushFont(UiBuilder.IconFont)) {
			ImGui.TextDisabled($"{FontAwesomeIcon.QuestionCircle.ToIconString()}");
		}

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
	/// draws a items icon
	/// </summary>
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
	/// Draws a single loot item row inside a zone.
	/// </summary>
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
		case 1: /* Common (white) */
			ImGui.TextColored(ImGuiColors.DalamudWhite, itemName.ToString());
			break;
		case 2: /* Uncommon (green, the best color) */
			ImGui.TextColored(ImGuiColors.ParsedGreen, itemName.ToString());
			break;
		case 3: /* Rare (blue) */
			ImGui.TextColored(ImGuiColors.ParsedBlue, itemName.ToString());
			break;
		case 4: /* Relic (purple) */
			ImGui.TextColored(ImGuiColors.ParsedPurple, itemName.ToString());
			break;
		case 7: /* Aetherial (pink) */
			ImGui.TextColored(ImGuiColors.ParsedPink, itemName.ToString());
			break;
		default: /* Default (gray) */
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

#if DEBUG
		DrawDebugTooltip(item, itemName); /* PERF: rather slow, debug info only */
#endif //* DEBUG */
		ImGui.PopID();

		ImGui.TableNextColumn();
		ImGui.TextUnformatted(Util.FormatNumber(item.Quantity));

		ImGui.TableNextColumn();
		if (item.Value == 0)
			ImGui.TextUnformatted("N/A");
		else
			ImGui.TextUnformatted(Util.FormatGil(item.Value * item.Quantity));
	}

	/// <summary>
	/// Draws the accumulated value display
	/// </summary>
	private static void
	DrawValueDisplay(int totalValue)
	{
		ImGui.TextUnformatted($"Total Value: {Util.FormatGil(totalValue)}");
		ImGui.SameLine();

		using (ImRaii.PushFont(UiBuilder.IconFont)) {
			ImGui.TextDisabled($"{FontAwesomeIcon.QuestionCircle.ToIconString()}");
		}

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
	/// Draws a zone header row and its item list as subtables.
	/// </summary>
	private void
	DrawZoneSection(string zone, List<LootItem> items)
	{
		int itemCount;
		int itemValue;
		uint headerBg;
		bool zoneOpen;

		ImGui.TableNextRow();
		headerBg = ImGui.GetColorU32(ImGuiCol.Tab);

		for (int col = 0; col < 4; col++)
			ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, headerBg, ImGui.TableGetRowIndex());

		ImGui.TableSetColumnIndex(0);
		ImGui.PushID(zone);

		zoneOpen = ImGui.TreeNodeEx("##zone",
			ImGuiTreeNodeFlags.DefaultOpen |
			ImGuiTreeNodeFlags.SpanAvailWidth);
		ImGui.PopID();

		itemCount = loot.GetZoneItemQuantity(zone);
		itemValue = loot.GetZoneItemValue(zone);

		ImGui.TableNextColumn();
		ImGui.TextUnformatted(zone);
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(Util.FormatNumber(itemCount));
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(Util.FormatGil(itemValue));

		if (!zoneOpen)
			return;

		foreach (LootItem item in items)
			DrawItemRow(item);

		ImGui.TreePop();
	}

	public override void
	Draw()
	{
		string zoneName;
		ImGuiTableFlags tableFlags;
		List<LootItem> items;

		DrawItemCounter();
		DrawValueDisplay(plugin.LootManager.GetTotalItemValue());
		ImGui.Spacing();

		using var child = ImRaii.Child("LootChild###", Vector2.Zero);
		if (!child.Success)
			return;

		tableFlags = ImGuiTableFlags.RowBg |
			ImGuiTableFlags.BordersOuter |
			ImGuiTableFlags.BordersInnerV |
			ImGuiTableFlags.SizingStretchProp |
			ImGuiTableFlags.ScrollY;

		if (ImGui.BeginTable("LootTable", 4, tableFlags)) {

			ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 20.0f);
			ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, 80.0f);
			ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 80.0f);

			ImGui.TableHeadersRow();

			foreach (KeyValuePair<string, List<LootItem>> kvp in plugin.LootManager.Loot) {
				if (kvp.Key != null)
					zoneName = kvp.Key;
				else
					zoneName = "<Unknown Zone>";

				if (kvp.Value != null)
					items = kvp.Value;
				else
					items = new List<LootItem>();

				DrawZoneSection(zoneName, items);
			}

			ImGui.EndTable();
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

		/* Cleanup */
	}
}