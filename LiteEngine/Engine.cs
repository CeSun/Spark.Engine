using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LiteEngine.Sdk;


    
namespace LiteEngine;
public class Engine
{
    static Engine _Instance = new Engine();
    public static Engine Instance { get { return _Instance; } }

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
    IGame GameDll;
    IGameUI GameUIDll;
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。


    public void Update(float deltaTime)
    {
        GameDll.OnUpdate(deltaTime);
        GameUIDll.OnUpdate(deltaTime);
    }
    public void Render()
    {
        GameDll.OnRender();
        GameUIDll.OnRender();
    }

    public void Init()
    {
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
        Console.WriteLine(Directory.GetCurrentDirectory());
       var gamedll = Assembly.LoadFrom("./Game.dll");
       var types = gamedll.GetTypes();
        foreach (var type in types)
        {
            if (typeof(IGame).IsAssignableFrom(type))
            {
                GameDll = (IGame)gamedll.CreateInstance(type.FullName);
            }
            if (typeof(IGameUI).IsAssignableFrom(type))
            {
                GameUIDll = (IGameUI)gamedll.CreateInstance(type.FullName);
            }
        }

        if (GameDll == null)
        {
            throw new Exception("Game.dll 中缺少 继承自LiteEngine.Sdk.IGame接口的类！");
        }
        if (GameUIDll == null)
        {
            throw new Exception("Game.dll 中缺少 继承自LiteEngine.Sdk.IGameui接口的类！");
        }
    }


}