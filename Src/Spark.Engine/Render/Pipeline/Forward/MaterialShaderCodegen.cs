using Spark.Engine.Render.Resources;

namespace Spark.Engine.Render.Pipeline.Forward;

/// <summary>
/// 前向材质 WGSL 代码生成（P1：模板 + 占位替换，纯函数可单测）。
/// 模板以嵌入式资源形式存放于 <c>Render/Pipeline/Forward/Shaders/*.wgsl</c>（首次访问时加载并缓存），
/// 由 <see cref="MaterialShaderKey"/> 折叠静态属性，生成完整 WGSL 模块；
/// 绑定组布局固定（4 组），只有 shader 代码随 key 变（ADR-15）。
/// </summary>
public static class MaterialShaderCodegen
{
    private const int MaxLights = ShaderConstants.MaxLights;

    private static readonly string HeaderTemplate = LoadShader("ForwardHeader.wgsl");
    private static readonly string ShadeLitTemplate = LoadShader("ForwardShadeLit.wgsl");
    private static readonly string FragmentTemplate = LoadShader("ForwardFragment.wgsl");

    /// <summary>按 key 生成完整 WGSL 源码（纯函数）。</summary>
    public static string Generate(MaterialShaderKey key)
    {
        string header = HeaderTemplate.Replace("{{MAX_LIGHTS}}", MaxLights.ToString());

        var sb = new System.Text.StringBuilder(header);

        if (key.ShadingModel != ShadingModel.Unlit)
            sb.Append(ShadeLitTemplate);

        string fragment = FragmentTemplate
            .Replace("{{BASE_COLOR_TEXTURE}}",
                key.TextureFlags.HasFlag(TextureFlags.BaseColor)
                    ? "    base = base * textureSample(base_color_tex, samp, in.uv);"
                    : "")
            .Replace("{{MR_TEXTURE}}",
                key.TextureFlags.HasFlag(TextureFlags.MetallicRoughness)
                    ? "    var mr = textureSample(mr_tex, samp, in.uv);\n    metallic = mr.r;\n    roughness = mr.g;"
                    : "")
            .Replace("{{SHADING}}",
                key.ShadingModel == ShadingModel.Unlit
                    ? ""
                    : "    color = shade_lit(color, metallic, roughness, n, in.world_pos, frame.camera_pos.xyz);")
            .Replace("{{EMISSIVE_TEXTURE}}",
                key.TextureFlags.HasFlag(TextureFlags.Emissive)
                    ? "    color = color + textureSample(emissive_tex, samp, in.uv).rgb * mp.emissive.w;"
                    : "")
            .Replace("{{MASK}}",
                key.BlendMode == BlendMode.Masked
                    ? "    if (textureSample(mask_tex, samp, in.uv).r < 0.5) { discard; }"
                    : "");

        sb.Append(fragment);
        return sb.ToString();
    }

    /// <summary>按文件名后缀从嵌入式资源加载模板（不依赖根命名空间，对重命名稳健）。</summary>
    private static string LoadShader(string fileName)
    {
        var assembly = typeof(MaterialShaderCodegen).Assembly;

        string? resourceName = null;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith("." + fileName, StringComparison.Ordinal))
            {
                resourceName = name;
                break;
            }
        }

        if (resourceName == null)
            throw new InvalidOperationException($"Embedded shader resource '{fileName}' not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
