using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;
using Spark.Util;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Components;

public class SkyboxComponent : PrimitiveComponent
{
    public SkyboxComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {

    }

    public TextureCube? _skyboxCube;
    public TextureCube? SkyboxCube 
    {
        get => _skyboxCube; 
        set => _skyboxCube = value;
    }

    public override nint GetSubComponentProperties()
    {
        return UnsafeHelper.Malloc(new SkyboxComponentProperties
        {
            SkyboxCube = SkyboxCube == null ? default : SkyboxCube.WeakGCHandle,
        });
    }

}


public class SkyboxComponentProxy : PrimitiveComponentProxy
{
    public TextureCubeProxy? SkyboxCubeProxy { get; set; }

    public unsafe override void UpdateSubComponentProxy(nint pointer, IRenderer renderer)
    {
        ref SkyboxComponentProperties properties = ref Unsafe.AsRef<SkyboxComponentProperties>((void*)pointer);
        SkyboxCubeProxy = renderer.GetProxy<TextureCubeProxy>(properties.SkyboxCube);
    }
}

public struct SkyboxComponentProperties
{
    private IntPtr Destructors { get; set; }

    public GCHandle SkyboxCube {  get; set; }
}