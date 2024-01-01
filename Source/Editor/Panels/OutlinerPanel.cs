using ImGuiNET;
using Spark.Engine;
using Spark.Engine.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels;

public class OutlinerPanel : ImGUIWindow
{
    public OutlinerPanel(Level level) : base(level)
    {
    }

    public override void Render(double deltaTime)
    {
        base.Render(deltaTime);
        ImGui.Begin("Outliner##outliner", ImGuiWindowFlags.NoCollapse);
        ImGui.End();
    }
}
