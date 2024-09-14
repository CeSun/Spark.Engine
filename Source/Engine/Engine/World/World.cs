using Silk.NET.OpenGLES;
using Spark.Engine.Assets;
using Spark.Engine.Render;

namespace Spark.Engine;

public class World
{
    public GL? GraphicsApi { get; set; }
    public Engine Engine { get; set; }

    public RenderTarget? WorldMainRenderTarget;
    public UpdateManager UpdateManager { get;  private set; } = new UpdateManager();
    public World(Engine engine)
    {
        Engine = engine;
        GraphicsApi = Engine.GraphicsApi;
        if (GraphicsApi != null)
        {
            SceneRenderer = new DeferredRenderer(GraphicsApi);
            WorldMainRenderTarget = new RenderTarget() { IsDefaultRenderTarget = true };
        }
    }

    public IRenderer? SceneRenderer;

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
