using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using LiteEngine.Core;
using OpenTK.Windowing.Common.Input;
using OpenTK.ImGui;
using ImGuiNET;

namespace Launcher.Platform
{
    public class Window : GameWindow
    {
        ImGuiController _controller;
        public Window() : base(
            GameWindowSettings.Default, 
            new NativeWindowSettings() {
                Size = new Vector2i(800, 600),
                Title = "LiteEngine",
                Flags = ContextFlags.ForwardCompatible, 
            })
        {
            _controller = new ImGuiController(ClientSize.X, ClientSize.Y);
        }
        
        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            GL.Enable(EnableCap.DepthTest);
        }
        Model model;
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.Enable(EnableCap.DepthTest);
            base.OnRenderFrame(e);
            _controller.Update(this, (float)e.Time);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            Scene.Current.Draw(e.Time);

            ImGui.ShowDemoWindow();
            _controller.Render();

            SwapBuffers();
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            var input = KeyboardState;
            if (input.IsKeyDown(Keys.Escape))
            {
                Close();
            }
            if (input.IsKeyPressed(Keys.P))
            {
                Console.WriteLine($"Camera: {Camera.Current.LocalPosition}");
            }
            Vector3 direction = Vector3.Zero;
            if (input.IsKeyDown(Keys.A) || input.IsKeyDown(Keys.D))
            {
                if (input.IsKeyDown(Keys.A))
                {
                    direction.X = 1;
                }
                if (input.IsKeyDown(Keys.D))
                {
                    direction.X = -1;
                }

            }
            if (input.IsKeyDown(Keys.W) || input.IsKeyDown(Keys.S))
            {
                if (input.IsKeyDown(Keys.W))
                {
                    direction.Z = 1;
                }

                if (input.IsKeyDown(Keys.S))
                {
                    direction.Z = -1;
                }
            }
            if (direction.Length == 0)
                return;
            direction.Normalize();
            var T = Camera.Current.Transform.ClearTranslation().ClearScale();
            var distance =  new Vector4(direction, 1.0f) * T;
            distance *= (float)e.Time * 10.0F;
            Camera.Current.LocalPosition += new Vector3(distance.X, distance.Y, distance.Z);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y); 
            _controller.WindowResized(ClientSize.X, ClientSize.Y);
        }
        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);
            if (MouseState.IsButtonDown(MouseButton.Left))
            {
                CursorVisible = false;
                var mouseX = e.DeltaX;
                var mouseY = e.DeltaY;
                Camera.Current.LocalRotation *= Quaternion.FromEulerAngles((float)(mouseY * 0.01), (float)(mouseX * -0.01), 0);
            }
            else
            {
                CursorVisible = true;
            }

        }
        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            _controller.PressChar((char)e.Unicode);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            _controller.MouseScroll(e.Offset);
        }

        protected override void OnUnload()
        {
            base.OnUnload();
        }
    }
}
