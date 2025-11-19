/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Lumina.Excel.Sheets;

namespace ComfyLoot.Windows;

public class ConfigWindow : Window, IDisposable
{
	private string ignoredItemNewEntry = "";

	private readonly ComfyLoot plugin;
	private readonly Configuration Configuration;

	/// <summary>
	/// ConfigWindow:ctor
	/// </summary>
	/// <param name="plugin">ComfyLoot plugin instance</param>
	public ConfigWindow(ComfyLoot plugin) 
		: base("ComfyLoot config###comfyloot_config_ui")
	{
		SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(600, 400),
			MaximumSize = new Vector2(600, 2000),
		};

		SizeCondition = ImGuiCond.Always;
		Configuration = plugin.Configuration;

		this.plugin = plugin;
	}

	public override void
	PreDraw()
	{
		/* Flags must be added or removed before Draw() is being called, or they won't apply */
		if (Configuration.IsConfigWindowMovable)
			Flags &= ~ImGuiWindowFlags.NoMove;
		else
			Flags |= ImGuiWindowFlags.NoMove;
	}

	public override void
	Draw()
	{
		uint itemIdBuffer;

		bool universalis;
		bool serverinfo;
		bool serverinfoDisplayChanged;
		int serverinfoDisplayOption;
		List<uint> ignoredItemIds;

		/* Can't ref a property, so use a local copy */
		universalis = Configuration.UniversalisEnabled;
		serverinfo = Configuration.ShowDtrBar;
		serverinfoDisplayOption = Configuration.DtrBarOption;
		ignoredItemIds = Configuration.IgnoredItemIds;

		ImGui.TextColored(ImGuiColors.DalamudRed, "Read this!!!");
		ImGui.TextColoredWrapped(ImGuiColors.DalamudYellow, "Ugh, another conscent thingy. We hate them too, but apparently it's the law. If you enable this, your ip, homeworld, and items you picked up will be sent to Universalis. We don't know what they will do with this data.");
		ImGui.TextColored(ImGuiColors.DalamudYellow, "Click 'Enable' so we can all pretend this mattered.");

		if (ImGui.Checkbox("Enable Universalis", ref universalis)) {
			Configuration.UniversalisEnabled = universalis;
			Configuration.Save();
			ComfyLoot.Log.Debug("[CONFIG) Univeralis enabled {univeralis}", universalis);
		}

		ImGui.Separator();

		int removeIndex = -1;
		ImGui.TextUnformatted("Ignored items");
		if (ImGui.BeginTable("IgnoredItemIdsTable", 2, ImGuiTableFlags.SizingStretchProp)) {

			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 25.0f);

			for (int i = 0; i < ignoredItemIds.Count; i++) {
				ImGui.PushID(i);

				ImGui.TableNextRow();
				ImGui.TableNextColumn();
				ImGui.SetNextItemWidth(-1);

				string value = ItemUtil.GetItemName(ignoredItemIds[i]).ToString();
				if (ImGui.InputText("##ItemId", ref value, 16, ImGuiInputTextFlags.CharsDecimal)) {
					if (uint.TryParse(value, out uint parsed)) {
						ignoredItemIds[i] = parsed;
						Configuration.Save();
					}
				}

				ImGui.TableNextColumn();

				if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
					removeIndex = i;

				ImGui.PopID();
			}

			if (removeIndex >= 0) {
				ignoredItemIds.RemoveAt(removeIndex);
				Configuration.Save();
			}

			ImGui.PushID("AddNew");

			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			ImGui.SetNextItemWidth(-1);

			// Use persistent buffer!
			ImGui.InputText("##NewItemId", ref ignoredItemNewEntry, 64);

			ImGui.TableNextColumn();

			if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
				if (!string.IsNullOrWhiteSpace(ignoredItemNewEntry)) {
					uint baseId = Util.GetItemBaseId(ignoredItemNewEntry);

					if (baseId != 0) {
						ignoredItemIds.Add(baseId);
						Configuration.Save();
						ComfyLoot.Log.Verbose("Ignoring item {Name} -> BaseId={BaseId}", ignoredItemNewEntry, baseId);
						ignoredItemNewEntry = ""; // clear after adding
					} else {
						ComfyLoot.Log.Warning("Unknown item name: {Name}", ignoredItemNewEntry);
					}
				}
			}

			ImGui.PopID();
			ImGui.EndTable();
		}

		ImGui.Separator();

		if (ImGui.Checkbox("Enable Server Info bar entry", ref serverinfo)) {
			Configuration.ShowDtrBar = serverinfo;
			plugin.UpdateDtrBar();
			Configuration.Save();
			ComfyLoot.Log.Debug("[CONFIG) DTR enabled {serverinfo}", serverinfo);
		}

		if (!serverinfo)
			ImGui.BeginDisabled();

		serverinfoDisplayChanged = false;

		if (ImGui.RadioButton("Total items", ref serverinfoDisplayOption, 0))
			serverinfoDisplayChanged = true;
		if (ImGui.RadioButton("Items per current zone", ref serverinfoDisplayOption, 1))
			serverinfoDisplayChanged = true;
		if (ImGui.RadioButton("Total value", ref serverinfoDisplayOption, 2))
			serverinfoDisplayChanged = true;
		if (ImGui.RadioButton("Value per current zone", ref serverinfoDisplayOption, 3))
			serverinfoDisplayChanged = true;

		if (serverinfoDisplayChanged) {
			Configuration.DtrBarOption = serverinfoDisplayOption;
			plugin.UpdateDtrBar();
			Configuration.Save();
			ComfyLoot.Log.Debug("[CONFIG) DTR setting {serverinfoDisplayOption}", serverinfoDisplayOption);
		}

		if (!serverinfo)
			ImGui.EndDisabled();
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
		ComfyLoot.Log.Verbose("[ConfigWindow] Disposing UI");

		/* Cleanup */
	}
}   
