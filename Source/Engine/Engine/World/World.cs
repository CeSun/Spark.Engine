using Spark.Engine.Actors;
using Spark.Engine.Render;
using Spark.Engine.Render.Renderer;
using Spark.Util;

namespace Spark.Engine.World;

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
        CurrentLevel = new Level(this);
        CurrentLevel.BeginPlay();
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
        CurrentLevel.BeginPlay();
    }

    public void CreateLevel(string path)
    {
        CurrentLevel = new Level(this);
        CurrentLevel.BeginPlay();
        CurrentLevel.CreateLevel();
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
