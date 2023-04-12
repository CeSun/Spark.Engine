using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core.Render;

public interface Renderer
{
    void Render(double DeltaTime);
}
