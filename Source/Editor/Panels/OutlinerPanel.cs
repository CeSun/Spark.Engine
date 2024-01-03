using Editor.Subsystem;
using ImGuiNET;
using Silk.NET.Input;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels;

public class OutlinerPanel : ImGUIWindow
{
    EditorSubsystem EditorSubsystem;
    public OutlinerPanel(Level level) : base(level)
    {
        var system = level.Engine.GetSubSystem<EditorSubsystem>();
        if (system != null)
            EditorSubsystem = system;
        else
            throw new Exception("no editor subsystem");
    }

    public override void Render(double deltaTime)
    {
        base.Render(deltaTime);
        ImGui.Begin("Outliner##outliner", ImGuiWindowFlags.NoCollapse);

        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            EditorSubsystem.SelectedActor = null;

        }
        if(ImGui.TreeNodeEx("All Actors", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var actor in EditorSubsystem.LevelWorld.CurrentLevel.Actors)
            {
                var cond = EditorSubsystem.SelectedActor != actor;
                if (cond)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                }
                else
                {
                    unsafe
                    {
                        Vector4* color = ImGui.GetStyleColorVec4(ImGuiCol.ButtonHovered);
                        ImGui.PushStyleColor(ImGuiCol.Button, *color);
                    }
                }

                if (ImGui.Button(actor.Name))
                {
                    EditorSubsystem.SelectedActor = actor;
                }
                ImGui.PopStyleColor();
                /*
                if (ImGui.TreeNode(actor.Name))
                {

                    ImGui.TreePop();
                    // ImGui.Button(actor.Name);
                }
                */
            }

            ImGui.TreePop();
        }
        ImGui.End();
    }
}
