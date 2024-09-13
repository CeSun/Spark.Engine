namespace Spark.Engine.Assets;

public class Material : AssetBase
{
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

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

public class MaterialProxy : RenderProxy
{
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

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


public enum BlendMode
{
    Opaque,
    Translucent,
    Masked
}
