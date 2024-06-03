using Editor.Subsystem;
using ImGuiNET;
using Silk.NET.Input;
using Spark.Util;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Editor.Panels;

public class LevelPanel : BasePanel
{
    private readonly EditorSubsystem _editorSubsystem;
    public LevelPanel(ImGuiSubSystem imGuiSubSystem) : base(imGuiSubSystem)
    {
        _editorSubsystem = Engine.GetSubSystem<EditorSubsystem>()!;
    }

    public bool IsPressed;

    public Vector2 PressedPosition;
    public override void Render(double deltaTime)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.Begin("New Level##levelPanel");

        var windowSize = ImGui.GetContentRegionAvail();
        if (_editorSubsystem.World is { WorldMainRenderTarget: not null })
        {
            var rt = _editorSubsystem.World.WorldMainRenderTarget;
            rt.Resize((int)windowSize.X, (int)windowSize.Y);
            var uv1 = new Vector2(0, (rt.Height / (float)rt.BufferHeight));
            var uv2 = new Vector2(rt.Width / (float)rt.BufferWidth, 0);
            ImGui.Image((nint)_editorSubsystem.World.WorldMainRenderTarget.GBufferIds[0], windowSize, uv1, uv2);
            if (ImGui.BeginDragDropTarget())
            {
                var payLoad = ImGui.AcceptDragDropPayload("PLACE_ACTOR_TYPE");
                unsafe
                {
                    if (payLoad.NativePtr != null)
                    {
                        var gcHandle = Marshal.PtrToStructure<GCHandle>(payLoad.Data);

                        if (gcHandle.Target != null)
                        {
                            var type = (Type)gcHandle.Target;
                            var level = _editorSubsystem.World.CurrentLevel;
                            Activator.CreateInstance(type, [level, ""]);
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }

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
                if(Engine.MainKeyBoard.IsKeyPressed(Key.W))
                {
                    movement.Y = 1;
                }
                else if (Engine.MainKeyBoard.IsKeyPressed(Key.S))
                {
                    movement.Y = -1;
                }
                else if (Engine.MainKeyBoard.IsKeyPressed(Key.A))
                {
                    movement.X = -1;
                }
                else if (Engine.MainKeyBoard.IsKeyPressed(Key.D))
                {
                    movement.X = 1;
                }
                if (movement != Vector2.Zero)
                {
                    movement = Vector2.Normalize(movement);

                    if (_editorSubsystem.EditorCameraActor != null)
                    {
                        _editorSubsystem.EditorCameraActor.WorldLocation += (_editorSubsystem.EditorCameraActor.ForwardVector * movement.Y + _editorSubsystem.EditorCameraActor.RightVector * movement.X) * (float)deltaTime;
                    }

                }
            }

        }

        if (_editorSubsystem.EditorCameraActor != null)
        {
            if (IsPressed)
            {
                var currentPos = ImGui.GetMousePos();
                var deltaPos = currentPos - PressedPosition;
                var euler = _editorSubsystem.EditorCameraActor.WorldRotation.ToEuler();

                var pitch = euler.X.RadiansToDegree() - deltaPos.Y * 0.5f;
                var yaw = euler.Y.RadiansToDegree() - deltaPos.X * 0.5f;
                if (pitch > 89)
                {
                    pitch = 89;
                }
                if (pitch < -89)
                {
                    pitch = -89;
                }
                _editorSubsystem.EditorCameraActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(yaw.DegreeToRadians(), pitch.DegreeToRadians(), 0);
               PressedPosition = currentPos;
            }
        }
        if (IsPressed && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            IsPressed = false;
        }

        if (ImGui.IsMouseHoveringRect(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize()) && _editorSubsystem.ClickType != null && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (_editorSubsystem.World != null)
            {
                var level = _editorSubsystem.World.CurrentLevel;
                Activator.CreateInstance(_editorSubsystem.ClickType, [level, ""]);
            }
            _editorSubsystem.ClickType = null;
        }

        ImGui.End();
    }
}
