using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
namespace LiteEngine.Platform;
public class Window : GameWindow
{
    public Window() : base(new GameWindowSettings { IsMultiThreaded = false, RenderFrequency = 0, UpdateFrequency = 60 }, NativeWindowSettings.Default)
    {
        Title = "LiteEngine"; 
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        Game.Instance.Init();
    }
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        Game.Instance.Update((float)args.Time);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        Game.Instance.Render(); 
        SwapBuffers();

    }

    protected override void OnUnload()
    {
        base.OnUnload();
        Game.Instance.Fini();
    }

}