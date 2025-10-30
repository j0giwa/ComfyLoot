/* See LICENSE file for copyright and license details. */
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ComfyLoot.Windows;

public class ConfigWindow : Window, IDisposable
{
	private readonly Configuration Configuration;

	/* We give this window a constant ID using ###.
	 * This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
	 * and he window ID will always be "###XYZ counter window" for ImGui */
	public ConfigWindow(ComfyLoot plugin) 
		: base("ComfyLoot config###With a constant ID")
	{
		SizeConstraints = new WindowSizeConstraints {
			MinimumSize = new Vector2(600, 400),
			MaximumSize = new Vector2(600, 2000),
		};

		SizeCondition = ImGuiCond.Always;
		Configuration = plugin.Configuration;
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
		var configValue = Configuration.UniversalisEnabled;

		ImGui.TextUnformatted("Read this!!!");
		ImGui.TextWrapped("Ugh, another conscent thingy. We hate them too, but apparently it's the law. If you enable this, your ip, homeworld, and items you picked up will be sent to Universalis. We don't know what they will do with this data.");
		ImGui.TextWrapped("Click 'Enable' so we can all pretend this mattered.");

		if (ImGui.Checkbox("Enable Universalis", ref configValue)) {
			Configuration.UniversalisEnabled = configValue;
			Configuration.Save();
		}
		ImGui.Separator();
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
