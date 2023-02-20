using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Spark.Engine.Core;
using System.Numerics;

namespace Spark.Engine;

public class Engine : Util.Singleton<Engine>
{
    public List<Game> Games;

    public Engine()
    {
        Games = new List<Game> { new Game() };
    }

    public void Init()
    {
        
    }

    public void Tick(double DeltaTime)
    {
        Games.ForEach(game => game.Tick(DeltaTime));
    }


    public void Render(double DeltaTime) 
    {
        Games.ForEach(game => game.Render(DeltaTime));
    }

    public void Fini()
    {

    }

    
    public void ConfigPlatform(GL gl)
    {
        RenderContext.Instance.GL = gl;
    }

    public void ReceiveCommondLines(string[] CommondLines)
    {

    }

    public void Resize(Vector2D<int> WindowSize)
    {

    }


}
