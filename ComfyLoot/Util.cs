using System;
using System.Globalization;
using ComfyLoot.Models;
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

	/// <summary>
	/// Formats a number to a gil value with shortened format.
	/// </summary>
	/// <param name="number">Gil value</param>
	/// <returns>Formatted string</returns>
	public static string 
	FormatGil(int number)
	{
		const char gil = (char)Dalamud.Game.Text.SeIconChar.Gil;
		return $"{FormatNumber(number)}{gil}";
	}

	/// <summary>
	/// Formats a number in a shortened form (K for thousands, M for millions).
	/// </summary>
	/// <param name="number">Number</param>
	/// <returns>Formatted string</returns>
	public static string 
	FormatNumber(int number)
	{
		double value = number;
		string suffix = "";
		string format;

		if (Math.Abs(value) >= 1_000_000) {
			value /= 1_000_000;
			suffix = "M";
		} else if (Math.Abs(value) >= 1_000) {
			value /= 1_000;
			suffix = "K";
		}

		// Keep one decimal if needed, otherwise no decimal
		format = value % 1 == 0 ? "0" : "0.#";
		return value.ToString(format, CultureInfo.InvariantCulture) + suffix;
	}

	/// <summary>
	/// Gets the itemid without offsets
	/// </summary>
	/// <param name="itemId">itemid</param>
	/// <returns>item baseid</returns>
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

	public static uint
	GetZoneId(string name)
	{
		string zoneName;
		ExcelSheet<TerritoryType>? sheet;
		TerritoryType? found;

		if (string.IsNullOrWhiteSpace(name))
			return 0;

		name = name.Trim();
		if (name.Equals("Marketboard", StringComparison.OrdinalIgnoreCase))
			return (uint)Zones.MARKETBOARD;

		if (name.Equals("Delivery", StringComparison.OrdinalIgnoreCase) ||
		    name.Equals("Mail", StringComparison.OrdinalIgnoreCase))
			return (uint)Zones.MAIL;

		sheet = ComfyLoot.DataManager.GetExcelSheet<TerritoryType>();
		if (sheet == null) {
			ComfyLoot.Log.Fatal("[Lumina] Failed to resolve sheet: TerritoryType");
			return 0;
		}

		found = null;
		foreach (TerritoryType zone in sheet) {
			zoneName = zone.PlaceName.Value.Name.ExtractText() ?? "";
			if (zoneName.Equals(name, StringComparison.OrdinalIgnoreCase)) {
				found = zone;
				break;
			}
		}

		if (found == null) {
			foreach (TerritoryType zone in sheet) {
				zoneName = zone.PlaceName.Value.Name.ExtractText() ?? "";
				if (zoneName.Contains(name, StringComparison.OrdinalIgnoreCase)) {
					found = zone;
					break;
				}
			}
		}

		if (found == null) {
			ComfyLoot.Log.Warning("[Lumina] Zone not found: {Zone}", 
				name);
			return 0;
		}

		return found.Value.RowId;
	}

	/// <summary>
	/// Gets the name of the current zone (where the player currently is).
	/// </summary>
	public static string
	GetZoneName(uint id)
	{
		string name;
		ExcelSheet<TerritoryType>? sheet;
		TerritoryType zone;

		name = "???"; /* fallback */

		switch (id) {
		case (uint)Zones.MARKETBOARD:
			return "Marketboard";
		case (uint)Zones.MAIL:
			return "Delivery";
		default:
			sheet = ComfyLoot.DataManager.GetExcelSheet<TerritoryType>();
			if (sheet == null) {
				ComfyLoot.Log.Fatal("[Lumina] Failed to resolve sheet: TerritoryType");
				return name;
			}

			if (!sheet.TryGetRow(id, out zone))
				return name;

			if (zone.PlaceName.Value.Name.IsEmpty)
				return name;

			name = zone.PlaceName.Value.Name.ToString();
			if (name.IsNullOrWhitespace())
				return "???";

			return name;
		}
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

			ComfyLoot.Log.Debug("Mail Target Check: BaseId={baseId}, Name={textValue}",
				target.BaseId,
				target.Name.TextValue);

			if (target.BaseId == 1003567 /* Delivery Moogle NPC */
			|| target.BaseId == 1969) /* housing mailbox */
				return true;

			/* fallback: identification over name */
			switch (target.Name.TextValue) {
			case "Delivery Moogle": /* FALLTHROUGH */
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

	public static bool
	IsTargetMarketboard()
	{
		IGameObject? target;

		if (!ComfyLoot.ClientState.IsLoggedIn)
			return false;

		try {
			target = ComfyLoot.TargetManager.Target;
			if (target == null)
				return false;

			ComfyLoot.Log.Debug("Mail Target Check: BaseId={baseId}, Name={textValue}",
				target.BaseId,
				target.Name.TextValue);

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