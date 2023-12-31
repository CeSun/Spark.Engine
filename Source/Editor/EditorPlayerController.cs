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
