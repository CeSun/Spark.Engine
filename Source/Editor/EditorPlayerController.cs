using ImGuiNET;
using Spark.Engine;
using Spark.Engine.Actors;

namespace Editor;

public class EditorPlayerController : PlayerController
{
    public EditorPlayerController(Level level, string name = "") : base(level, name)
    {
        var style = ImGui.GetStyle();
        style.WindowMenuButtonPosition = ImGuiDir.None;
    }
}
