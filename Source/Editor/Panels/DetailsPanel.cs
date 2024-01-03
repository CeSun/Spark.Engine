﻿using Editor.Subsystem;
using ImGuiNET;
using Spark.Engine;
using Spark.Engine.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels
{
    public class DetailsPanel : ImGUIWindow
    {
        EditorSubsystem EditorSubsystem;
        public DetailsPanel(Level level) : base(level)
        {
            var system = level.Engine.GetSubSystem<EditorSubsystem>();
            if (system != null)
                EditorSubsystem = system;
            else
                throw new Exception("no editor subsystem");
           
        }

        public override void Render(double deltaTime)
        {

            ImGui.Begin("Details##details");
            if (EditorSubsystem.SelectedActor != null)
            {
                ImGui.Text("Name: ");
                ImGui.SameLine();
                var name = EditorSubsystem.SelectedActor.Name;
                ImGui.InputText("##actorName",ref name, 32);


                ImGui.NewLine();

                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 80);
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 8));

                ImGui.Text("Location");
                ImGui.Text("Rotation");
                ImGui.Text("Scale"); 
                ImGui.PopStyleVar();

                ImGui.NextColumn();


                var location = EditorSubsystem.SelectedActor.WorldLocation;
                ImGui.Text("X");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50);
                ImGui.InputFloat("##locationX", ref location.X);
                ImGui.SameLine();
                ImGui.Text("Y");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50);
                ImGui.InputFloat("##locationY", ref location.Y);
                ImGui.SameLine();
                ImGui.Text("Z");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50);
                ImGui.InputFloat("##locationZ", ref location.Z);

                ImGui.NewLine();

                
                var scale = EditorSubsystem.SelectedActor.WorldScale;
                ImGui.Text("X");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50);
                ImGui.InputFloat("##scaleX", ref scale.X);
                ImGui.SameLine();
                ImGui.Text("Y");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50);
                ImGui.InputFloat("##scaleY", ref scale.Y);
                ImGui.SameLine();
                ImGui.Text("Z");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50);
                ImGui.InputFloat("##scaleZ", ref scale.Z);

            }

            ImGui.End();



        }
    }
}
