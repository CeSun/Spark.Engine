﻿using Silk.NET.OpenGLES;
using Spark.Core.Render;
using Spark.Util;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Assets;

public class Material(bool allowMuiltUpLoad = false) : AssetBase(allowMuiltUpLoad)
{
    public BlendMode _blendMode;
    public BlendMode BlendMode 
    {
        get => _blendMode;
        set => ChangeProperty(ref _blendMode, value);
    }

    private string _shaderPath = string.Empty;
    public string ShaderPath
    {
        get => _shaderPath;
        set => ChangeProperty(ref _shaderPath, value);
    }

    Dictionary<string, Texture> _textures { get; set; } = new Dictionary<string, Texture>();

    public IReadOnlyDictionary<string, Texture> Textures => _textures;

    public void AddTexture(string name, Texture texture)
    {
        if (IsUploaded == true && AllowMuiltUpLoad == false)
            throw new Exception();
        _textures.Add(name, texture);
        if (IsUploaded == true)
        {
            foreach (var renderer in Renderers)
            {
                PostProxyToRenderer(renderer);
            }
        }
    }

    public void ClearTextures()
    {
        if (IsUploaded == true && AllowMuiltUpLoad == false)
            throw new Exception();
        _textures.Clear(); 
        if (IsUploaded == true)
        {
            foreach (var renderer in Renderers)
            {
                PostProxyToRenderer(renderer);
            }
        }
    }

    public void SetTexture(string name, Texture texture)
    {
        if (IsUploaded == true && AllowMuiltUpLoad == false)
            throw new Exception();
        _textures[name] = texture;
        if (IsUploaded == true)
        {
            foreach (var renderer in Renderers)
            {
                PostProxyToRenderer(renderer);
            }
        }

    }

    protected override unsafe int assetPropertiesSize => sizeof(MaterialProxyProperties);

    public override void PostProxyToRenderer(BaseRenderer renderer)
    {
        foreach(var (name, texture) in _textures)
        {
            texture.PostProxyToRenderer(renderer);
        }
        base.PostProxyToRenderer(renderer);
    }

    public override nint CreateProperties()
    {
        var ptr = base.CreateProperties();
        ref var properties = ref UnsafeHelper.AsRef<MaterialProxyProperties>(ptr);
        properties.BlendMode = BlendMode;
        Span<GCHandle> names = stackalloc GCHandle[this._textures.Count];
        Span<GCHandle> textures = stackalloc GCHandle[_textures.Count];
        int i = 0;
        foreach (var (name, texture) in this._textures)
        {
            names[i] = GCHandle.Alloc(name, GCHandleType.Normal);
            textures[i] = texture.WeakGCHandle;
        }
        properties.TextureNames = new UnmanagedArray<GCHandle>(names);
        properties.Textures = new UnmanagedArray<GCHandle>(textures);
        properties.ShaderName = GCHandle.Alloc(ShaderPath, GCHandleType.Normal);
        return ptr;
    }
    public unsafe override nint GetCreateProxyFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<GCHandle>)&CreateProxy;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static GCHandle CreateProxy() => GCHandle.Alloc(new MaterialProxy(), GCHandleType.Normal);
    public unsafe override nint GetPropertiesDestoryFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&DestoryProperties;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static void DestoryProperties(IntPtr ptr)
    {
        ref var properties = ref UnsafeHelper.AsRef<MaterialProxyProperties>(ptr);
        for(int i = 0; i < properties.TextureNames.Count; i++)
        {
            properties.TextureNames[i].Free();
        }
        properties.TextureNames.Dispose();
        properties.Textures.Dispose();
        properties.ShaderName.Free();
    }

}

public class MaterialProxy : AssetRenderProxy
{

    public unsafe override void UpdatePropertiesAndRebuildGPUResource(BaseRenderer renderer, IntPtr propertiesPtr)
    {
        base.UpdatePropertiesAndRebuildGPUResource(renderer, propertiesPtr);
        ref var properties = ref UnsafeHelper.AsRef<MaterialProxyProperties>(propertiesPtr);
        BlendMode = properties.BlendMode;
        for (int i = 0; i < properties.Textures.Length; i++)
        {
            if (properties.TextureNames[i].Target is not string name)
                continue;
            var proxy = renderer.GetProxy<TextureProxy>(properties.Textures[i]);
            if (proxy == null)
                continue;
            Textures.Add(name, proxy);
        }
    }

    public override void DestoryGpuResource(BaseRenderer renderer)
    {
        base.DestoryGpuResource(renderer);
    }
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    public Dictionary<string, TextureProxy> Textures = new Dictionary<string, TextureProxy>();
}


public enum BlendMode
{
    Opaque,
    Translucent,
    Masked
}


public struct MaterialProxyProperties
{
    public AssetProperties Base;
    public BlendMode BlendMode;
    public UnmanagedArray<GCHandle> Textures;
    public UnmanagedArray<GCHandle> TextureNames;
    public GCHandle ShaderName;
}
