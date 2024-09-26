using Spark.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core.Assets;

public class RenderTargetCube : RenderTarget
{
    public unsafe override nint GetCreateProxyFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<GCHandle>)&CreateProxy;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static GCHandle CreateProxy() => GCHandle.Alloc(new RenderTargetCubeProxy(), GCHandleType.Normal);
}


public class RenderTargetCubeProxy : RenderTargetProxy
{
    public override void UpdatePropertiesAndRebuildGPUResource(BaseRenderer renderer, nint propertiesPtr)
    {
        base.UpdatePropertiesAndRebuildGPUResource(renderer, propertiesPtr);

    }
}