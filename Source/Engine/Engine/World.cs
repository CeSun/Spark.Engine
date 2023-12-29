using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Platform;
using Spark.Engine.Render.Renderer;
using Spark.Util;

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
        OnBeginPlay();
        if (Engine.GameConfig.DefaultLevel != null && FileSystem.Instance.FileExits(Engine.GameConfig.DefaultLevel) == true)
        {
            OpenLevel(Engine.GameConfig.DefaultLevel);
        }
        else
        {
            CurrentLevel = new Level(this); ;
            new GameMode(CurrentLevel);

            using(var sw = FileSystem.Instance.GetStreamWriter("level"))
            {
                CurrentLevel.Serialize(new BinaryWriter(sw.BaseStream), Engine);
            }
        }

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
        using (var stream = FileSystem.Instance.GetStreamReader(path))
        {
            CurrentLevel = new Level(this);
            CurrentLevel.Deserialize(new BinaryReader(stream.BaseStream), Engine);
            CurrentLevel.BeginPlay();
        }
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
