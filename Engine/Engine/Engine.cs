using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Spark.Engine.Core;
using System.Numerics;

namespace Spark.Engine;

public class Engine : Util.Singleton<Engine>
{
    public Level? CurrentLevel { get; set; }
    public void Init()
    {
        
    }

    public void Tick(double DeltaTime)
    {
        RenderContext.Instance.Render(gl =>
        {
            gl.Clear(ClearBufferMask.ColorBufferBit);
        });
    }


    public void Render(double DeltaTime) 
    { 

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
