using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor;

[Subsystem(Enable = true)]
public class EditorSubsystem : BaseSubSystem
{
    public override bool ReceiveUpdate => true;

    public bool IsProjectOpened = false;

    public string? ProjectPath = null;

    public World? LevelWorld = null;
    public EditorSubsystem(Engine engine) : base(engine)
    {

    }


    public override void BeginPlay()
    {
        base.BeginPlay();
        var world = new World(this.CurrentEngine);
        world.WorldMainRenderTarget = world.SceneRenderer.CreateRenderTarget(100, 100, 1);
        CurrentEngine.Worlds.Add(world);
        world.BeginPlay();
        LevelWorld = world;
        var cameraActor = new CameraActor(world.CurrentLevel);
        var SkyboxActor = new Actor(world.CurrentLevel);
        var SkyboxComponent = new SkyboxComponent(SkyboxActor);
        SkyboxComponent.SkyboxHDR = TextureHDR.LoadFromFile("");

    }
}
