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

/// <summary>
/// Configuration UI
/// </summary>
public class ConfigWindow : Window, IDisposable {

	private string ignoredItemNewEntry = "";
	private string ignoredZoneNewEntry = "";

	private readonly ComfyLoot plugin;
	private readonly Configuration Configuration;

	public ConfigWindow(ComfyLoot plugin)
		: base("ComfyLoot config###comfyloot_config_ui")
	{
		SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(600, 500),
			MaximumSize = new Vector2(600, 2000),
		};

		SizeCondition = ImGuiCond.Always;
		Configuration = plugin.Configuration;

		this.plugin = plugin;
	}

	///
	///
	///
	private void
	DrawResolvedIdAdd(string widgetId, ref string newEntry,  List<uint> list, Action<List<uint>> addAction)
	{
		ImGui.PushID(widgetId);

		ImGui.TableNextRow();
		ImGui.TableNextColumn();
		ImGui.SetNextItemWidth(-1);

		ImGui.InputText("##New", ref newEntry, 64);

		ImGui.TableNextColumn();
		if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
			addAction(list);
		ImGui.PopID();
	}

	///
	///
	///
	private int
	DrawResolvedIdList(string widgetId, List<uint> idList, Func<uint, string> nameResovler)
	{
		int removeIndex = -1;
		string text;
		uint parsed;

		for (int i = 0; i < idList.Count; i++) {
			ImGui.PushID(i);

			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			ImGui.SetNextItemWidth(-1);

			text = nameResovler(idList[i]);
			if (ImGui.InputText(widgetId, ref text, 16, ImGuiInputTextFlags.CharsDecimal))
				if (uint.TryParse(text, out parsed))
					if (idList[i] != parsed) {
						idList[i] = parsed;
						Configuration.Save();
					}

			ImGui.TableNextColumn();
			if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
				removeIndex = i;

			ImGui.PopID();
		}

		return removeIndex;
	}

	public override void
	Draw()
	{
		bool universalis;
		bool serverinfo;
		int serverinfoDisplayOption;
		List<uint> ignoredItemIds;
		List<uint> ignoredZoneIds;

		universalis = Configuration.UniversalisEnabled;
		serverinfo = Configuration.ShowDtrBar;
		serverinfoDisplayOption = Configuration.DtrBarOption;
		ignoredItemIds = Configuration.IgnoredItemIds;
		ignoredZoneIds = Configuration.IgnoredZoneIds;

		if (ImGui.TreeNodeEx("Ignored Entrys")) {
			DrawZoneIgnorelist(ignoredZoneIds);
			DrawItemIgnorelist(ignoredItemIds);
		}

		ImGui.Separator();

		DrawUniversalisSection(ref universalis);
		DrawDtrSection(ref serverinfo, ref serverinfoDisplayOption);
	}

	private void 
	DrawUniversalisSection(ref bool universalis)
	{
		bool opened;

		if (universalis) {
			if (ImGui.Checkbox("Enable Universalis", ref universalis)) {
				Configuration.UniversalisEnabled = universalis;
				Configuration.Save();
				ComfyLoot.Log.Debug("[CONFIG] Universalis enabled {universalis}", universalis);
			}
			return;
		}

		opened = ImGui.TreeNodeEx("Read this!!!");
		if (opened) {
			ImGui.TextColored(ImGuiColors.DalamudRed, "Universalis Consent & Warning");

			ImGui.TextColoredWrapped(
				ImGuiColors.DalamudYellow,
				"Ugh, another consent thingy. We hate them too, but apparently it's the law. " +
				"If you enable this, your IP, homeworld, and items you picked up will be sent to Universalis. " +
				"We don't know what they will do with this data."
			);

			ImGui.TextColored(
				ImGuiColors.DalamudYellow,
				"Click 'Enable' so we can all pretend this mattered."
			);

			ImGui.Spacing();
			ImGui.TreePop();
		}

		ImGui.BeginDisabled(!opened);

		if (ImGui.Checkbox("Enable Universalis", ref universalis)) {
			Configuration.UniversalisEnabled = universalis;
			Configuration.Save();
			ComfyLoot.Log.Debug("[CONFIG] Universalis enabled {universalis}", universalis);
		}

		ImGui.EndDisabled();
	}

	/// <summary>
	/// Draws configuration options regarding the dtr bar
	/// </summary>
	private void
	DrawDtrSection(ref bool serverinfo, ref int serverinfoDisplayOption)
	{
		if (ImGui.Checkbox("Enable Server Info bar entry", ref serverinfo)) {
			Configuration.ShowDtrBar = serverinfo;
			plugin.UpdateDtrBar();
			Configuration.Save();
			ComfyLoot.Log.Debug("[CONFIG) DTR enabled {serverinfo}", serverinfo);
		}

		if (!serverinfo)
			ImGui.BeginDisabled();

		bool serverinfoDisplayChanged = false;

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

	/// <summary>
	/// Draws an item ignorelist
	/// </summary>
	private void
	DrawItemIgnorelist(List<uint> ignoredItemIds)
	{
		ImGui.TextUnformatted("Ignored items");
		if (!ImGui.BeginTable("IgnoredItemIdsTable", 2, ImGuiTableFlags.SizingStretchProp))
			return;

		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 25.0f);

		int removeIndex = DrawResolvedIdList(
			"##ItemId",
			ignoredItemIds,
			id => ItemUtil.GetItemName(id).ToString()
		);

		if (removeIndex >= 0)
			RemoveItem(ignoredItemIds, removeIndex);

		DrawResolvedIdAdd(
			"##AddNewItem",
			ref ignoredItemNewEntry,
			ignoredItemIds,
			TryAddIgnoredItem
		);

		ImGui.EndTable();
	}

	/// <summary>
	/// Draws an item ignorelist
	/// </summary>
	private void
	DrawZoneIgnorelist(List<uint> ignoredZoneIds)
	{
		ImGui.TextUnformatted("Ignored zones");
		if (!ImGui.BeginTable("IgnoredZonesTable", 2, ImGuiTableFlags.SizingStretchProp))
			return;

		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 25.0f);

		int removeIndex = DrawResolvedIdList(
			"##Zone",
			ignoredZoneIds,
			id => Util.GetZoneName(id)
		);

		if (removeIndex >= 0)
			RemoveZone(ignoredZoneIds, removeIndex);

		DrawResolvedIdAdd(
			"##AddNewZone",
			ref ignoredZoneNewEntry,
			ignoredZoneIds,
			TryAddIgnoredZone
		);

		ImGui.EndTable();
	}

	/// <summary>
	/// removes a zone from the zone ignorelist
	/// </summary>
	private void
	RemoveZone(List<uint> zones, int index)
	{
		zones.RemoveAt(index);
		Configuration.Save();
	}

	/// <summary>
	/// removes a item from the zone ignorelist
	/// </summary>
	private void
	RemoveItem(List<uint> list, int index)
	{
		list.RemoveAt(index);
		Configuration.Save();
	}

	/// <summary>
	/// attempts to add an item to the ignorelist
	/// </summary>
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

	/// <summary>
	/// attempts to add an zone to the ignorelist
	/// </summary>
	private void
	TryAddIgnoredZone(List<uint> ignoredZoneIds)
	{
		uint baseId;

		if (string.IsNullOrWhiteSpace(ignoredZoneNewEntry))
			return;

		baseId = Util.GetZoneId(ignoredZoneNewEntry);
		if (baseId == 0) {
			ComfyLoot.Log.Warning("Unknown zone name: {Name}", ignoredZoneNewEntry);
			return;
		}

		ignoredZoneIds.Add(baseId);
		Configuration.Save();

		ComfyLoot.Log.Verbose("Ignoring zone {Name} -> BaseId={BaseId}", ignoredZoneNewEntry, baseId);
		ignoredZoneNewEntry = "";
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
	}
}