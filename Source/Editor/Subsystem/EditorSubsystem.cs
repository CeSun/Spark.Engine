using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Attributes;
using Spark.Engine.Components;

namespace Editor.Subsystem;

[Subsystem(Enable = true)]
public class EditorSubsystem(Engine engine) : BaseSubSystem(engine)
{
    public Type? ClickType = null;
    public override bool ReceiveUpdate => true;

    public World? World = null;

    public CameraActor? EditorCameraActor = null;

    private readonly Dictionary<string, object?> _maps = [];

    public string CurrentPath = Directory.GetCurrentDirectory().Replace("\\", "/");
    public T? GetValue<T>(string key)
    {
        if (_maps.TryGetValue(key, out var value))
        {
            if (value is T t)
                return t;
            return default;
        }
        return default;
    }

    public void SetValue(string key, object? obj)
    {
        _maps[key] = obj;
    }

    public Actor? SelectedActor;
    public override void BeginPlay()
    {
        base.BeginPlay();
        var world = new World(CurrentEngine);
        world.WorldMainRenderTarget = world.SceneRenderer.CreateRenderTarget(100, 100, 1);
        CurrentEngine.Worlds.Add(world);
        world.BeginPlay();
        World = world;
        var cameraActor = new CameraActor(world.CurrentLevel)
        {
            NearPlaneDistance = 1
        };
        EditorCameraActor = cameraActor;
        var skyboxActor = new Actor(world.CurrentLevel, "SkyboxActor");
        _ = new SkyboxComponent(skyboxActor);
    }
}
