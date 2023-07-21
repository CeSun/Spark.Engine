using Spark.Engine.Assets;
using Spark.Engine.GameLevel;
using Spark.Engine.Render.Proxy;
using static Spark.Engine.Assets.StaticMesh;

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
        var proxy = new StaticMeshProxy();
        return proxy;
    }
    private StaticMesh? _StaticMesh;
    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set
        {
            List<MaterialProxy> materials = new List<MaterialProxy>();
            List<Sector> sectors = new List<Sector>();
            if (value != null)
            {
                value.Sectors.ForEach(sector => materials.Add(new MaterialProxy(sector.Material)));
                sectors.AddRange(value.Sectors);
            }
            RenderThread.AddCommand(rt =>
            {
                StaticMeshProxy.Sectors.Clear();
                StaticMeshProxy.MaterialProxies.Clear();
                if (value != null)
                {
                    StaticMeshProxy.Sectors = sectors;
                    StaticMeshProxy.MaterialProxies = materials;
                }
            });
            _StaticMesh = value;
        }
    }

    
}
