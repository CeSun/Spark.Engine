using ImGuiNET;

namespace Editor.Panels;

[AddPanelToEditor]
public class ContentViewerPanel : IPanel
{
    public void Renderer(double DeltaTime)
    {
        ImGui.Begin("Content Viewer");

        ImGui.End();
    }
}
