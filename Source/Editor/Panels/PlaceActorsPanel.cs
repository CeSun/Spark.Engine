using ImGuiNET;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.GUI;
using Spark.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels;

public class PlaceActorsPanel : ImGUIWindow
{

    List<Type> ActorTypes = new List<Type>();

    public PlaceActorsPanel(Level level) : base(level)
    {
        RefreshActors();
    }


    public void RefreshActors()
    {
        foreach(var type in AssemblyHelper.GetAllType())
        {
            if (type.IsSubclassOf(typeof(Actor)))
            {
                ActorTypes.Add(type);
            }
        }
    }
    public override void Render(double DeltaTime)
    {
        ImGui.Begin("Place Actors##placeactors");


        if (ImGui.BeginTabBar("MyTabBar"))
        {
            if (ImGui.BeginTabItem("All Actors"))
            {
                foreach (var type in ActorTypes)
                {
                    ImGui.Button(type.Name);
                }
            }
            if (ImGui.BeginTabItem("Base"))
            {
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Light"))
            {
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }


        ImGui.End();


    }
}
