using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;


    
namespace LiteEngine;
public class Game
{
    static Game _Instance = new Game();
    public static Game Instance { get { return _Instance; } }

    public void Update(float deltaTime)
    {

    }
    public void Render()
    {

    }
    public void Init()
    {

    }

    public T Create<T>() where T : new()
    {
        return new T();
    }

    public void Fini()
    {

    }
}