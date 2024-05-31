using Editor.Subsystem;
using ImGuiNET;
using Silk.NET.Input;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.GUI;
using Spark.Util;
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
    private readonly EditorSubsystem _editorSubsystem;
    public OutlinerPanel(Level level) : base(level)
    {
        var system = level.Engine.GetSubSystem<EditorSubsystem>();
        if (system != null)
            _editorSubsystem = system;
        else
            throw new Exception("no editor subsystem");
    }

    public override void Render(double deltaTime)
    {
        base.Render(deltaTime);
        ImGui.Begin("Outliner##outliner", ImGuiWindowFlags.NoCollapse);
    
        if(ImGui.TreeNodeEx("All Actors", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var actor in _editorSubsystem.LevelWorld.CurrentLevel.Actors)
            {
                if (actor.IsEditorActor == true)
                    continue;
                var cond = _editorSubsystem.SelectedActor != actor;
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
                    if (cond == false)
                    {
                        _editorSubsystem.EditorCameraActor.WorldLocation = actor.WorldLocation + actor.ForwardVector * 10 + actor.UpVector * 5;
                        _editorSubsystem.EditorCameraActor.WorldRotation = actor.WorldRotation;

                        _editorSubsystem.EditorCameraActor.WorldRotation *= Quaternion.CreateFromYawPitchRoll(180F.DegreeToRadians(), 0, 0);
                    }
                    else
                    {
                        _editorSubsystem.SelectedActor = actor;
                    }
                }
                ImGui.PopStyleColor();
            }

            ImGui.TreePop();
        }

        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _editorSubsystem.SelectedActor = null;

        }
        ImGui.End();

    }
}
