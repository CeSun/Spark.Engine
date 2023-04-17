using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Render.Renderer;

public interface IRenderer
{
    void Render(double DeltaTime);
}
