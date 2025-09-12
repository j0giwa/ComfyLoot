
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

namespace ComfyLoot.Windows;

public class MainWindow : Window, IDisposable {

	private ComfyLoot Plugin;

	private IDataManager dataManager;

	/// <summary>
	/// MainWindow:ctor
	/// </summary>
	/// <param name="plugin"></param>
	public MainWindow(ComfyLoot plugin, IDataManager dataManager)
		: base("ComfyLoot", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(375, 330),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};

		Plugin = plugin;
		dataManager = dataManager;

		/* SMELL: Likely originated from Epsteinsync */
		TitleBarButtons = new() {
			new TitleBarButton() {
				Icon = FontAwesomeIcon.Cog,
				Click = (msg) => {
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
		ImGuiTableFlags tableFlags;


		ImGui.TextUnformatted($"Total count: {Plugin.LootManager.GetTotalItemQuantity()}");
		ImGui.TextUnformatted($"Total Value: N/A");
		ImGui.Spacing();

		using (var child = ImRaii.Child("LootChild", Vector2.Zero, true)) {
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

				foreach (var kvp in Plugin.LootManager.Loot) {
					string zoneName = kvp.Key ?? "<Unknown Zone>";
					List<LootItem> items = kvp.Value ?? new();

					/* Pretty subheader */
					ImGui.TableNextRow();
					var headerBg = ImGui.GetColorU32(ImGuiCol.TableHeaderBg);
					for (int col = 0; col < 4; col++)
						ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, headerBg, ImGui.TableGetRowIndex());
					ImGui.TableSetColumnIndex(0);
					ImGui.PushID(zoneName);
					bool zoneOpen = ImGui.TreeNodeEx("##zone",
						ImGuiTreeNodeFlags.FramePadding |
						ImGuiTreeNodeFlags.DefaultOpen |
						ImGuiTreeNodeFlags.SpanAvailWidth);
					ImGui.PopID();

					ImGui.TableSetColumnIndex(1);
					ImGui.TextUnformatted(zoneName);
					ImGui.TableSetColumnIndex(2);
					ImGui.TableSetColumnIndex(3);

					// --- Items ---
					if (zoneOpen) {
						foreach (var item in items) {
							ImGui.TableNextRow();

							ImGui.TableSetColumnIndex(0); 
							ImGui.TextUnformatted(""); /* empty for indentation */

							ImGui.TableSetColumnIndex(1);
							var itemSeString = ItemUtil.GetItemName(item.ItemId, true);
							ImGui.TextUnformatted(itemSeString.ToString());

							ImGui.TableSetColumnIndex(2);
							ImGui.TextUnformatted(item.Quantity.ToString());

							ImGui.TableSetColumnIndex(3);
							ImGui.TextUnformatted("N/A");
						}

						ImGui.TreePop();
					}
				}

				ImGui.EndTable();
			}
		}
	}

	public void
	Dispose()
	{ }
}
