/* See LICENSE file for copyright and license details. */
using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace ComfyLoot;

public enum DtrBarOption {
	TOTAL_QUANTITY = 0,
	ZONE_QUANTITY = 1,
	TOTAL_VALUE = 2,
	ZONE_VALUE = 3
}

[Serializable]
public class Config : IPluginConfiguration {
	
	/// <summary>
	/// This should never never be manipulated, this is only a unit test utility
	/// </summary>
	public static bool IsTestEnvironment { get; set; } = false;
	/// <summary>
	/// Globably toggles all experimental features
	/// </summary>
	public static bool STABLE { get; } = false;

	public int Version { get; set; } = 0; /* TODO: figure out the point of this */

	/* all features should be opt in */
	public List<uint> IgnoredItemIds { get; set; } = new List<uint>();
	public List<string> IgnoredZones { get; set; } = new List<string>();
	public bool UniversalisEnabled { get; set; } = false; /* NOTE: disabled for legal reasons */

	/* NOTE: Exprerimantal, might get removed */
	public bool IgnoreCrystals { get; set; } = false;
	public bool ItemContextMenu { get; set; } = false;
	public bool ShowDtrBar { get; set; } = false;
	public DtrBarOption DtrBarOption { get; set; } = DtrBarOption.TOTAL_QUANTITY;

	/// <summary>
	/// Saves plugin config
	/// </summary>
	public void
	Save()
	{
		/* NOTE: This exists to make saving less cumbersome */
		ComfyLoot.Dalamud.SavePluginConfig(this);
	}
}