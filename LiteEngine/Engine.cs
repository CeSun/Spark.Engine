using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LiteEngine.Core;
using LiteEngine.Core.SubSystem;
using LiteEngine.Sdk;
using Silk.NET.OpenGL;

namespace LiteEngine;
public partial class Engine
{
  

    private Engine()
    {

    }

 


    public void Update(float deltaTime)
    {
        Gl.Clear(ClearBufferMask.ColorBufferBit);
        GameUIDll.OnUpdate(deltaTime);
        World?.Update(deltaTime);
    }
    public void Render()
    {
        GameUIDll.OnRender();
        World?.Render();
    }

    public void Init(GL gl, IPlatFile platFile)
    {
        Gl = gl;
        FileSystem = new FileSystem(platFile);
        LoadGameDll();
        Gl.ClearColor(0.2f, 0.3f, 0.2f, 1.0f);
        GameUIDll.OnInit();
        World = new World();
        World.Init();
    }

    public void Fini()
    {
        GameUIDll.OnFini();
        World.Fini();
    }


    public void WindowResize(Size size)
    {
        Gl.Viewport(size);
    }
}


public partial class Engine
{
    static Engine _Instance = new Engine();
    public static Engine Instance { get => _Instance; }


    public World World
    {
        get
        {
            if (_World == null)
                throw new("此时世界还不存在，不应该获取。");
            return _World;
        }
        private set => _World = value;
    }
    private World? _World;

    private FileSystem? _FileSystem;
    public FileSystem FileSystem
    {
        get
        {
            if (_FileSystem == null)
                throw new("Error FileSystem is null");
            return _FileSystem;
        }

        private set => _FileSystem = value;
    }


    private GL? _Gl;
    public GL Gl
    {
        get
        {
            if (_Gl == null)
                throw new("Error GL is null");
            return _Gl;
        }
        private set => _Gl = value;
    }


    public IGame? _GameDll;
    public IGame GameDll
    {
        get
        {
            if (_GameDll == null)
                throw new("IGame的实例为空");
            return _GameDll;
        }
        private set => _GameDll = value;
    }

    public IGameUI? _GameUIDll;
    public IGameUI GameUIDll
    {
        get
        {
            if (_GameUIDll == null)
                throw new("IGameUI的实例为空");
            return _GameUIDll;
        }
        private set => _GameUIDll = value;
    }


    /// <summary>
    /// 加载GameDll
    /// </summary>
    /// <exception cref="Exception"></exception>
    public void LoadGameDll()
    {
#if DEBUG
        var asm = Assembly.Load(FileSystem.LoadFile("Game.dll"), FileSystem.LoadFile("Game.pdb"));
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

        if (GameDll == null)
            throw new("IGame的实例为空");
        if (GameUIDll == null)
            throw new("IGameUI的实例为空");
    }

}