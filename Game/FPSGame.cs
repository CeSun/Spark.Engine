using LiteEngine;
using LiteEngine.Core.Actors;
using LiteEngine.Sdk;
using Silk.NET.OpenGL;

namespace Game;
public class FPSGame : IGame
{
    public GL gl { get => Engine.Instance.Gl; }
    public void OnFini()
    {

    }

    public void OnInit()
    {
        var camera = new CameraActor();

    }

    public void OnLevelLoaded()
    {

    }

    public void OnRender()
    {

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
