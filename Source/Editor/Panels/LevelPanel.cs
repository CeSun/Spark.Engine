using ImGuiNET;
using Spark.Engine;
using Spark.Engine.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels;

public class LevelPanel : ImGUIWindow
{
    public LevelPanel(Level level) : base(level)
    {
    }

    public override void Render(double deltaTime)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.Begin("New Level##levelpanel");
        ImGui.End();
    }
}
