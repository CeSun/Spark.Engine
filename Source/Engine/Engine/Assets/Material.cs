using Silk.NET.OpenGLES;
using Spark.Core.Render;
using Spark.Util;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            i++;
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
        var obj = properties.ShaderName.Target;
        var shaderName = obj as string;
        if (shaderName != null)
        {
            ShaderTemplate = renderer.ReadShaderTemplate(shaderName);
        }
    }

    public override void DestoryGpuResource(BaseRenderer renderer)
    {
        base.DestoryGpuResource(renderer);
    }
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    public Dictionary<string, TextureProxy> Textures = new Dictionary<string, TextureProxy>();
    public ShaderTemplate? ShaderTemplate { get; private set; }
}

public class ShaderJson
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("include")]
    public List<string> Include { get; set; } = [];

    [JsonPropertyName("vertex")]
    public string Vertex { get; set; } = string.Empty;

    [JsonPropertyName("fragment")]
    public string Fragment { get; set; } = string.Empty;

}

public static class ShaderTemplateHelper
{
    public static ShaderTemplate? ReadShaderTemplate(this BaseRenderer renderer, string shaderName)
    {
        var shaderTemplate = renderer.GetShaderTemplate(shaderName);
        if (shaderTemplate != null)
            return shaderTemplate;
        ShaderJson? shaderObject = null;
        var engine = renderer.Engine;
        using var stream = engine.FileSystem.GetStream(shaderName);
        shaderObject = JsonSerializer.Deserialize(stream.BaseStream, ShaderJsonContext.Default.ShaderJson);

        if (shaderObject == null)
            return null;
        shaderTemplate = new ShaderTemplate
        {
            Name = shaderObject.Name,
        };

        foreach (var includePath in shaderObject.Include)
        {
            using(var sr = engine.FileSystem.GetStream(includePath))
            {
                shaderTemplate.IncludeSource.Add(sr.ReadToEnd());
            }
        }

        shaderTemplate.VertexShaderSource = engine.FileSystem.GetStream(shaderObject.Vertex).ReadToEnd();
        shaderTemplate.FragmentShaderSource = engine.FileSystem.GetStream(shaderObject.Fragment).ReadToEnd();
        renderer.SetShaderTemplate(shaderName, shaderTemplate);
        return shaderTemplate;
    }
}
[JsonSerializable(typeof(ShaderJson))]
internal partial class ShaderJsonContext : JsonSerializerContext
{
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
