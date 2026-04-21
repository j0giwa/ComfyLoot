/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
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
	private readonly Config Configuration;

	public ConfigWindow(ComfyLoot plugin)
		: base("ComfyLoot config###comfyloot_config_ui")
	{
		SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(260, 400),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
		};

		SizeCondition = ImGuiCond.Always;
		Configuration = plugin.Configuration;

		this.plugin = plugin;
	}

	/// <summary>
	/// Renders the config UI window.
	/// </summary>
	public override void
	Draw()
	{
		bool universalis;
		bool contextMenu;
		bool crystals;
		bool serverinfo;
		DtrBarOption dtrOption;
		List<uint> ignoredItemIds;
		List<string> ignoredZones;

		universalis = Configuration.UniversalisEnabled;
		contextMenu = Configuration.ItemContextMenu;
		crystals = Configuration.IgnoreCrystals;
		serverinfo = Configuration.ShowDtrBar;
		dtrOption = Configuration.DtrBarOption;
		ignoredItemIds = Configuration.IgnoredItemIds;
		ignoredZones = Configuration.IgnoredZones;

		if (ImGui.TreeNodeEx("Ignored Entrys")) {
			DrawZoneIgnorelist(ignoredZones);
			DrawItemIgnorelist(ignoredItemIds);
		}

		ImGui.Separator();

		DrawUniversalisSection(ref universalis);

		if (!Config.STABLE) {

			if (ImGui.Checkbox("Enable item context menu", ref contextMenu)) {
				Configuration.ItemContextMenu = contextMenu;
				Configuration.Save();
				ComfyLoot.Log.Debug("[CONFIG) ContextMenu enabled {serverinfo}", serverinfo);
			}
			ImGui.SameLine();
			DrawExperimentalTooltip();

			DrawDtrSection(ref serverinfo);
			ImGui.SameLine();
			DrawExperimentalTooltip();
			DrawDtrSubsection(ref serverinfo, ref dtrOption);
		}
	}

	/// <summary>
	/// Draws the configuration section for the Dalamud DTR (server info) bar.
	/// </summary>
	/// <param name="serverinfo">Reference to the enable flag for the DTR entry.</param>
	/// <param name="dtrOption">Reference to the selected display mode.</param>
	private void
	DrawDtrSection(ref bool serverinfo)
	{
		if (ImGui.Checkbox("Enable Server Info bar entry", ref serverinfo)) {
			Configuration.ShowDtrBar = serverinfo;
			plugin.UpdateDtrBar();
			Configuration.Save();
			ComfyLoot.Log.Debug("[CONFIG) DTR enabled {serverinfo}", serverinfo);
		}
	}

	private void
	DrawDtrSubsection(ref bool serverinfo, ref DtrBarOption dtrOption)
	{
		bool serverinfoDisplayChanged;

		if (!serverinfo)
			ImGui.BeginDisabled();

		serverinfoDisplayChanged = false;

		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 25);

		if (ImGui.RadioButton("Total items", ref dtrOption, DtrBarOption.TOTAL_QUANTITY))
			serverinfoDisplayChanged = true;

		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 25);
		if (ImGui.RadioButton("Items per current zone", ref dtrOption, DtrBarOption.ZONE_QUANTITY))
			serverinfoDisplayChanged = true;

		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 25);
		if (ImGui.RadioButton("Total value", ref dtrOption, DtrBarOption.TOTAL_VALUE))
			serverinfoDisplayChanged = true;

		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 25);
		if (ImGui.RadioButton("Value per current zone", ref dtrOption, DtrBarOption.ZONE_VALUE))
			serverinfoDisplayChanged = true;

		if (serverinfoDisplayChanged) {
			Configuration.DtrBarOption = dtrOption;
			plugin.UpdateDtrBar();
			Configuration.Save();
			ComfyLoot.Log.Debug("[CONFIG] DTR setting {serverinfoDisplayOption}", dtrOption);
		}

		if (!serverinfo)
			ImGui.EndDisabled();
	}

	private void
	DrawExperimentalTooltip()
	{
		Vector4 color = ImGuiColors.DalamudYellow;

		using (ImRaii.PushFont(UiBuilder.IconFont))
			ImGui.TextColored(color, $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()}");

		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);

			ImGui.TextUnformatted("Experimental Feature");
			ImGui.TextUnformatted("May get changed drasticly or removed");
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
	}

	/// <summary>
	/// Draws the UI table for the ignored item ID list.
	/// </summary>
	/// <param name="ignoredItemIds">The list of ignored item IDs.</param>
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
	/// Draws UI elements allowing users to add new resolved numeric IDs
	/// (item IDs or zone IDs) to an ignore list.
	/// </summary>
	/// <param name="widgetId">A unique ImGui widget ID.</param>
	/// <param name="newEntry">Reference to the text buffer where user input is stored.</param>
	/// <param name="list">The list to which the resolved ID will be added.</param>
	/// <param name="addAction">Callback used to add the entry to the list.</param>
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

	/// <summary>
	/// Draws a list of resolved numeric IDs (items or zones) that can be modified or removed.
	/// </summary>
	/// <param name="widgetId">Unique widget identifier.</param>
	/// <param name="idList">List of IDs to be displayed.</param>
	/// <param name="nameResovler">Function resolving each ID to a readable name.</param>
	/// <returns>The index of an entry flagged for removal, or -1 if none.</returns>
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

	/// <summary>
	/// Draws the Universalis settings section, including hideble concent text.
	/// </summary>
	/// <param name="universalis">Reference to the Universalis-enabled flag.</param>
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
	/// Draws a modifiable string list.
	/// </summary>
	private int
	DrawStringList(string widgetId, List<string> list)
	{
		int removeIndex = -1;

		for (int i = 0; i < list.Count; i++) {
			ImGui.PushID(i);

			ImGui.TableNextRow();
			ImGui.TableNextColumn();
			ImGui.SetNextItemWidth(-1);

			string text = list[i];

			if (ImGui.InputText(widgetId, ref text, 64))
				if (list[i] != text) {
					list[i] = text;
					Configuration.Save();
				}

			ImGui.TableNextColumn();

			if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
				removeIndex = i;

			ImGui.PopID();
		}

		return removeIndex;
	}

	/// <summary>
	/// Draws UI elements allowing users to add new string entries.
	/// </summary>
	/// <param name="ignoredZoneIds">The list of ignored zone IDs.</param>
	private void
	DrawStringAdd(string widgetId, ref string newEntry, List<string> list)
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

	/// <summary>
	/// Draws the UI table for the ignored zone list.
	/// </summary>
	private void
	DrawZoneIgnorelist(List<string> ignoredZones)
	{
		ImGui.TextUnformatted("Ignored zones");

		if (!ImGui.BeginTable("IgnoredZonesTable", 2, ImGuiTableFlags.SizingStretchProp))
			return;

		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 25.0f);

		int removeIndex = DrawStringList(
			"##Zone",
			ignoredZones
		);

		if (removeIndex >= 0)
			RemoveZone(ignoredZones, removeIndex);

		DrawStringAdd(
			"##AddNewZone",
			ref ignoredZoneNewEntry,
			ignoredZones
		);

		ImGui.EndTable();
	}

	/// <summary>
	/// Removes a zone from the ignored zone list.
	/// </summary>
	/// <param name="zones">The list of ignored zone IDs.</param>
	/// <param name="index">Index of the zone to remove.</param>
	private void
	RemoveZone(List<string> zones, int index)
	{
		zones.RemoveAt(index);
		Configuration.Save();
	}

	/// <summary>
	/// Removes an item from the ignored item list.
	/// </summary>
	/// <param name="list">The ignored item list.</param>
	/// <param name="index">Index of the item to remove.</param>
	private void
	RemoveItem(List<uint> list, int index)
	{
		list.RemoveAt(index);
		Configuration.Save();
	}

	/// <summary>
	/// Attempts to add a new item to the ignored item ID list.
	/// </summary>
	/// <param name="ignoredItemIds">The ignored item ID list.</param>
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