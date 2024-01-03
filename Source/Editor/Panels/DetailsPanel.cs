using Editor.Subsystem;
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
                var ContentWidth = ImGui.GetContentRegionAvail().X;
                bool Modify = false;
                ImGui.Columns(2);
                var leftWidth = ContentWidth * 0.2;
                if (leftWidth < 80)
                    leftWidth = 80;
                ImGui.SetColumnWidth(0, (float)leftWidth);
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 8));
                ImGui.Text("Name: ");
                ImGui.Text("Location");
                ImGui.Text("Rotation");
                ImGui.Text("Scale"); 
                ImGui.PopStyleVar();



                ImGui.NextColumn();
                var Name = EditorSubsystem.SelectedActor.Name;
                var width = ImGui.GetColumnWidth();
                ImGui.SetNextItemWidth(width);
                ImGui.InputText("##Name",ref Name, 32);

                var labelWidth = width / 3 * 0.3f;
                var InputWidth = width / 3 * 0.7f;
                var location = EditorSubsystem.SelectedActor.WorldLocation;
                ImGui.Text("X");
                ImGui.SameLine(labelWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##locationX", ref location.X);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    Modify = true;
                }
                ImGui.SameLine();;
                ImGui.Text("Y");
                ImGui.SameLine(labelWidth * 2 + InputWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##locationY", ref location.Y);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    Modify = true;
                }

                ImGui.SameLine();
                ImGui.SetNextItemWidth(labelWidth);
                ImGui.Text("Z");
                ImGui.SameLine(labelWidth * 3 + InputWidth * 2);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##locationZ", ref location.Z);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    Modify = true;
                }
                if (Modify == true)
                {
                    EditorSubsystem.SelectedActor.WorldLocation = location;
                }

                float Yaw = 0, Pitch = 0, Roll = 0;
                ImGui.Text("Yaw");
                ImGui.SameLine(labelWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##Yaw", ref Yaw);
                ImGui.SameLine();
                
                ImGui.Text("Pitch");
                ImGui.SameLine(labelWidth * 2 + InputWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##Pitch", ref Pitch);
                ImGui.SameLine();
                
                ImGui.Text("Roll");
                ImGui.SameLine(labelWidth * 3 + InputWidth * 2);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##Roll", ref Roll);

                Modify = false;
                var scale = EditorSubsystem.SelectedActor.WorldScale;
                
                ImGui.Text("X");
                ImGui.SameLine(labelWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##scaleX", ref scale.X); 
                if (ImGui.IsItemDeactivatedAfterEdit())
                    Modify = true;
                ImGui.SameLine();
                
                ImGui.Text("Y");
                ImGui.SameLine(labelWidth * 2 + InputWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##scaleY", ref scale.Y);
                if (ImGui.IsItemDeactivatedAfterEdit())
                    Modify = true;
                ImGui.SameLine();
                
                ImGui.Text("Z");
                ImGui.SameLine(labelWidth * 3 + InputWidth * 2);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##scaleZ", ref scale.Z);
                if (ImGui.IsItemDeactivatedAfterEdit())
                    Modify = true;
                if (Modify)
                    cdcdcsxdEditorSubsystem.SelectedActor.WorldScale = scale;
            }

            ImGui.End();



        }
    }
}
