using System;
using System.Globalization;
using Dalamud.Game.ClientState.Objects.Types;
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
/* SMELL: Cohesion is fucked, but it didn't fit anywhere else */
public static class Util {

	public static bool 
	IsTargetMarketboard()
	{
		IGameObject ? target;

		if (!ComfyLoot.ClientState.IsLoggedIn)
			return false;

		try {
			target = ComfyLoot.TargetManager.Target;
			if (target == null)
				return false;
			ComfyLoot.Log.Debug($"Mail Target Check: BaseId={target.BaseId}, DataId={target.DataId}, Name={target.Name.TextValue}");

			if (target.BaseId == 2000402)
				return true;

			/* fallback: identification over name */
			switch (target.Name.TextValue) {
			case "Market Board": /* FALLTHROUGH */
			case "Schwarzes Brett":
			case "Panneau des ventes":
			case "マーケットボード":
				return true;
			default:
				return false;
			}
		} catch (Exception e) {
			ComfyLoot.Log.Error(e, "WTF");
			return false;
		}
	}

	public static bool
	IsTargetMail()
	{
    		IGameObject? target;

    		if (!ComfyLoot.ClientState.IsLoggedIn)
        		return false;

   	 	try {
        		target = ComfyLoot.TargetManager.Target;
        		if (target == null)
            		return false;

			ComfyLoot.Log.Debug($"Mail Target Check: BaseId={target.BaseId}, DataId={target.DataId}, Name={target.Name.TextValue}");

        		if (target.BaseId == 1003567 /* Delivery Moogle NPC */
			|| target.DataId == 1969) /* housing mailbox (BaseId may vary slightly but DataId is consistent) */
				return true;

			/* fallback: identification over name */
			switch (target.Name.TextValue){
            		case "Delivery Moogle":
			case "Mailbox":
			case "Kupo-Kurier":
			case "Briefkasten":
			case "Mog postier":
			case "Boîte aux lettres":
			case "レターモーグリ":
            		case "メールボックス":
                		return true;
            		default:
                		return false;
        		}
    		} catch (Exception ex) {
        		ComfyLoot.Log.Error(ex, "Error detecting mailbox/delivery moogle target");
        		return false;
    		}
	}


	/// <summary>
	/// Formats a number to a gil value.
	/// </summary>
	/// <param name="number">Gil value</param>
	/// <returns>Formated string</returns>
	public static string
	FormatGil(int number)
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
	/// Gets the itemid without offsets
	/// </summary>
	public static uint
	GetBaseId(uint itemId)
	{
		return ItemUtil.GetBaseId(itemId).ItemId;
	}

	/// <summary>
	/// Retrieves the name of the character's homeworld.
	/// </summary>
	public static unsafe string
	GetHomeWorld()
	{
		uint worldId;
		string name;
		ExcelSheet<World>? sheet;
		World worldRow;
		BattleChara* localPlayer;

		name = "???"; /* fallback */

		sheet = ComfyLoot.DataManager.GetExcelSheet<World>();
		if (sheet == null) {
			ComfyLoot.Log.Fatal("[Lumina] Failed to resolve sheet: World");
			return name;
		}

		worldId = AgentLobby.Instance()->LobbyData.HomeWorldId;
		if (worldId == 0) {
			localPlayer = Control.GetLocalPlayer();
			if (localPlayer != null)
				worldId = localPlayer->CurrentWorld;
		}

		if (!sheet.TryGetRow(worldId, out worldRow)
		|| worldRow.Name.IsEmpty)
			return name;

		name = worldRow.Name.ToString();
		if (name.IsNullOrWhitespace())
			return "???";

		return name;
	}

