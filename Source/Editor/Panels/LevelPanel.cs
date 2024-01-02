using Editor.Subsystem;
using ImGuiNET;
using Spark.Engine;
using Spark.Engine.GUI;
using Spark.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels;

public class LevelPanel : ImGUIWindow
{
    EditorSubsystem EditorSubsystem;
    public LevelPanel(Level level) : base(level)
    {
        var system = level.Engine.GetSubSystem<EditorSubsystem>();
        if (system != null)
            EditorSubsystem = system;
        else
            throw new Exception("no editor subsystem");
    }

    public bool IsPressed = false;

    public Vector2 PressedPosition;
    public override void Render(double deltaTime)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.Begin("New Level##levelpanel");

        var windowSize = ImGui.GetContentRegionAvail();
        if (EditorSubsystem.LevelWorld != null && EditorSubsystem.LevelWorld.WorldMainRenderTarget!=null)
        {
            var rt = EditorSubsystem.LevelWorld.WorldMainRenderTarget;
            rt.Resize((int)windowSize.X, (int)windowSize.Y);
            var uv1 = new Vector2(0, 0);
            var uv2 = new Vector2(rt.Width / (float)rt.BufferWidth, (rt.Height / (float)rt.BufferHeight));
            ImGui.Image((nint)EditorSubsystem.LevelWorld.WorldMainRenderTarget.GBufferIds[0], windowSize, uv1, uv2);


            if(ImGui.IsWindowHovered())
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && IsPressed == false)
                {
                    IsPressed = true;
                    PressedPosition = ImGui.GetMousePos();
                }
            }

        }

        if (EditorSubsystem.EditorCameraActor != null)
        {
            if (IsPressed == true)
            {
                var currentPos = ImGui.GetMousePos();
                var deltaPos = currentPos - PressedPosition;
                EditorSubsystem.EditorCameraActor.WorldRotation *= Quaternion.CreateFromYawPitchRoll(-1 * deltaPos.X.DegreeToRadians(),  deltaPos.Y.DegreeToRadians(), 0);
                PressedPosition = currentPos;
            }
        }
        if (IsPressed == true && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            IsPressed = false;
        }

        ImGui.End();
    }
}
