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

namespace ComfyLoot.Windows;

public class ConfigWindow : Window, IDisposable
{
	private string ignoredItemNewEntry = "";
	private string ignoredZoneNewEntry = "";

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

/* TODO: flawed implementation */
#region ignoreListHelpers
	private int 
	DrawRowsString(List<string> list, string widgetIdPrefix)
	{
		int removeIndex = -1;
		string value;

		for (int i = 0; i < list.Count; i++) {
			ImGui.PushID(i);

			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			ImGui.SetNextItemWidth(-1);

			value = list[i];
			if (ImGui.InputText(widgetIdPrefix, ref value, 64)) {
				list[i] = value;
				Configuration.Save();
			}

			ImGui.TableNextColumn();
			if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
				removeIndex = i;

			ImGui.PopID();
		}

		return removeIndex;
	}

	private int
	DrawRowsUint(List<uint> list, string widgetIdPrefix)
	{
		int removeIndex = -1;
		string text;
		uint parsed;

		for (int i = 0; i < list.Count; i++) {
			ImGui.PushID(i);

			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			ImGui.SetNextItemWidth(-1);

			text = ItemUtil.GetItemName(list[i]).ToString();
			if (ImGui.InputText(widgetIdPrefix, ref text, 16, ImGuiInputTextFlags.CharsDecimal)) {
				if (uint.TryParse(text, out parsed)) {
					if (list[i] != parsed) {
						list[i] = parsed;
						Configuration.Save();
					}
				}
			}

			ImGui.TableNextColumn();
			if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
				removeIndex = i;

			ImGui.PopID();
		}

		return removeIndex;
	}

	private void 
	DrawAddRowString(ref string newEntry, string widgetId, List<string> list)
	{
		ImGui.PushID(widgetId);

		ImGui.TableNextRow();
		ImGui.TableNextColumn();
		ImGui.SetNextItemWidth(-1);

		ImGui.InputText("##New", ref newEntry, 64);

		ImGui.TableNextColumn();
		if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
			if (!string.IsNullOrWhiteSpace(newEntry)) {
				list.Add(newEntry.Trim());
				Configuration.Save();
				newEntry = "";
			}
		}

		ImGui.PopID();
	}

	private void 
	DrawAddRowUint(ref string newEntry, string widgetId, List<uint> list)
	{
		ImGui.PushID(widgetId);

		ImGui.TableNextRow();
		ImGui.TableNextColumn();
		ImGui.SetNextItemWidth(-1);

		ImGui.InputText("##New", ref newEntry, 64);

		ImGui.TableNextColumn();
		if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
			TryAddIgnoredItem(list);

		ImGui.PopID();
	}
#endregion

	private void 
	TryAddIgnoredItem(List<uint> ignoredItemIds)
	{
		uint baseId;

		if (string.IsNullOrWhiteSpace(ignoredItemNewEntry))
			return;

		baseId = Util.GetItemBaseId(ignoredItemNewEntry);
		if (baseId == 0) {
			ComfyLoot.Log.Warning("Unknown item name: {Name}", ignoredItemNewEntry);
			return;
		}

		ignoredItemIds.Add(baseId);
		Configuration.Save();

		ComfyLoot.Log.Verbose("Ignoring item {Name} -> BaseId={BaseId}", ignoredItemNewEntry, baseId);
		ignoredItemNewEntry = "";
	}

	private int
	DrawItemRows(List<uint> ignoredItemIds) => DrawRowsUint(ignoredItemIds, "##ItemId");

	private void
	DrawNewItemRow(List<uint> ignoredItemIds) => DrawAddRowUint(ref ignoredItemNewEntry, "AddNewItem", ignoredItemIds);

	private int 
	DrawZoneRows(List<string> zones) => DrawRowsString(zones, "##Zone");

	private void 
	DrawNewZoneRow(List<string> zones) => DrawAddRowString(ref ignoredZoneNewEntry, "AddNewZone", zones);

	private void 
	RemoveZone(List<string> zones, int index)
	{
		zones.RemoveAt(index);
		Configuration.Save();
	}

	private void
	RemoveItem(List<uint> list, int index)
	{
		list.RemoveAt(index);
		Configuration.Save();
	}

	public override void
	Draw()
	{
		bool universalis;
		bool serverinfo;
		bool serverinfoDisplayChanged;
		int serverinfoDisplayOption;
		List<uint> ignoredItemIds;
		List<string> ignoredZones;

		/* Can't ref a property, so use a local copy */
		universalis = Configuration.UniversalisEnabled;
		serverinfo = Configuration.ShowDtrBar;
		serverinfoDisplayOption = Configuration.DtrBarOption;
		ignoredItemIds = Configuration.IgnoredItemIds;
		ignoredZones = Configuration.IgnoredZones;

		ImGui.TextColored(ImGuiColors.DalamudRed, "Read this!!!");
		ImGui.TextColoredWrapped(ImGuiColors.DalamudYellow, "Ugh, another conscent thingy. We hate them too, but apparently it's the law. If you enable this, your ip, homeworld, and items you picked up will be sent to Universalis. We don't know what they will do with this data.");
		ImGui.TextColored(ImGuiColors.DalamudYellow, "Click 'Enable' so we can all pretend this mattered.");

		if (ImGui.Checkbox("Enable Universalis", ref universalis)) {
			Configuration.UniversalisEnabled = universalis;
			Configuration.Save();
			ComfyLoot.Log.Debug("[CONFIG) Univeralis enabled {univeralis}", universalis);
		}

		ImGui.Separator();
		DrawIgnoredZoneList(ignoredZones);
		DrawItemIgnoreList(ignoredItemIds);
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

	private void
	DrawIgnoredZoneList(List<string> ignoredZones)
	{
		ImGui.TextUnformatted("Ignored zones");
		if (!ImGui.BeginTable("IgnoredZonesTable", 2, ImGuiTableFlags.SizingStretchProp))
			return;

		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 25.0f);

		int removeIndex = DrawZoneRows(ignoredZones);
		if (removeIndex >= 0)
			RemoveZone(ignoredZones, removeIndex);

		DrawNewZoneRow(ignoredZones);

		ImGui.EndTable();
	}

	private void 
	DrawItemIgnoreList(List<uint> ignoredItemIds)
	{
		ImGui.TextUnformatted("Ignored items");
		if (!ImGui.BeginTable("IgnoredItemIdsTable", 2, ImGuiTableFlags.SizingStretchProp))
			return;

		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 25.0f);

		int removeIndex = DrawItemRows(ignoredItemIds);
		if (removeIndex >= 0)
			RemoveItem(ignoredItemIds, removeIndex);

		DrawNewItemRow(ignoredItemIds);

		ImGui.EndTable();
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

		/* nothing to Cleanup */
	}
}   
