using Silk.NET.OpenGLES;
using Spark.Core.Render;
using Spark.Util;
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

    public Texture?[] Textures = new Texture?[10];
    public Texture? BaseColor { get => Textures[0]; set => Textures[0] = value; }
    public Texture? Normal { get => Textures[1]; set => Textures[1] = value; }
    public Texture? MetallicRoughness { get => Textures[2]; set => Textures[2] = value; }
    public Texture? AmbientOcclusion { get => Textures[3]; set => Textures[3] = value; }
    public Texture? Parallax { get => Textures[4]; set => Textures[4] = value; }

    public string PreShaderSnippet { get; set; } = "";
    public string GetBaseColorShaderSnippet { get; set; } = "";
    public string GetNormalShaderSnippet { get; set; } = "";
    public string GetMetallicShaderSnippet { get; set; } = "";
    public string GetRoughnessShaderSnippet { get; set; } = "";
    public string GetAmbientOcclusionShaderSnippet { get; set; } = "";
    public string GetOpaqueMaskShaderSnippet { get; set; } = "";

}

public class MaterialProxy : AssetRenderProxy
{

    public unsafe override void UpdatePropertiesAndRebuildGPUResource(BaseRenderer renderer, IntPtr propertiesPtr)
    {
        base.UpdatePropertiesAndRebuildGPUResource(renderer, propertiesPtr);
        ref var properties = ref UnsafeHelper.AsRef<MaterialProxyProperties>(propertiesPtr);
        BlendMode = properties.BlendMode;
        for (int i = 0; i < properties.Textures.Count; i++)
        {
            if (properties.Textures[i] == default)
                Textures[i] = default;
            Textures[i] = renderer.GetProxy<TextureProxy>(properties.Textures[i]);

        }
    }

    public override void DestoryGpuResource(BaseRenderer renderer)
    {
        base.DestoryGpuResource(renderer);
    }
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    public TextureProxy?[] Textures = new TextureProxy?[10];
}


public enum BlendMode
{
    Opaque,
    Translucent,
    Masked
}


public struct MaterialProxyProperties
{
    public BlendMode BlendMode;

    public UnmanagedArray<GCHandle> Textures;

    public UnmanagedArray<char> PreShaderSnippet;
    public UnmanagedArray<char> GetBaseColorShaderSnippet;
    public UnmanagedArray<char> GetNormalShaderSnippet;
    public UnmanagedArray<char> GetMetallicShaderSnippet;
    public UnmanagedArray<char> GetRoughnessShaderSnippet;
    public UnmanagedArray<char> GetAmbientOcclusionShaderSnippet;
    public UnmanagedArray<char> GetOpaqueMaskShaderSnippet;

}
