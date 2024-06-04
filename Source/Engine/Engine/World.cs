using Spark.Engine.Actors;
using Spark.Engine.Render;
using Spark.Engine.Render.Renderer;
using Spark.Util;

namespace Spark.Engine;

public class World
{
    public Engine Engine { get; set; }

    public RenderTarget? WorldMainRenderTarget;
    public World(Engine engine)
    {
        Engine = engine;
        SceneRenderer = new DeferredSceneRenderer(this);
    }
    public Level? Level;


    public Level CurrentLevel
    {
        get
        {
            if (Level == null)
            {
                throw new Exception("");
            }
            return Level;
        }
        private set => Level = value;
    }

    public IRenderer SceneRenderer;
    public void BeginPlay()
    {
        OnBeginPlay();

        if (Engine.GameConfig.DefaultLevel != null && Engine.MainWorld == this)
        {
            OpenLevel(Engine.GameConfig.DefaultLevel);
        }
        else if (Engine.GameConfig.DefaultGameModeClass != null && Engine.MainWorld == this)
        {
            CurrentLevel = new Level(this);
            Activator.CreateInstance(Engine.GameConfig.DefaultGameModeClass, [CurrentLevel, "GameMode"]);
            CurrentLevel.BeginPlay();
        }
        else
        {
            CurrentLevel = new Level(this);
            _ = new GameMode(CurrentLevel, "GameMode");
            CurrentLevel.BeginPlay();
        }
    }

    protected virtual void OnBeginPlay()
    {
    }
    public void Update(double deltaTime)
    {
        OnUpdate(deltaTime);
    }
    protected virtual void OnUpdate(double deltaTime)
    {
        CurrentLevel.Update(deltaTime);
    }




    public void Render(double deltaTime)
    {
        CurrentLevel.Render(deltaTime);
    }

    public void OpenLevel(string path)
    {
        if (Level != null)
        {
            CurrentLevel.Destory();
        }

        using var stream = Engine.FileSystem.GetContentStreamReader(path);
        CurrentLevel = new Level(this);
        CurrentLevel.Deserialize(new BinaryReader(stream.BaseStream), Engine);
        CurrentLevel.BeginPlay();
    }

    public void CreateLevel(string path)
    {
        CurrentLevel = new Level(this);
        CurrentLevel.BeginPlay();
        CurrentLevel.CreateLevel();

        Task.Delay(100).Then(() =>
        {
            using (var stream = new StreamWriter(path))
            {
                CurrentLevel.Serialize(new BinaryWriter(stream.BaseStream), Engine);
            }
            OpenLevel("Content/test.level");

        });

    }

    public void Destory()
    {
        CurrentLevel.Destory();
        OnEndPlay();
    }
    protected virtual void OnEndPlay()
    {

    }

}
