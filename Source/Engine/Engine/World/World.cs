using Silk.NET.OpenGLES;
using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Components;
using Spark.Util;
using System.Drawing;

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
        WorldMainRenderTarget = new RenderTarget() { IsDefaultRenderTarget = true };
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
        var CameraActor = new CameraActor(this)
        {
            WorldLocation = new System.Numerics.Vector3(1, 22, 3),
            WorldRotation = System.Numerics.Quaternion.CreateFromYawPitchRoll(30f.DegreeToRadians(), 1, 1),
            ClearColor = Color.Red,
            ClearFlag  = CameraClearFlag.Depth,
            Order = 3
        };

        var DecalActor = new DecalActor(this)
        {
            WorldLocation = new System.Numerics.Vector3(1, 22, 3),
        };
        var DirectionLightActor = new DirectionLightActor(this)
        {
            WorldLocation = new System.Numerics.Vector3(13, 22, 3),
        };
        var PointLightActor = new PointLightActor(this)
        {
            WorldLocation = new System.Numerics.Vector3(12, 22, 33),
        };
        var SpotLightActor = new SpotLightActor(this)
        {
            WorldLocation = new System.Numerics.Vector3(21, 22, 23),
            LightStrength = 10,
            Color = Color.DarkBlue,
        };
        var StaticMeshActor = new StaticMeshActor(this)
        {
            WorldLocation = new System.Numerics.Vector3(1, 22, 3),
        };
    }

    public void Update(double deltaTime)
    {
        UpdateManager.Update(deltaTime);
        UpdateRenderProperties();
    }
    
    private void UpdateRenderProperties()
    {
        if (Engine.SceneRenderer != null && RenderWorld != null)
        {
            foreach (var component in RenderDirtyComponent)
            {
                var propertiesStructPointer = component.GetPrimitiveComponentProperties();
                Engine.SceneRenderer.AddRunOnRendererAction(renderer =>
                {
                    RenderWorld.RenderPropertiesQueue.Add(propertiesStructPointer);
                });
            }
            RenderDirtyComponent.Clear();
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
