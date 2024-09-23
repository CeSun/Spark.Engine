using Silk.NET.OpenGLES;
using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Components;
using Spark.Util;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core;

public class World
{
    public GL? GraphicsApi { get; set; }
    public Engine Engine { get; set; }
    public UpdateManager UpdateManager { get;  private set; } = new UpdateManager();
    public RenderTarget? WorldMainRenderTarget { get; set; }
    public WorldProxy? RenderWorld { get; set; }

    private HashSet<PrimitiveComponent> RenderDirtyComponent = [];

    public void AddRenderDirtyComponent(PrimitiveComponent component)
    {
        if (RenderDirtyComponent.Contains(component))
            return;
        RenderDirtyComponent.Add(component);
    }
    public World(Engine engine)
    {
        Engine = engine;
        GraphicsApi = Engine.GraphicsApi;
        var width = 0;
        var height = 0;
        if (engine.MainView != null)
        {
            width = engine.MainView.Size.X;
            height = engine.MainView.Size.Y;
        }
        WorldMainRenderTarget = new RenderTarget() { IsDefaultRenderTarget = true, Width = width, Height = height };
        if (GraphicsApi != null)
        {
            RenderWorld = new WorldProxy();
            if (engine.SceneRenderer != null)
            {
                engine.SceneRenderer.AddRunOnRendererAction(renderer =>
                {
                    engine.SceneRenderer.RenderWorlds.Add(RenderWorld);
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
        UpdateRenderProperties();
    }
    
    private unsafe void UpdateRenderProperties()
    {
        if (Engine.SceneRenderer != null && RenderWorld != null)
        {
            var pointer = Marshal.AllocHGlobal(Unsafe.SizeOf<nint>() * RenderDirtyComponent.Count);
            var len = RenderDirtyComponent.Count;
            var array = new Span<nint>((void*)pointer, len);
            int i = 0;
            foreach (var component in RenderDirtyComponent)
            {
                array[i++] = component.GetPrimitiveComponentProperties();
            }
            RenderDirtyComponent.Clear();
            Engine.SceneRenderer.AddRunOnRendererAction(renderer =>
            {
                var array = new Span<nint>((void*)pointer, len);
                RenderWorld.AddRenderPropertiesList.AddRange(array);
                Marshal.FreeHGlobal(pointer);
            });
        }
    }

    public void Destory()
    {
        var engine = Engine;
        var renderWorld = this.RenderWorld;
        if (engine.SceneRenderer != null && renderWorld != null)
        {
            engine.SceneRenderer.AddRunOnRendererAction(renderer =>
            {
                renderWorld.Destory(renderer);
                renderer.RenderWorlds.Remove(renderWorld);
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
