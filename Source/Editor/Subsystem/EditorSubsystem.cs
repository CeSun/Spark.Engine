using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Components;
using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Subsystem;

[Subsystem(Enable = true)]
public class EditorSubsystem : BaseSubSystem
{
    public Type? ClickType = null;
    public override bool ReceiveUpdate => true;

    public bool IsProjectOpened = false;

    public string? ProjectPath = null;

    public World? LevelWorld = null;

    public CameraActor? EditorCameraActor = null;

    private Dictionary<string, object?> Maps = new Dictionary<string, object?>();
    public T? GetValue<T>(string key)
    {
        if (Maps.TryGetValue(key, out var value))
        {
            if (value is T t)
                return t;
            return default;
        }
        return default;
    }

    public void SetValue(string key, object? obj)
    {
        Maps[key] = obj;
    }
    public EditorSubsystem(Engine engine) : base(engine)
    {

    }

    public Actor? SelectedActor;
    public override void BeginPlay()
    {
        base.BeginPlay();
        var world = new World(CurrentEngine);
        world.WorldMainRenderTarget = world.SceneRenderer.CreateRenderTarget(100, 100, 1);
        CurrentEngine.Worlds.Add(world);
        world.BeginPlay();
        LevelWorld = world;
        var cameraActor = new CameraActor(world.CurrentLevel);
        cameraActor.IsEditorActor = true;
        EditorCameraActor = cameraActor;
        var SkyboxActor = new Actor(world.CurrentLevel, "SkyboxActor");
        var SkyboxComponent = new SkyboxComponent(SkyboxActor);
        SkyboxComponent.SkyboxHDR = CurrentEngine.AssetMgr.Load<TextureHDR>(CurrentEngine.GameName + "/Assets/SkyboxHDR2.asset");

        /*
        using (var sw = FileSystem.Instance.GetStreamWriter("SkyboxHDR.asset"))
        {
            TextureHDR.LoadFromFile("kloofendal_43d_clear_puresky_2k.hdr").Serialize(new BinaryWriter(sw.BaseStream), CurrentEngine);
        }
        */
    }
}
