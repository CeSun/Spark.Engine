using Editor.Subsystem;
using ImGuiNET;
using Silk.NET.Windowing;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Attributes;
using Spark.Engine.GUI;
using Spark.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Editor.Panels;

public class PlaceActorsPanel : ImGUIWindow
{

    List<Type> ActorTypes = new List<Type>();

    EditorSubsystem EditorSubsystem;
    public PlaceActorsPanel(Level level) : base(level)
    {
        RefreshActors();
        EditorSubsystem = level.Engine.GetSubSystem<EditorSubsystem>();
    }


    public void RefreshActors()
    {
        foreach(var type in AssemblyHelper.GetAllType())
        {
            if (type.IsSubclassOf(typeof(Actor)))
            {
                var att = type.GetCustomAttribute<ActorInfo>();
                if (att != null)
                {
                    if (att.DisplayOnEditor == false)
                        continue;
                    if (Groups.Contains(att.Group) == false)
                    {
                        Groups.Add(att.Group);

                        ActorsTypeMap.Add(att.Group, new List<Type>());
                    }
                    var list = ActorsTypeMap[att.Group];
                    list.Add(type);

                }

                ActorTypes.Add(type);
            }
        }
        Groups.Add("All Classes");
        ActorsTypeMap.Add("All Classes", ActorTypes);

    }

    private int SelectGroup = 0;

    public List<string> Groups = new List<string>();

    public Dictionary<string, List<Type>> ActorsTypeMap = new Dictionary<string, List<Type>>();

    public override void Render(double DeltaTime)
    {
        ImGui.Begin("Place Actors##placeactors");


        ImGui.Columns(2); 
        ImGui.SetColumnWidth(0, 100);
        ImGui.BeginChild("123"); 
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
        float buttonWidth = ImGui.GetContentRegionAvail().X;
        Vector4 HoveredColor = default;

        unsafe
        {
            Vector4* color = ImGui.GetStyleColorVec4(ImGuiCol.ButtonHovered);

            HoveredColor = *color;
        }

        for (int i = 0; i < Groups.Count; i++)
        {
            if (SelectGroup == i)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, HoveredColor);
            }
            if(ImGui.Button(Groups[i], new Vector2(buttonWidth, 0)))
            {
                SelectGroup = i;
            }

            if (SelectGroup == i)
            {
                ImGui.PopStyleColor();
            }
        }

        ImGui.PopStyleVar();
        ImGui.EndChild();
        ImGui.NextColumn();

        for (int i = 0; i <= Groups.Count; i++) { 
            if (SelectGroup == i)
            {

                ImGui.BeginChild("##Group" + i);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                buttonWidth = ImGui.GetContentRegionAvail().X;
                foreach (var item in ActorsTypeMap[Groups[i]])
                {
                    if (ImGui.Button(item.Name, new Vector2(buttonWidth, 0)))
                    {
                        
                    }
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Left) && EditorSubsystem.ClickType == null)
                    {
                        EditorSubsystem.ClickType = item;
                    }
                }
                ImGui.PopStyleVar();
                ImGui.EndChild();
            }
        }

        ImGui.End();

    }
}
