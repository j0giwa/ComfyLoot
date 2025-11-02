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
		bool universalis;
		bool serverinfo;
		bool serverinfoDisplayChanged;
		int serverinfoDisplayOption;

		/* Can't ref a property, so use a local copy */
		universalis = Configuration.UniversalisEnabled;
		serverinfo = Configuration.ShowDtrBar;
		serverinfoDisplayOption = Configuration.DtrBarOption;

		ImGui.TextUnformatted("Read this!!!");
		ImGui.TextWrapped("Ugh, another conscent thingy. We hate them too, but apparently it's the law. If you enable this, your ip, homeworld, and items you picked up will be sent to Universalis. We don't know what they will do with this data.");
		ImGui.TextWrapped("Click 'Enable' so we can all pretend this mattered.");

		if (ImGui.Checkbox("Enable Universalis", ref universalis)) {
			Configuration.UniversalisEnabled = universalis;
			Configuration.Save();
			ComfyLoot.Log.Debug("[CONFIG) Univeralis enabled {univeralis}", universalis);
		}

		ImGui.Separator();

		if (ImGui.Checkbox("Enable Server Info bar entry", ref serverinfo)) {
			Configuration.ShowDtrBar = serverinfo;
			plugin.UpdateDtrBar();
			Configuration.Save();
			ComfyLoot.Log.Debug("[CONFIG) DTR enabled {serverinfo}", serverinfo);
		}

		if (!serverinfo)
			ImGui.BeginDisabled();

		serverinfoDisplayChanged = false;

		if (ImGui.RadioButton("Total items", ref serverinfoDisplayOption, 0))
			serverinfoDisplayChanged = true;
		if (ImGui.RadioButton("Items per current zone", ref serverinfoDisplayOption, 1))
			serverinfoDisplayChanged = true;
		if (ImGui.RadioButton("Total value", ref serverinfoDisplayOption, 2))
			serverinfoDisplayChanged = true;
		if (ImGui.RadioButton("Value per current zone", ref serverinfoDisplayOption, 3))
			serverinfoDisplayChanged = true;

		if (serverinfoDisplayChanged) {
			Configuration.DtrBarOption = serverinfoDisplayOption;
			plugin.UpdateDtrBar();
			Configuration.Save();
			ComfyLoot.Log.Debug("[CONFIG) DTR setting {serverinfoDisplayOption}", serverinfoDisplayOption);
		}

		if (!serverinfo)
			ImGui.EndDisabled();
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
