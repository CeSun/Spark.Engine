using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Render;

namespace Spark.Core;

public class World
{
    public GL? GraphicsApi { get; set; }
    public Engine Engine { get; set; }

    public UpdateManager UpdateManager { get;  private set; } = new UpdateManager();

    public RenderTarget? WorldMainRenderTarget { get; set; }
    public RenderWorld? RenderWorld { get; set; } 
    public World(Engine engine)
    {
        Engine = engine;
        GraphicsApi = Engine.GraphicsApi;
        if (GraphicsApi != null)
        {
            WorldMainRenderTarget = new RenderTarget() { IsDefaultRenderTarget = true };
            RenderWorld = new RenderWorld();
        }
    }

    public void BeginPlay()
    {

    }
    public void Update(double deltaTime)
    {
        UpdateManager.Update(deltaTime);
    }
    public void Render(double deltaTime)
    {

    }

 
    public void Destory()
    {

    }

    public void Resize(int width, int height)
    {
        OnResize?.Invoke(width, height);
    }
    public event Action<int, int>? OnResize;

}
