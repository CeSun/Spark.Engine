using Silk.NET.OpenGL;
using Spark.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine;

public class RenderContext : Singleton<RenderContext>
{
    public GL? GL { get; set; }

    public bool IsSupportRender => GL != null;

    public void Render(Action<GL> fun)
    {
        if (GL != null)
        {
            fun(GL);
        }

    }
}
