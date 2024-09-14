
using Spark.Engine.Render;

namespace Spark.Engine.Assets;

public class Material : AssetBase
{
    public BlendMode _blendMode;
    public BlendMode BlendMode 
    {
        get => _blendMode;
        set
        {
            _blendMode = value;
            RunOnRenderer(renderer =>
            {
                var proxy = renderer.GetProxy<MaterialProxy>(this);
                if (proxy != null)
                {
                    proxy.BlendMode = _blendMode;
                    RequestRendererRebuildGpuResource();
                }
            });
        }
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

    public override void PostProxyToRenderer(IRenderer renderer)
    {
        foreach(var texture in Textures)
        {
            if (texture == null)
                continue;
            texture.PostProxyToRenderer(renderer);
        }
        base.PostProxyToRenderer(renderer);
    }
    public override Func<IRenderer, RenderProxy>? GetGenerateProxyDelegate()
    {
        var blendMode = _blendMode;
        var textures = Textures.ToList();

        string preShaderSnippet = PreShaderSnippet;
        string getBaseColorShaderSnippet = GetBaseColorShaderSnippet;
        string getNormalShaderSnippet = GetNormalShaderSnippet;
        string getMetallicShaderSnippet = GetMetallicShaderSnippet;
        string getRoughnessShaderSnippet = GetRoughnessShaderSnippet;
        string getAmbientOcclusionShaderSnippet = GetAmbientOcclusionShaderSnippet;
        string getOpaqueMaskShaderSnippet = GetOpaqueMaskShaderSnippet;

        return renderer => new MaterialProxy
        {
            BlendMode = _blendMode,
            PreShaderSnippet = preShaderSnippet,
            GetBaseColorShaderSnippet = getBaseColorShaderSnippet,
            GetNormalShaderSnippet = getNormalShaderSnippet,
            GetMetallicShaderSnippet = getMetallicShaderSnippet,
            GetRoughnessShaderSnippet = getRoughnessShaderSnippet,
            GetAmbientOcclusionShaderSnippet = getAmbientOcclusionShaderSnippet,
            GetOpaqueMaskShaderSnippet = getOpaqueMaskShaderSnippet,
            Textures = [
                textures[0]==null?null:renderer.GetProxy<TextureProxy>(textures[0]!),
                textures[1]==null?null:renderer.GetProxy<TextureProxy>(textures[1]!),
                textures[2]==null?null:renderer.GetProxy<TextureProxy>(textures[2]!),
                textures[3]==null?null:renderer.GetProxy<TextureProxy>(textures[3]!),
                textures[4]==null?null:renderer.GetProxy<TextureProxy>(textures[4]!),
                textures[5]==null?null:renderer.GetProxy<TextureProxy>(textures[5]!),
                textures[6]==null?null:renderer.GetProxy<TextureProxy>(textures[6]!),
                textures[7]==null?null:renderer.GetProxy<TextureProxy>(textures[7]!),
                textures[8]==null?null:renderer.GetProxy<TextureProxy>(textures[8]!),
                textures[9]==null?null:renderer.GetProxy<TextureProxy>(textures[9]!),
            ]
        };
    }
}

public class MaterialProxy : RenderProxy
{
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    public TextureProxy?[] Textures = new TextureProxy?[10];
    public TextureProxy? BaseColor { get => Textures[0]; set => Textures[0] = value; }
    public TextureProxy? Normal { get => Textures[1]; set => Textures[1] = value; }
    public TextureProxy? MetallicRoughness { get => Textures[2]; set => Textures[2] = value; }
    public TextureProxy? AmbientOcclusion { get => Textures[3]; set => Textures[3] = value; }
    public TextureProxy? Parallax { get => Textures[4]; set => Textures[4] = value; }
    public string PreShaderSnippet { get; set; } = "";
    public string GetBaseColorShaderSnippet { get; set; } = "";
    public string GetNormalShaderSnippet { get; set; } = "";
    public string GetMetallicShaderSnippet { get; set; } = "";
    public string GetRoughnessShaderSnippet { get; set; } = "";
    public string GetAmbientOcclusionShaderSnippet { get; set; } = "";
    public string GetOpaqueMaskShaderSnippet { get; set; } = "";
}


public enum BlendMode
{
    Opaque,
    Translucent,
    Masked
}
