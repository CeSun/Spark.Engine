using Editor.Subsystem;
using ImGuiNET;
using Editor.GUI;
using Spark.Util;
using System.Numerics;

namespace Editor.Panels;

public class OutlinerPanel : BasePanel
{
    private readonly EditorSubsystem _editorSubsystem;
    public OutlinerPanel(ImGuiSubSystem imGuiSubSystem) : base(imGuiSubSystem)
    {
        _editorSubsystem = Engine.GetSubSystem<EditorSubsystem>()!;
    }

    public override void Render(double deltaTime)
    {
        base.Render(deltaTime);
        if (_editorSubsystem.World == null)
            return;
        ImGui.Begin("Outliner##outliner", ImGuiWindowFlags.NoCollapse);
    
        if(ImGui.TreeNodeEx("All Actors", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var actor in _editorSubsystem.World.CurrentLevel.Actors)
            {
                if (actor.IsEditorActor)
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
