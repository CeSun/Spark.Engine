using ImGuiNET;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.GUI;

namespace Editor;

public class EditorPlayerController : PlayerController
{
    public EditorPlayerController(Level level, string Name = "") : base(level, Name)
    {
        var EditorImguiCanvas = new EditorImguiContext(level);

        EditorImguiCanvas.AddToViewPort();
    }
}

public class EditorImguiContext : ImGUIContext
{
    public EditorImguiContext(Level level) : base(level)
    {
    }

    public override void Render(double deltaTime)
    {
        

        ImGui.Begin("Hello Editor!");
        ImGui.Button("button");
        ImGui.End();
    }
}
