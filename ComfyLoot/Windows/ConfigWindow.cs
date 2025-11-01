/* See LICENSE file for copyright and license details. */
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ComfyLoot.Windows;

public class ConfigWindow : Window, IDisposable
{
	private readonly ComfyLoot plugin;
	private readonly Configuration Configuration;

	/// <summary>
	/// ConfigWindow:ctor
	/// </summary>
	/// <param name="plugin">ComfyLoot plugin instance</param>
	public ConfigWindow(ComfyLoot plugin) 
		: base("ComfyLoot config###comfyloot_config_ui")
	{
		SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(600, 400),
			MaximumSize = new Vector2(600, 2000),
		};

		SizeCondition = ImGuiCond.Always;
		Configuration = plugin.Configuration;

		this.plugin = plugin;
	}

	public override void
	PreDraw()
	{
		/* Flags must be added or removed before Draw() is being called, or they won't apply */
		if (Configuration.IsConfigWindowMovable)
			Flags &= ~ImGuiWindowFlags.NoMove;
		else
			Flags |= ImGuiWindowFlags.NoMove;
	}

	public override void
	Draw()
	{
		/* Can't ref a property, so use a local copy */
		bool universalis = Configuration.UniversalisEnabled;
		bool serverinfo = Configuration.ShowDtrBar;

		ImGui.TextUnformatted("Read this!!!");
		ImGui.TextWrapped("Ugh, another conscent thingy. We hate them too, but apparently it's the law. If you enable this, your ip, homeworld, and items you picked up will be sent to Universalis. We don't know what they will do with this data.");
		ImGui.TextWrapped("Click 'Enable' so we can all pretend this mattered.");

		if (ImGui.Checkbox("Enable Universalis", ref universalis)) {
			Configuration.UniversalisEnabled = universalis;
			Configuration.Save();
		}

		ImGui.Separator();

		if (ImGui.Checkbox("Enable Server Info bar entry", ref serverinfo)) {
			Configuration.ShowDtrBar = serverinfo;
			plugin.UpdateDtrBar();
			Configuration.Save();
		}
	}

	public void 
	Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void
	Dispose(bool disposing)
	{
		/* Cleanup */
	}
}   
