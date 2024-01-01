using ImGuiNET;
using Spark.Engine;
using Spark.Engine.GUI;

namespace Editor.Panels;

public class ContentViewerPanel : ImGUIWindow
{
    public ContentViewerPanel(Level level) : base(level)
    {
    }

    public override void Render(double DeltaTime)
    {
        ImGui.Begin("Content Viewer");

        ImGui.End();
    }
}
