using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Spark.Engine;
using Spark.Engine.Components;
using Spark.Engine.Platform;
using Spark.Engine.Render.Renderer;

namespace Spark.Engine;

public class World
{
    public Engine Engine { get; set; }
    public World(Engine engine)
    {
        Engine = engine;
        SceneRenderer = new DeferredSceneRenderer(this);
    }
    public Level? _Level;


    public Level CurrentLevel
    {
        get
        {
            if (_Level == null)
            {
                throw new Exception("");
            }
            return _Level;
        }
        private set
        {
            _Level = value;
        }
    }

    public IRenderer SceneRenderer;
    public void BeginPlay()
    {
        OpenLevel("Default");
        OnBeginPlay();
    }

    protected virtual void OnBeginPlay()
    {
    }
    public void Update(double DeltaTime)
    {
        OnUpdate(DeltaTime);
    }
    protected virtual void OnUpdate(double DeltaTime)
    {
        CurrentLevel.Update(DeltaTime);
    }




    public void Render(double DeltaTime)
    {
        CurrentLevel.Render(DeltaTime);
    }

    public void OpenLevel(string path)
    {
        if (_Level != null)
        {
            CurrentLevel.Destory();
        }
        using (var stream = FileSystem.Instance.GetStream(path))
        {
            CurrentLevel = new Level(this);
            CurrentLevel.Deserialize(new BinaryReader(stream), Engine);
            CurrentLevel.BeginPlay();
        }
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
