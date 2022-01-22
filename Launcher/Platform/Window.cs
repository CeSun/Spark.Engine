using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using LiteEngine.Core;

namespace Launcher.Platform
{
    public class Window : GameWindow
    {
        private readonly float[] _vertices =
        {
            -0.5f, -0.5f, 0.0f, // Bottom-left vertex
             0.5f, -0.5f, 0.0f, // Bottom-right vertex
             0.0f,  0.5f, 0.0f  // Top vertex
        };
        private int _vertexBufferObject;

        private int _vertexArrayObject;


        public Window() : base(GameWindowSettings.Default, new NativeWindowSettings() {
                Size = new Vector2i(800, 600),
                Title = "Test",
                Flags = ContextFlags.ForwardCompatible, })
        {
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            model = new Model();
            model.LoadModel(@"C:\Users\cesun\Desktop\CSO2\LEET\leet.FBX");
            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        }
        Model model;
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            model.Draw(e.Time);

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
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
        }

        protected override void OnUnload()
        {
          

            base.OnUnload();
        }
    }
}
