using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LiteEngine.Sdk;
using Silk.NET.OpenGL;

namespace LiteEngine;
public class Engine
{
    static Engine _Instance = new Engine();
    public static Engine Instance { get { return _Instance; } }

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
    IGame GameDll;
    IGameUI GameUIDll;
    public GL Gl { get; private set; }
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。

    public void Update(float deltaTime)
    {
        GameDll.OnUpdate(deltaTime);
        GameUIDll.OnUpdate(deltaTime);
    }
    public void Render()
    {
        Gl.Clear(ClearBufferMask.ColorBufferBit);
        GameDll.OnRender();
        GameUIDll.OnRender();
    }

    public void Init(Silk.NET.OpenGL.GL gl)
    {
        Gl = gl;
        Gl.ClearColor(0.2f, 0.3f, 0.2f, 1.0f);
        LoadGameDll();
        GameDll.OnInit();
        GameUIDll.OnInit();
    }

    public void Fini()
    {
        GameDll.OnFini();
        GameUIDll.OnFini();
    }


    /// <summary>
    /// 加载GameDll
    /// </summary>
    /// <exception cref="Exception"></exception>
    public void LoadGameDll()
    {
        GameDll = new Game.FPSGame();
        GameUIDll = new Game.GameUI();
    }


}