/* See LICENSE file for copyright and license details. */
using System;
using Dalamud.Configuration;

namespace ComfyLoot;

[Serializable]
public class Configuration : IPluginConfiguration
{
	public int Version { get; set; } = 0;

	public bool IsConfigWindowMovable { get; set; } = true;
	public bool UniversalisEnabled { get; set; } = false; /* disabled for legal reasons */

	// The below exist just to make saving less cumbersome
	public void
	Save()
	{
		ComfyLoot.Dalamud.SavePluginConfig(this);
	}
}
