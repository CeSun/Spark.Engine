using Spark.Engine.Render;

namespace Spark.Engine.World;

public class World
{
    public Engine Engine { get; set; }

    public RenderTarget? WorldMainRenderTarget;

    public UpdateManager UpdateManager { get;  private set; } = new UpdateManager();
    public World(Engine engine)
    {
        Engine = engine;
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

}
