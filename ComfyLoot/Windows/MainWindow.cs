/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Lumina.Text.ReadOnly;

using ComfyLoot.Managers;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Lumina.Excel.Sheets;
using Lumina.Excel;

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

		/* WARN:
		 * This snippet likely originated from an Epsteinsync
		 * cba to trace it's origins, assume yes
		 */
		TitleBarButtons = new() {
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

	public override void
	Draw()
	{
		int totalValue;
		string zoneName;
		ImGuiTableFlags tableFlags;
		List<LootItem> items;

		totalValue = plugin.LootManager.GetTotalItemValue();

		ImGui.TextUnformatted($"Total count: {plugin.LootManager.GetTotalItemQuantity()}");
		ImGui.SameLine();
		ImGui.TextDisabled("(?)"); 
		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
			ImGui.TextUnformatted("Only traditional items are counted. Currencys such as Gil, Scrips or Tomestones are ignored");
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}

		if (totalValue == 0)
			ImGui.TextUnformatted($"Total Value: N/A");
		else
			ImGui.TextUnformatted($"Total Value: {totalValue}");
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
				zoneName = kvp.Key ?? "<Unknown Zone>";
				items = kvp.Value ?? new List<LootItem>();

				DrawZoneSection(zoneName, items);
			}

			ImGui.EndTable();
		}
	}

	/// <summary>
	/// Draws a zone header row and its item list as subtables.d
	/// </summary>
	private void
	DrawZoneSection(string zone, List<LootItem> items)
	{
		bool zoneOpen;
		uint headerBg;

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

		ImGui.TableNextColumn();
		ImGui.TextUnformatted(zone);
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(loot.GetZoneItemQuantity(zone).ToString());
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(loot.GetZoneItemValue(zone).ToString());

		if (!zoneOpen)
			return;

		foreach (LootItem item in items)
			DrawItemRow(item);

		ImGui.TreePop();
	}

	/// <summary>
	/// Draws a single loot item row inside a zone.
	/// </summary>
	private static void
	DrawItemRow(LootItem item)
	{
		byte rarity;
		ReadOnlySeString itemName;

		rarity = Util.GetRarity(item.ItemId); /* Might be better to store in struct */

		ImGui.TableNextRow();
		ImGui.TableSetColumnIndex(0);
		DrawIcon(item.ItemId);

		ImGui.TableNextColumn();
		itemName = ItemUtil.GetItemName(item.ItemId, true);
		/* TODO: switch to dalamud themeing */
		switch (rarity) {
		case 1: /* Common (white) */
			ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), itemName.ToString());
			break;
		case 2: /* Uncommon (green, the best color) */
			ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), itemName.ToString());
			break;
		case 3: /* Rare (blue) */
			ImGui.TextColored(new Vector4(0.2f, 0.5f, 1.0f, 1.0f), itemName.ToString());
			break;
		case 4: /* Relic (purple) */
			ImGui.TextColored(new Vector4(0.64f, 0.21f, 0.93f, 1.0f), itemName.ToString());
			break;
		case 7: /* Aetherial (pink) */
			ImGui.TextColored(new Vector4(0.95f, 0.68f, 0.95f, 1.0f), itemName.ToString());
			break;
		default: /* Default (gray) */
			ImGui.TextUnformatted(itemName.ToString());
			break;
		}

		ImGui.TableNextColumn();
		ImGui.TextUnformatted(item.Quantity.ToString());

		ImGui.TableNextColumn();
		if (item.Value == 0)
			ImGui.TextUnformatted("N/A");
		else
			ImGui.TextUnformatted((item.Value * item.Quantity).ToString());
	}

	private static void 
	DrawIcon(uint itemId)
	{
		Vector2 iconSize = new Vector2(20, 20);
		GameIconLookup lookup;
		ISharedImmediateTexture? sharedTexture;
		ExcelSheet<Item> itemSheet;
		Item? luminaitem;

		itemSheet = ComfyLoot.DataManager.GetExcelSheet<Item>();
		luminaitem = itemSheet?.GetRow(itemId);

		if (luminaitem == null) {
			ImGui.TextUnformatted("");
			return;
		}

		lookup = new GameIconLookup(luminaitem.Value.Icon);

		if (!ComfyLoot.Textures.TryGetFromGameIcon(in lookup, out sharedTexture)
		|| sharedTexture == null) {
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

	public void
	Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void
	Dispose(bool disposing)
	{
		/* Cleanup */
	}
}