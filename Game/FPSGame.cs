using LiteEngine;
using LiteEngine.Core.Actors;
using LiteEngine.Core.Components;
using LiteEngine.Sdk;
using Silk.NET.OpenGL;

namespace Game;
public class FPSGame : IGame
{
    public GL gl { get => Engine.Instance.Gl; }
    public void OnFini()
    {

    }
    class TestActor : Actor
    {
        StaticMeshComponent staticMeshComponent;
        public TestActor() : base()
        {
            staticMeshComponent = new StaticMeshComponent(RootComponent, "meshComp");
        }

    }
    public void OnInit()
    {
        var actor = new CameraActor();
        var testActor = new TestActor();

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
