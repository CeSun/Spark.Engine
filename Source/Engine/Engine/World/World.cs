using Silk.NET.OpenGLES;
using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Components;
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
        WorldMainRenderTarget = new RenderTarget() { IsDefaultRenderTarget = true };
        if (GraphicsApi != null)
        {
            RenderWorld = new RenderWorld();
            if (engine.SceneRenderer != null)
            {
                engine.SceneRenderer.AddRunOnRendererAction(renderer =>
                {
                    engine.RenderWorlds.Add(RenderWorld);
                });
            }
        }
    }

    public void BeginPlay()
    {

    }
    public void Update(double deltaTime)
    {
        UpdateManager.Update(deltaTime);
    }
    
    public void Destory()
    {
        var engine = Engine;
        var renderWorld = this.RenderWorld;
        if (engine.SceneRenderer != null && renderWorld != null)
        {
            engine.SceneRenderer.AddRunOnRendererAction(renderer =>
            {
                engine.RenderWorlds.Remove(renderWorld);
            });
        }
    }

    public IReadOnlySet<PrimitiveComponent> PrimitiveComponents => _primitiveComponents;

    private HashSet<PrimitiveComponent> _primitiveComponents = new HashSet<PrimitiveComponent>();



    public IReadOnlySet<Actor> Actors => _actors;

    private HashSet<Actor> _actors = new HashSet<Actor>();
    public void AddActor(Actor actor)
    {
        if (_actors.Contains(actor))
            return;
        _actors.Add(actor);
    }

    public void RemoveActor(Actor actor)
    {
        if (_actors.Contains(actor) == false)
            return;
        _actors.Remove(actor);
    }

    public void AddComponent(PrimitiveComponent component)
    {
        if (_primitiveComponents.Contains(component))
            return;
        _primitiveComponents.Add(component);
    }

    public void RemoveComponent(PrimitiveComponent component) 
    {
        if ( _primitiveComponents.Contains(component) == false)
            return;
        _primitiveComponents.Remove(component);
    }
}
