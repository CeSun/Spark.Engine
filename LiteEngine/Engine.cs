using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LiteEngine.Core;
using LiteEngine.Core.SubSystem;
using LiteEngine.Sdk;
using Silk.NET.OpenGL;

namespace LiteEngine;
public class Engine
{
    static Engine _Instance = new Engine();
    public static Engine Instance { get => _Instance; }

    private Engine ()
    {
    }

    private World? _World;
    public World World { 
        get
        {
            if (_World == null)
                throw new("此时世界还不存在，不应该获取。");
            return _World;
        }
        private set => _World = value;
    }

    public FileSystem FileSystem 
    { 
        get 
        { 
            if (_FileSystem == null) 
                throw new ("Error FileSystem is null");
            return _FileSystem;
        } 

        private set => _FileSystem = value; 
    }

    private FileSystem? _FileSystem;

    public GL Gl 
    { 
        get {
            if (_Gl == null)
                throw new("Error GL is null");
            return _Gl;
        }
        private set => _Gl = value; }

    private GL? _Gl;


    IGame GameDll;
    IGameUI GameUIDll;


    public void Update(float deltaTime)
    {
        GameDll.OnUpdate(deltaTime);
        GameUIDll.OnUpdate(deltaTime);
        World?.Update(deltaTime);
    }
    public void Render()
    {
        GameDll.OnRender();
        GameUIDll.OnRender();
        World?.Render();
    }

    public void Init(GL gl, IPlatFile platFile)
    {
        Gl = gl;
        FileSystem = new FileSystem(platFile);
        Gl.ClearColor(0.2f, 0.3f, 0.2f, 1.0f);
        LoadGameDll();
        GameDll.OnInit();
        GameUIDll.OnInit();
        World = new World();
        World.Init();
    }

    public void Fini()
    {
        GameDll.OnFini();
        GameUIDll.OnFini();
        World.Fini();
    }


    /// <summary>
    /// 加载GameDll
    /// </summary>
    /// <exception cref="Exception"></exception>
    public void LoadGameDll()
    {
#if DEBUG
        var asm = Assembly.Load(FileSystem.LoadFile("Game.dll"), FileSystem.LoadFile("Game.pdb")) ;
#else
        var asm = Assembly.Load(FileSystem.LoadFile("Game.dll")) ;
#endif

        foreach (var type in asm.GetTypes())
        {
            if (typeof(IGame).IsAssignableFrom(type))
            {
                var dll = (IGame?)Activator.CreateInstance(type);
                if (dll == null)
                    throw new("Game.dll中未实现IGame接口");
                GameDll = dll;
            }
            else if (typeof(IGameUI).IsAssignableFrom(type))
            {
                var dll = (IGameUI?)Activator.CreateInstance(type);
                if (dll == null)
                    throw new("Game.dll中未实现IGameUI接口");
                GameUIDll = dll;
            }
        }
    }


}