	/// <summary>
	/// Gets the name of the current zone (where the player currently is).
	/// </summary>
	public static string
	GetCurrentZoneName()
	{
		uint id;
		string name;
		ExcelSheet<TerritoryType>? sheet;
		TerritoryType zone;

		name = "???"; /* fallback */

		sheet = ComfyLoot.DataManager.GetExcelSheet<TerritoryType>();
		if (sheet == null) {
			ComfyLoot.Log.Fatal("[Lumina] Failed to resolve sheet: TerritoryType");
			return name;
		}

		id = ComfyLoot.ClientState.TerritoryType;
		if (!sheet.TryGetRow(id, out zone))
			return name;

		if (zone.PlaceName.Value.Name.IsEmpty)
			return name;

		name = zone.PlaceName.Value.Name.ToString();
		if (name.IsNullOrWhitespace())
			return "???";

		return name;
	}

	/// <summary>
	/// Gets the rarity of an item.
	/// </summary>
	public static byte
	GetRarity(uint itemId)
	{
		ExcelSheet<Item>? sheet;
		Item? item;
		byte rarity;

		rarity = 1; /* fallback */

		sheet = ComfyLoot.DataManager.GetExcelSheet<Item>();
		if (sheet == null) {
			ComfyLoot.Log.Fatal("[Lumina] Failed to resolve sheet: Item");
			return rarity;
		}

		item = sheet.GetRowOrDefault(itemId);
		if (item == null)
			return rarity;

		rarity = item.Value.Rarity;
		return rarity;
	}

	/// <summary>
	/// Determines if the given item ID represents a currency.
	/// </summary>
	public static bool
	IsCurrency(uint itemId)
	{
		ExcelSheet<Item>? sheet;
		Item? item;
		bool result;

		result = false;

		sheet = ComfyLoot.DataManager.GetExcelSheet<Item>();
		if (sheet == null) {
			ComfyLoot.Log.Fatal("[Lumina] Failed to resolve sheet: Item");
			return true;
		}

		item = sheet.GetRowOrDefault(itemId);
		if (item == null)
			return result;

		/* FIXME: There might be some missing here */
		switch (item.Value.FilterGroup) {
		case 16: /* FALLTHOUGH */
		case 29:
		case 47:
		case 54:
		case 56:
		case 57:
			result = true;
			break;
		default:
			result = false;
			break;
		}
		return result;
	}

	public static uint
	GetItemBaseId(string name)
	{
		uint baseid;
		string target;
		string itemName;
		ExcelSheet<Item>? sheet;
		Item? item;

		if (string.IsNullOrWhiteSpace(name))
			return 0;

		sheet = ComfyLoot.DataManager.GetExcelSheet<Item>();
		if (sheet == null) {
			ComfyLoot.Log.Fatal("[Lumina] Failed to resolve sheet: Item");
			return 0;
		}

		target = name.Trim();
		item = null;

		/* EXACT case-insensitive match */		
		foreach (Item row in sheet) {
			itemName = row.Name.ExtractText();
			if (itemName.Equals(target, StringComparison.OrdinalIgnoreCase)) {
				item = row;
				break;
			}
		}

		/* PARTIAL match as fallback */
		if (item == null) {
			foreach (Item row in sheet) {
				itemName = row.Name.ExtractText();
				if (itemName.Contains(target, StringComparison.OrdinalIgnoreCase)) {
					item = row;
					break;
				}
			}
		}

		if (item == null) {
			ComfyLoot.Log.Warning("[Lumina] Item not found: {item}", target);
			return 0;
		}

		baseid = ItemUtil.GetBaseId(item.Value.RowId).ItemId;
		return baseid;
	}

	/// <summary>
	/// Determines if an item is tradable.
	/// </summary>
	public static bool
	IsTradable(uint itemId)
	{
		ExcelSheet<Item>? sheet;
		Item item;

		sheet = ComfyLoot.DataManager.GetExcelSheet<Item>();
		if (sheet == null) {
			ComfyLoot.Log.Fatal("[Lumina] Failed to resolve sheet: Item");
			return false;
		}

		if (!sheet.TryGetRow(itemId, out item))
			return false;

		return !item.IsUntradable;
	}
}