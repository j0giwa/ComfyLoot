using System;
using System.Globalization;
using System.Text;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game;
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
	/// Formats a number into a compact readable form (e.g., K for thousands, M for millions).
	/// </summary>
	/// <param name="number">The number to format.</param>
	/// <returns>A shortened numeric string.</returns>
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

		format = value % 1 == 0 ? "0" : "0.#";
		return value.ToString(format, CultureInfo.InvariantCulture) + suffix;
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
		TerritoryType? found;

		if (string.IsNullOrWhiteSpace(name))
			return 0;

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
	/// <param name="itemId">The item ID.</param>
	/// <returns><c>true</c> if the item is a currency; otherwise, <c>false</c>.</returns>
	/// <summary>
	/// Determines whether the specified item is classified as a currency.
	/// </summary>
	/// <param name="itemId">The item ID.</param>
	/// <returns><c>true</c> if the item is a currency; otherwise, <c>false</c>.</returns>
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
		case 29: /* FALLTHROUGH */
		case 47:
		case 54:
		case 56:
		case 57:
		case 16: /* ERROR: this makes the function overly agressive */
			/* HACK: stop pieces from getting flagged as currency */
			switch (itemId) {
			case (uint)SpecialItems.ALLAGAN_TIN_PIECE:
			case (uint)SpecialItems.ALLAGAN_BRONZE_PIECE:
			case (uint)SpecialItems.ALLAGAN_SILVER_PIECE:
			case (uint)SpecialItems.ALLAGAN_GOLD_PIECE:
			case (uint)SpecialItems.ALLAGAN_PLATINUM_PIECE:
			case (uint)SpecialItems.NIGHTWORLD_BRONZE_PIECE:
			case (uint)SpecialItems.NIGHTWORLD_SILVER_PIECE:
				result = false;
				break;
			default:
				result = true;
				break;
			}
			break;
		default:
			result = false;
			break;
		}

		return result;
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