using Spark.Engine.Render;
using System.Drawing;

namespace Spark.Engine;

public class World
{
    public Engine Engine { get; set; }

    public RenderTarget? WorldMainRenderTarget;
    public UpdateManager UpdateManager { get;  private set; } = new UpdateManager();
    public World(Engine engine)
    {
        Engine = engine;
        SceneRenderer = new DeferredRenderer(Engine);
    }

    public IRenderer SceneRenderer;

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
