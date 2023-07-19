using Spark.Engine.Assets;
using Spark.Engine.GameLevel;
using Spark.Engine.Render.Proxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Components;

public class StaticMeshComponent : PrimitiveComponent
{
    private StaticMeshProxy StaticMeshProxy;
    public StaticMeshComponent(Level level) : base(level)
    {
        StaticMeshProxy = (StaticMeshProxy)PrimitiveProxy;
    }

    protected override PrimitiveProxy CreateProxy()
    {
        return new StaticMeshProxy();
        
    }
    private StaticMesh? _StaticMesh;
    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set
        {
            RenderThread.AddCommand(rt =>
            {
                StaticMeshProxy.StaticMesh = value;
            });
            _StaticMesh = value;
        }
    }


}
