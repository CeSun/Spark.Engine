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
        ref var flags = ref ImGui.GetIO().ConfigFlags;
        flags |= ImGuiConfigFlags.DockingEnable;

    }

    public override void Render(double deltaTime)
    {
        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport());

        ImGui.Begin("Hello Editor!");
        ImGui.Button("button");
        ImGui.End();
    }
}
