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
    private readonly EditorSubsystem _editorSubsystem;
    public LevelPanel(Level level) : base(level)
    {
        var system = level.Engine.GetSubSystem<EditorSubsystem>();
        if (system != null)
            _editorSubsystem = system;
        else
            throw new Exception("no editor subsystem");
    }

    public bool IsPressed = false;

    public Vector2 PressedPosition;
    public override void Render(double deltaTime)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.Begin("New Level##levelPanel");

        var windowSize = ImGui.GetContentRegionAvail();
        if (_editorSubsystem.LevelWorld is { WorldMainRenderTarget: not null })
        {
            var rt = _editorSubsystem.LevelWorld.WorldMainRenderTarget;
            rt.Resize((int)windowSize.X, (int)windowSize.Y);
            var uv1 = new Vector2(0, (rt.Height / (float)rt.BufferHeight));
            var uv2 = new Vector2(rt.Width / (float)rt.BufferWidth, 0);
            ImGui.Image((nint)_editorSubsystem.LevelWorld.WorldMainRenderTarget.GBufferIds[0], windowSize, uv1, uv2);


            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();

            if(ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Right) && IsPressed == false)
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

                    _editorSubsystem.EditorCameraActor.WorldLocation += (_editorSubsystem.EditorCameraActor.ForwardVector * movement.Y + _editorSubsystem.EditorCameraActor.RightVector * movement.X) * (float)deltaTime;

                }
            }

        }

        if (_editorSubsystem.EditorCameraActor != null)
        {
            if (IsPressed == true)
            {
                var currentPos = ImGui.GetMousePos();
                var deltaPos = currentPos - PressedPosition;
                var euler = _editorSubsystem.EditorCameraActor.WorldRotation.ToEuler();

                var pitch = euler.X.RadiansToDegree() - deltaPos.Y;
                if (pitch > 89)
                {
                    pitch = 89;
                }
                if (pitch < -89)
                {
                    pitch = -89;
                }
                _editorSubsystem.EditorCameraActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(euler.Y - deltaPos.X.DegreeToRadians(), pitch.DegreeToRadians(), 0);
               PressedPosition = currentPos;
            }
        }
        if (IsPressed && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            IsPressed = false;
        }

        if (ImGui.IsMouseHoveringRect(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize()) && _editorSubsystem.ClickType != null && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (_editorSubsystem.LevelWorld != null)
            {
                var level = _editorSubsystem.LevelWorld.CurrentLevel;
                var actor = Activator.CreateInstance(_editorSubsystem.ClickType, [level, ""]);
            }
            _editorSubsystem.ClickType = null;
        }

        ImGui.End();
    }
}
