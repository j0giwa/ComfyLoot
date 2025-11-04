using ComfyLoot.Models;
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

	/// <summary>
	/// Determines if the given item ID represents a currency.
	/// </summary>
	public static bool
	IsCurrency(uint itemId)
	{
		/* TODO: Lumina lookup instead of hardcoding*/
		switch (itemId) {
		case (int)Currency.GIL:
		case (int)Currency.STORM_SEAL:
		case (int)Currency.SERPENT_SEAL:
		case (int)Currency.FLAME_SEAL:
		case (int)Currency.ALLIED_SEALS:
		case (int)Currency.WOLF_MARKS:
		case (int)Currency.MGP:
		case (int)Currency.TROPHY_CRYSTALS:
		case (int)Currency.TOMESTONE_POETICS:
		case (int)Currency.TOMESTONE_AESTETICS:
		case (int)Currency.TOMESTONE_MATHEMATICS:
		case (int)Currency.TOMESTONE_HELIOMETRY:
		case (int)Currency.CENTURIO_SEALS:
		case (int)Currency.SACK_OF_NUTS:
		case (int)Currency.BICOLOR_GEMSTONES:
		case (int)Currency.WHITE_CRAFTER_SCRIPS:
		case (int)Currency.PURPLE_CRAFTER_SCRIPS:
		case (int)Currency.ORANGE_CRAFTER_SCRIPS:
		case (int)Currency.WHITE_GATHERER_SCRIPS:
		case (int)Currency.PURPLE_GATHERER_SCRIPS:
		case (int)Currency.ORANGE_GATHERER_SCRIPS:
		case (int)Currency.SKYBUILDER_SCRIPS:
			return true;
		default:
			return false;
		}
	}
}