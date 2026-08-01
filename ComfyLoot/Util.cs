using System;
using System.Globalization;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.Sheets;

using ComfyLoot.Models;

namespace ComfyLoot;

/// <summary>
/// Misc utility Functions.
/// </summary>
/* SMELL: Cohesion is fucked, but it didn't fit anywhere else */
public static class Util {

	/// <summary>
	/// Formats a numeric value as a gil amount using a shortened representation.
	/// </summary>
	/// <param name="number">The gil value to format.</param>
	/// <returns>A formatted gil string.</returns>
	public static string
	FormatGil(int number)
	{
		const char gil = (char)Dalamud.Game.Text.SeIconChar.Gil;
		return $"{FormatNumber(number)}{gil}";
	}

	/// <summary>
	/// Formats a number into a compact readable form
	/// (e.g., K for thousands, M for millions).
	/// </summary>
	/// <param name="number">The number to format</param>
	/// <returns>A shortened numeric string.</returns>
	public static string
	FormatNumber(int number)
	{
		double value;
		string suffix;
		string format;
		string result;

		suffix = "";
		value = number;
		if (Math.Abs(value) >= 1_000_000) {
			value /= 1_000_000;
			suffix = "M";
		}

		if (Math.Abs(value) >= 1_000) {
			value /= 1_000;
			suffix = "K";
		}

		format = "0.#";
		if (value % 1 == 0)
			format = "0";

		result = value.ToString(format, CultureInfo.InvariantCulture);
		result += suffix;
		
		return result;
	}

	/// <summary>
	/// Gets the name of the player's home world.
	/// </summary>
	/// <returns>The home world name, or <c>"???"</c> if unresolved.</returns>
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
	/// Retrieves the base item ID (no offsets) for the given item ID.
	/// </summary>
	/// <param name="itemId">The item ID.</param>
	/// <returns>The base item ID.</returns>
	public static uint
	GetItemBaseId(uint itemId)
	{
		return ItemUtil.GetBaseId(itemId).ItemId;
	}

