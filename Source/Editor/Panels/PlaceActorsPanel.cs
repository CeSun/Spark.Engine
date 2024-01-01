using ImGuiNET;
using Spark.Engine;
using Spark.Engine.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels;

public class PlaceActorsPanel : ImGUIWindow
{

    public PlaceActorsPanel(Level level) : base(level)
    {
    }

    public override void Render(double DeltaTime)
    {
        ImGui.Begin("Place Actors##placeactors");
        ImGui.End();


    }
}
