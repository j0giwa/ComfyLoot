
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

using ComfyLoot.Managers;
using Lumina.Text.ReadOnly;

namespace ComfyLoot.Windows;

public class MainWindow : Window, IDisposable {

	private ComfyLoot Plugin;

	//private IDataManager dataManager;

	/// <summary>
	/// MainWindow:ctor
	/// </summary>
	/// <param name="plugin"></param>
	public MainWindow(ComfyLoot plugin)
		: base("ComfyLoot", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(375, 330),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};

		Plugin = plugin;

		/* WARN: 
		 * This snippet likely originated from an Epsteinsync
		 * Cba to trace it's origins, assume yes
		 */
		TitleBarButtons = new() {
			new TitleBarButton() {
				Icon = FontAwesomeIcon.Cog,
				Click = (msg) => {
		    			/* NOTE: No configs yet */
					//Plugin.ToggleConfigUI();
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
		string zoneName;
		ImGuiTableFlags tableFlags;
		List<LootItem> items;
		
		ImGui.TextUnformatted($"Total count: {Plugin.LootManager.GetTotalItemQuantity()}");
		ImGui.TextUnformatted($"Total Value: N/A");
		ImGui.Spacing();

		using var child = ImRaii.Child("LootChild", Vector2.Zero, true);
		if (!child.Success)
			return;

		tableFlags = ImGuiTableFlags.RowBg |
			ImGuiTableFlags.BordersInnerV |
			ImGuiTableFlags.BordersInnerH |
			ImGuiTableFlags.SizingStretchProp |
			ImGuiTableFlags.ScrollY;
			
		if (ImGui.BeginTable("LootTable", 4, tableFlags)) {

			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 35.0f);
			ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, 80.0f);
			ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 80.0f);

			ImGui.TableHeadersRow();
			foreach (KeyValuePair<string, List<LootItem>> kvp in Plugin.LootManager.Loot) {
				zoneName = kvp.Key ?? "<Unknown Zone>";
				items = kvp.Value ?? new List<LootItem>();

				DrawZoneSection( $"zoneName {kvp.Value.Count} ", items);
			}

			ImGui.EndTable();
		}
	}

	/// <summary>
	/// Draws a zone header row and its item list as subtables.
	/// </summary>
	private void
	DrawZoneSection(string zoneName, List<LootItem> items)
	{
		bool zoneOpen;
		uint headerBg;

		ImGui.TableNextRow();
		headerBg = ImGui.GetColorU32(ImGuiCol.TableHeaderBg);

		for (int col = 0; col < 4; col++)
			ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, headerBg, ImGui.TableGetRowIndex());

		ImGui.TableSetColumnIndex(0);
		ImGui.PushID(zoneName);
		zoneOpen = ImGui.TreeNodeEx("##zone",
			ImGuiTreeNodeFlags.FramePadding |
			ImGuiTreeNodeFlags.DefaultOpen |
			ImGuiTreeNodeFlags.SpanAvailWidth);

		ImGui.PopID();
		ImGui.TableSetColumnIndex(1);
		ImGui.TextUnformatted(zoneName);
		ImGui.TableSetColumnIndex(2);
		ImGui.TableSetColumnIndex(3);

		if (!zoneOpen)
			return;

		foreach (LootItem item in items)
			DrawItemRow(item);

		ImGui.TreePop();
	}

	/// <summary>
	/// Draws a single loot item row inside a zone.
	/// </summary>
	private void
	DrawItemRow(LootItem item)
	{
		int GetItemValue;
		ReadOnlySeString itemName;

		ImGui.TableNextRow();
		ImGui.TableSetColumnIndex(0);
		ImGui.TextUnformatted(""); // indentation placeholder

		ImGui.TableSetColumnIndex(1);
		itemName = ItemUtil.GetItemName(item.ItemId, true);
		ImGui.TextUnformatted(itemName.ToString());

		ImGui.TableSetColumnIndex(2);
		ImGui.TextUnformatted(item.Quantity.ToString());

		ImGui.TableSetColumnIndex(3);
		if (item.Value == 0)
			ImGui.TextUnformatted("N/A");
		else
			ImGui.TextUnformatted(item.Value.ToString());
	}

	public void
	Dispose()
	{ }
}