	/// <summary>
	/// Resolves a string item name (exact or partial match) to its base item ID.
	/// </summary>
	/// <param name="name">The item name to resolve.</param>
	/// <returns>The base item ID, or <c>0</c> if not found.</returns>
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
	/// Obtains the rarity value of an item.
	/// </summary>
	/// <param name="itemId">The item ID.</param>
	/// <returns>The item rarity value.</returns>
	public static byte
	GetRarity(uint itemId)
	{
		ExcelSheet<Item>? sheet;
		Item? item;
		byte rarity;

		rarity = 1; /* fallback */

		/* HACK: PWlugindata not accesble during tests, skipping */ 
		if (Config.IsTestEnvironment
		|| ComfyLoot.DataManager == null)
			return 1;

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
	/// Resolves a zone name (exact or partial match) to a zone ID.
	/// </summary>
	/// <param name="name">The zone name.</param>
	/// <returns>The zone ID, or <c>0</c> if not found.</returns>
	public static uint 
	GetZoneId(string name)
	{
		string zoneName;
		ExcelSheet<TerritoryType>? sheet;
		TerritoryType? partialMatch;

		if (string.IsNullOrWhiteSpace(name))
			return 0;

		sheet = ComfyLoot.DataManager.GetExcelSheet<TerritoryType>();
		if (sheet == null) {
			ComfyLoot.Log.Fatal("[Lumina] Failed to resolve sheet: TerritoryType");
			return 0;
		}

		partialMatch = null;

		foreach (TerritoryType zone in sheet) {
			if (zone.PlaceName.Value.Name.IsEmpty)
				zoneName = "";
			else
				zoneName = zone.PlaceName.Value.Name.ExtractText();

			if (zoneName.Equals(name, StringComparison.OrdinalIgnoreCase))
				return zone.RowId;

			if (partialMatch == null) {
				if (zoneName.Contains(name, StringComparison.OrdinalIgnoreCase))
					partialMatch = zone;
			}
		}
		
		if (partialMatch != null)
			return partialMatch.Value.RowId;

		ComfyLoot.Log.Warning("[Lumina] Zone not found: {Zone}", name);
		return 0;
	}

	/// <summary>
	/// Gets the name of the zone associated with the given zone ID.
	/// </summary>
	/// <param name="id">The zone ID.</param>
	/// <returns>The zone name, or <c>"???"</c> if unresolved.</returns>
	public static string
	GetZoneName(uint id)
	{
		string name;
		ExcelSheet<TerritoryType>? sheet;
		TerritoryType zone;

		name = "???"; /* fallback */

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

	public static unsafe string
	GetTradePartner()
	{
		const int tradeArrayIndex = 10;
		const int tradeParnerIndex = 11;

		string result;
		AtkArrayDataHolder atk;
		StringArrayData* tradeArray;

		atk = RaptureAtkModule.Instance()->AtkArrayDataHolder;
		tradeArray = atk._StringArrays[tradeArrayIndex];
		result = tradeArray->StringArray[tradeParnerIndex].ToString();

		return result;
	}

	/// <summary>
	/// Determines whether the specified item is classified as a currency.
	/// </summary>
	/// <remarks>We will only regard it as a currency, if it shows up in the currency window</remarks> 
	/// <param name="itemId">The item ID.</param>
	/// <returns><c>true</c> if the item is a currency; otherwise, <c>false</c>.</returns>
	public static bool
	IsCurrency(uint itemId)
	{
		/* NOTE: Lookup solution turned out to be overly agressive */
		switch (itemId) {
		case (uint)Currency.GIL: /* FALLTHROUGH */
		case (uint)Currency.STORM_SEAL:
		case (uint)Currency.SERPENT_SEAL:
		case (uint)Currency.FLAME_SEAL:
		case (uint)Currency.ALLIED_SEALS:
		case (uint)Currency.WOLF_MARKS:
		case (uint)Currency.MGP:
		case (uint)Currency.TROPHY_CRYSTALS:
		case (uint)Currency.TOMESTONE_POETICS:
		case (uint)Currency.TOMESTONE_AESTETICS:
		case (uint)Currency.TOMESTONE_HELIOMETRY:
		case (uint)Currency.TOMESTONE_MATHEMATICS:
		case (uint)Currency.CENTURIO_SEALS:
		case (uint)Currency.SACK_OF_NUTS:
		case (uint)Currency.BICOLOR_GEMSTONES:
		case (uint)Currency.WHITE_CRAFTER_SCRIPS:
		case (uint)Currency.WHITE_GATHERER_SCRIPS:
		case (uint)Currency.PURPLE_CRAFTER_SCRIPS:
		case (uint)Currency.PURPLE_GATHERER_SCRIPS:
		case (uint)Currency.ORANGE_CRAFTER_SCRIPS:
		case (uint)Currency.ORANGE_GATHERER_SCRIPS:
		case (uint)Currency.SKYBUILDER_SCRIPS:
			return true;
		default:
			return false;
		}
	}

	/// <summary>
	/// Determines whether the currently targeted object is a Delivery Moogle or mailbox.
	/// </summary>
	/// <returns><c>true</c> if the target represents mail interaction; otherwise, <c>false</c>.</returns>
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

			if (target.BaseId == 1003567 /* Delivery Moogle NPC */
			|| target.BaseId == 131113 /* Regal mailbox */
			|| target.BaseId == 1969) /* housing mailbox */
				return true;

			/* fallback: identification over name */
			switch (target.Name.TextValue) {
			case "Delivery Moogle": /* FALLTHROUGH */
			case "Regal Letter Box":
			case "Mailbox":
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
	/// Determines whether the currently targeted object is a marketboard.
	/// </summary>
	/// <returns><c>true</c> if the target is a marketboard; otherwise, <c>false</c>.</returns>
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
			if (target.Name.TextValue.Equals("Market Board"))
				return true;

			return false;
		} catch (Exception e) {
			ComfyLoot.Log.Error(e, "WTF");
			return false;
		}
	}

	/// <summary>
	/// Determines whether the specified item is tradable.
	/// </summary>
	/// <param name="itemId">The item ID.</param>
	/// <returns><c>true</c> if the item can be traded; otherwise, <c>false</c>.</returns>
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