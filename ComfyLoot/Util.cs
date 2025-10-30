using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace ComfyLoot;

/// <summary>
/// Misc utility Functions
/// </summary>
public static class Util {

	/// <summary>
	/// retrieves the name of the characters homeworld.
	/// </summary>
	public static unsafe string
	GetHomeWorld()
	{
		uint id;
		string? name;
		ExcelSheet<World> sheet;
		World worldRow;

		name = null;
		id = AgentLobby.Instance()->LobbyData.HomeWorldId;
		sheet = ComfyLoot.DataManager.GetExcelSheet<World>();

		if (sheet != null
		&& sheet.TryGetRow(id, out worldRow))
			name = worldRow.Name.ToString();

		if (name == null) /* In case of (unlikely) failures */
			name = "???";

		return name;
	}

	/// <summary>
	/// Gets the name of the current zone.
	/// aka: Where is the player right now?
	/// </summary>
	/// <returns>Name of the current zone</returns>
	public static string
	GetCurrentZoneName()
	{
		uint id;
		string? name;
		ExcelSheet<TerritoryType> sheet;
		TerritoryType zoneRow;

		name = null;
		id = ComfyLoot.ClientState.TerritoryType;
		sheet = ComfyLoot.DataManager.GetExcelSheet<TerritoryType>();

		if (sheet != null
		&& sheet.TryGetRow(id, out zoneRow))
			name = zoneRow.PlaceName.Value.Name.ToString();

		if (name == null) /* just in case */
			name = "???";

		return name;
	}
}