using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Spark.Engine;

public class Engine : Util.Singleton<Engine>
{

    public void Init()
    {
        
    }

    public void Update(double DeltaTime)
    {
        RenderContext.Instance.Render(gl =>
        {
            gl.Clear(ClearBufferMask.ColorBufferBit);
        });
    }


    public void FixedUpdate(double DeltaTime) 
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
