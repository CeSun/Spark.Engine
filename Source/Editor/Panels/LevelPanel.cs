using Editor.Subsystem;
using ImGuiNET;
using Silk.NET.Input;
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
            var uv1 = new Vector2(0, (rt.Height / (float)rt.BufferHeight));
            var uv2 = new Vector2(rt.Width / (float)rt.BufferWidth, 0);
            ImGui.Image((nint)EditorSubsystem.LevelWorld.WorldMainRenderTarget.GBufferIds[0], windowSize, uv1, uv2);


            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();

            if(ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && IsPressed == false)
                {
                    if (ImGui.IsMouseHoveringRect(min, max))
                    {
                        IsPressed = true;
                        PressedPosition = ImGui.GetMousePos();
                    }
                }
            }

            if (ImGui.IsMouseHoveringRect(min, max))
            {
                Vector2 movement = Vector2.Zero;
                if(level.Engine.MainKeyBoard.IsKeyPressed(Key.W))
                {
                    movement.Y = 1;
                }
                else if (level.Engine.MainKeyBoard.IsKeyPressed(Key.S))
                {
                    movement.Y = -1;
                }
                else if (level.Engine.MainKeyBoard.IsKeyPressed(Key.A))
                {
                    movement.X = -1;
                }
                else if (level.Engine.MainKeyBoard.IsKeyPressed(Key.D))
                {
                    movement.X = 1;
                }
                if (movement != Vector2.Zero)
                {
                    movement = Vector2.Normalize(movement);

                    EditorSubsystem.EditorCameraActor.WorldLocation += (EditorSubsystem.EditorCameraActor.ForwardVector * movement.Y + EditorSubsystem.EditorCameraActor.RightVector * movement.X) * (float)deltaTime;

                }
            }

        }

        if (EditorSubsystem.EditorCameraActor != null)
        {
            if (IsPressed == true)
            {
                var currentPos = ImGui.GetMousePos();
                var deltaPos = currentPos - PressedPosition;
                EditorSubsystem.EditorCameraActor.WorldRotation *= Quaternion.CreateFromYawPitchRoll(-1 * deltaPos.X.DegreeToRadians(), -1 *  deltaPos.Y.DegreeToRadians(), 0);
                PressedPosition = currentPos;
            }
        }
        if (IsPressed == true && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            IsPressed = false;
        }

        if (ImGui.IsMouseHoveringRect(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize()) && EditorSubsystem.ClickType != null && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (EditorSubsystem.LevelWorld != null)
            {
                var level = EditorSubsystem.LevelWorld.CurrentLevel;
                var actor = Activator.CreateInstance(EditorSubsystem.ClickType, [level, ""]);
            }
            EditorSubsystem.ClickType = null;
        }

        ImGui.End();
    }
}
