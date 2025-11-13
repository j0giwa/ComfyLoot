using System.Globalization;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace ComfyLoot;

/// <summary>
/// Misc utility Functions.
/// </summary>
public static class Util {

	/// <summary>
	/// Formats a number to a gil value.
	/// </summary>
	/// <param name="number">Gil value</param>
	/// <returns>Formated string</returns>
	public static string
	FormatGilSting(int number)
	{
		const char gil = (char)Dalamud.Game.Text.SeIconChar.Gil;

		string result;

		result = FormatNumber(number);

		return $"{result}{gil}";
	}

	/// <summary>
	/// Formats a number.
	/// </summary>
	/// <param name="number">number</param>
	/// <returns>Formated string</returns>
	public static string
	FormatNumber(int number)
	{
		string result;

		result = number.ToString("N0", CultureInfo.InvariantCulture);
		result = result.Replace(",", ".");

		return $"{result}";
	}

	/// <summary>
	/// retrieves the name of the characters homeworld.
	/// </summary>
	public static unsafe string
	GetHomeWorld()
	{
		uint worldId;
		string? name;
		ExcelSheet<World> sheet;
		World worldRow;
		BattleChara* localPlayer;

		name = "???"; /* fallback */

		sheet = ComfyLoot.DataManager.GetExcelSheet<World>();
		if (sheet == null) {
			ComfyLoot.Log.Fatal("[Lumina] Cannot determine Homeword, world-sheet can not be resolved");
			return name;
		}

		worldId = AgentLobby.Instance()->LobbyData.HomeWorldId;
		if (worldId == 0) {
			localPlayer = Control.GetLocalPlayer();
			if (localPlayer != null)
				worldId = localPlayer->CurrentWorld;
		}

		if (sheet.TryGetRow(worldId, out worldRow))
			name = worldRow.Name.ToString();

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
		TerritoryType zone;

		name = "???"; /* fallback */

		sheet = ComfyLoot.DataManager.GetExcelSheet<TerritoryType>();
		if (sheet == null) {
			ComfyLoot.Log.Fatal("[Lumina] Cannot determine zone, TerritoryType-sheet can not be resolved");
			return name;
		}

		id = ComfyLoot.ClientState.TerritoryType;
		if (sheet.TryGetRow(id, out zone))
			name = zone.PlaceName.Value.Name.ToString();

		return name;
	}

	/// <summary>
	/// Get the items rarity
	/// </summary>
	/// <param name="itemId"></param>
	/// <returns></returns>
	public static byte
	GetRarity(uint itemId)
	{
		ExcelSheet<Item>? items;
		Item? item;

		items = ComfyLoot.DataManager.GetExcelSheet<Item>();
		if (items == null) {
			ComfyLoot.Log.Fatal("[Lumina] Cannot determine rarity, Item-sheet can not be resolved");
			return 1;
		}

		item = items.GetRowOrDefault(itemId);
		if (item == null)
			return 1;

		return item.Value.Rarity;
	}

	/// <summary>
	///
	/// </summary>
	/// <param name="itemId"></param>
	/// <returns></returns>
	public static bool
	IsUntradable(uint itemId)
	{
		ExcelSheet<Item>? items;
		Item? item;

		items = ComfyLoot.DataManager.GetExcelSheet<Item>();
		if (items == null) {
			ComfyLoot.Log.Fatal("[Lumina] Cannot determine tradabiliy, Item-sheet can not be resolved");
			return true;
		}

		item = items.GetRowOrDefault(itemId);
		if (item == null)
			return true;

		return item.Value.IsUntradable;
	}

	/// <summary>
	/// Gets the itemid without offsets
	/// </summary>
	public static uint
	GetBaseId(uint itemId)
	{
		return ItemUtil.GetBaseId(itemId).ItemId;
	}

	/// <summary>
	/// Determines if the given item ID represents a currency.
	/// </summary>
	public static bool
	IsCurrency(uint itemId)
	{
		ExcelSheet<Item>? items;
		Item? item;

		items = ComfyLoot.DataManager.GetExcelSheet<Item>();
		if (items == null) {
			ComfyLoot.Log.Fatal("[Lumina] Cannot determine category, Item-sheet can not be resolved");
			return true;
		}

		item = items.GetRowOrDefault(itemId);
		if (item == null)
			return false;

		/* FIXME: There might be some missing here */
		switch (item.Value.FilterGroup) {
		case 16: /* FALLTHOUGH */
		case 29:
		case 47:
		case 54:
		case 56:
		case 57:
			return true;
		default:
			return false;
		}
	}
}