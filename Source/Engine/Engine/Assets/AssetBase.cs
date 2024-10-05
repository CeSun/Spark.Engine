using Silk.NET.OpenGLES;
using Spark.Core.Render;
using Spark.Util;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Assets;

public abstract class AssetBase
{
    public GCHandle WeakGCHandle { get; private set; }
    public bool IsUploaded { get; private set; } = false;
    public bool AllowMuiltUpLoad { get; private set; }
    protected virtual unsafe int assetPropertiesSize => sizeof(AssetProperties);
    public HashSet<RenderDevice> Renderers { get; private set; } = [];
    public AssetBase(bool allowMuiltUpLoad = false)
    {
        WeakGCHandle = GCHandle.Alloc(this, GCHandleType.Weak);
        AllowMuiltUpLoad = allowMuiltUpLoad;
    }


    public virtual void PostProxyToRenderer(RenderDevice renderer)
    {
        if (IsUploaded == true && AllowMuiltUpLoad == false)
            return;
        var ptr = CreateProperties();
        renderer.UpdateAssetProxy(ptr);
        if (Renderers.Contains(renderer) == false)
            Renderers.Add(renderer);
        IsUploaded = true;
        if (AllowMuiltUpLoad == false)
        {
            ReleaseAssetMemory();
        }
    }



    public void ChangeProperty<T>(ref T property, in T newValue)
    {
        if (IsUploaded == true && AllowMuiltUpLoad == false)
            throw new Exception();
        property = newValue;
        if (IsUploaded == true)
        {
            foreach (var renderer in Renderers)
            {
                PostProxyToRenderer(renderer);
            }
        }
    }


    protected virtual void ReleaseAssetMemory()
    {

    }
    public virtual IntPtr CreateProperties()
    {
        var ptr = Marshal.AllocHGlobal(assetPropertiesSize);
        ref var properties = ref UnsafeHelper.AsRef<AssetProperties>(ptr);
        properties.AssetWeakGCHandle = WeakGCHandle;
        properties.CreateProxyPointer = GetCreateProxyFunctionPointer();
        properties.DestoryPointer = GetPropertiesDestoryFunctionPointer();
        return ptr;
    }

    public unsafe virtual IntPtr GetCreateProxyFunctionPointer()
    {
        delegate* unmanaged[Cdecl]<GCHandle> p = &CreateProxy;
        return (nint)p;
    }

    public unsafe virtual IntPtr GetPropertiesDestoryFunctionPointer()
    {
        return IntPtr.Zero;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static GCHandle CreateProxy()
    {
        return GCHandle.Alloc(new AssetProperties(), GCHandleType.Normal);
    }

}


public class AssetRenderProxy
{
    public AssetRenderProxy()
    {
    }

    public virtual void UpdatePropertiesAndRebuildGPUResource(RenderDevice renderer, IntPtr propertiesPtr)
    {
    }


    public virtual void DestoryGpuResource(RenderDevice renderer) 
    { 
    }
}

public struct AssetProperties
{
    public GCHandle AssetWeakGCHandle;

    public IntPtr CreateProxyPointer;

    public IntPtr DestoryPointer;
}
