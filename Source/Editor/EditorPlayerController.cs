using Editor.GUI;
using Editor.Panels;
using Editor.Subsystem;
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
        var style = ImGui.GetStyle();
        style.WindowMenuButtonPosition = ImGuiDir.None;
        List<BasePanel> list =
        [
            new MainPanel(imGuiSubSystem),
            new LevelPanel(imGuiSubSystem),
            new PlaceActorsPanel(imGuiSubSystem),
            new ContentViewerPanel(imGuiSubSystem),
            new OutlinerPanel(imGuiSubSystem),
            new DetailsPanel(imGuiSubSystem),
        ];

        list.ForEach(panel => panel.AddToViewPort());
    }
}
