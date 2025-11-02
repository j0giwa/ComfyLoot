/* See LICENSE file for copyright and license details. */
using System;
using Dalamud.Configuration;

namespace ComfyLoot;

[Serializable]
public class Configuration : IPluginConfiguration
{
	public int Version { get; set; } = 0; /* TODO: figure out the point of this */

	public bool IsConfigWindowMovable { get; set; } = true;
	public bool UniversalisEnabled { get; set; } = false; /* disabled for legal reasons */

	public bool ShowDtrBar { get; set; } = false;
	public int DtrBarOption { get; set; } = 0;

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
