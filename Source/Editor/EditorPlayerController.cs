using Editor.Panels;
using ImGuiNET;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.GUI;

namespace Editor;

public class EditorPlayerController : PlayerController
{
    public EditorPlayerController(Level level, string Name = "") : base(level, Name)
    {

        var style = ImGui.GetStyle();
        style.WindowMenuButtonPosition = ImGuiDir.None;
        List<ImGUIWindow> list =
        [
            new MainPanel(this.CurrentLevel),
            new LevelPanel(this.CurrentLevel),
            new PlaceActorsPanel(this.CurrentLevel),
            new ContentViewerPanel(this.CurrentLevel),
            new OutlinerPanel(this.CurrentLevel),
            new DetailsPanel(this.CurrentLevel),
        ];

        list.ForEach(panel => panel.AddToViewPort());
    }
}
