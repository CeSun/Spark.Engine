using LiteEngine.Core;
using LiteEngine.Core.GameObjects;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace LiteEngine.Platform
{
    public class Desktop : GameWindow
    {
        public Desktop() : base(new GameWindowSettings { RenderFrequency = 0, UpdateFrequency = 100, IsMultiThreaded = false }, new NativeWindowSettings { Title = "小引擎" } )
        {
            GameObject.FixedDeltaTime = 1 / this.UpdateFrequency;
            Console.WriteLine(GameObject.FixedDeltaTime);
        }

        protected override void OnLoad()
        {
            GL.ClearColor(Color4.Gray);
            base.OnLoad();
            Game.Instance.Load();
        }
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            GameObject.DeltaTime = args.Time;
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            Game.Instance.Update();
            SwapBuffers();
        }


        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
            Game.Instance.FixedUpdate();
        }


        protected override void OnUnload()
        {
            base.OnUnload();
            Game.Instance.UnLoad();
        }


        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0,0,this.Size.X, this.Size.Y);
        }

    }
}
