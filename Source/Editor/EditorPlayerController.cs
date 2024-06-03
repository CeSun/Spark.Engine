using Editor.Panels;
using Editor.Subsystem;
using Editor.Windows;
using ImGuiNET;
using Spark.Engine;
using Spark.Engine.Actors;

namespace Editor;

public class EditorPlayerController : PlayerController
{
    public EditorPlayerController(Level level, string Name = "") : base(level, Name)
    {

        var imGuiSubSystem = CurrentLevel.Engine.GetSubSystem<ImGuiSubSystem>();
        if (imGuiSubSystem == null)
            return;

        new StartWindow(level.Engine).Open();
    }
}
