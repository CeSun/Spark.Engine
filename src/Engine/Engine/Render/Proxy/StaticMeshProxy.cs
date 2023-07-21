using Spark.Engine.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.Assets.StaticMesh;

namespace Spark.Engine.Render.Proxy;

public class StaticMeshProxy : PrimitiveProxy
{
    public StaticMeshProxy() 
    {
        Sectors = new List<Sector>();
        MaterialProxies = new List<MaterialProxy>();
    }
    public List<Sector> Sectors;
    public List<MaterialProxy> MaterialProxies; 
}
