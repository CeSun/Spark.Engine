using LiteEngine;
using LiteEngine.Sdk;

namespace Game;
public class FPSGame : IGame
{
    public void OnFini()
    {

    }

    public void OnInit()
    {
        Engine.Instance.Gl.ClearColor(1, 0, 0, 1);
    }

    public void OnLevelLoaded()
    {

    }

    public void OnRender()
    {
        // Engine.Instance.Gl.Clear(Silk.NET.OpenGL.ClearBufferMask.ColorBufferBit);
    }

    public void OnRoundEnd()
    {

    }

    public void OnRoundStart()
    {

    }

    public void OnUpdate(float deltaTime)
    {

    }
}
