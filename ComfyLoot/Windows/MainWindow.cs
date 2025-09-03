using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace ComfyLoot.Windows;

public class MainWindow : Window, IDisposable
{
    	private string GoatImagePath;
    	private Plugin Plugin;

	/// <summary>
	/// MainWindow:ctor
	/// </summary>
	/// <param name="plugin"></param>
	public MainWindow(Plugin plugin)
		: base("ComfyLoot", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(375, 330),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};

		Plugin = plugin;

		TitleBarButtons = new() {
			new TitleBarButton() {
				Icon = FontAwesomeIcon.Cog,
				Click = (msg) => {
		    			Plugin.ToggleConfigUI();
				},
				IconOffset = new(2,1),
				ShowTooltip = () => {
		    			ImGui.BeginTooltip();
		    			ImGui.Text("Open Settings");
		    			ImGui.EndTooltip();
				}
	    		}
		};
	}

	public void Dispose() { }

	public override void
	Draw()
	{
		ImGui.TextUnformatted($"Total count: 666");
		ImGui.TextUnformatted($"Total Value: 42069");
		ImGui.Spacing();

		// Normally a BeginChild() would have to be followed by an unconditional EndChild(),
		// ImRaii takes care of this after the scope ends.
		// This works for all ImGui functions that require specific handling, examples are BeginTable() or Indent().
		using (var child = ImRaii.Child("SomeChildWithAScrollbar", Vector2.Zero, true)) {
			
			// Check if this child is drawing
			if (child.Success)
			{
				ImGuiHelpers.ScaledDummy(20.0f);

				// Example for other services that Dalamud provides.
				// ClientState provides a wrapper filled with information about the local player object and client.

				var localPlayer = Plugin.ClientState.LocalPlayer;
				if (localPlayer == null)
				{
					ImGui.TextUnformatted("Our local player is currently not loaded.");
					return;
				}

				if (!localPlayer.ClassJob.IsValid)
				{
					ImGui.TextUnformatted("Our current job is currently not valid.");
					return;
				}

				// If you want to see the Macro representation of this SeString use `ToMacroString()`
				ImGui.TextUnformatted($"Our current job is ({localPlayer.ClassJob.RowId}) \"{localPlayer.ClassJob.Value.Abbreviation}\"");

				// Example for quarrying Lumina directly, getting the name of our current area.
				var territoryId = Plugin.ClientState.TerritoryType;
				if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
				{
					ImGui.TextUnformatted($"We are currently in ({territoryId}) \"{territoryRow.PlaceName.Value.Name}\"");
				}
				else
				{
					ImGui.TextUnformatted("Invalid territory.");
				}
			}
		}
	}
}