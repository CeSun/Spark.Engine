using Spark.Engine.Render.Resources;

namespace Spark.Engine.Render.Pipeline.Forward;

/// <summary>
/// 材质 WGSL 代码生成（模板 + 占位替换，纯函数可单测）。
/// 模板以嵌入式资源形式存放于 <c>Render/Pipeline/Forward/Shaders/*.wgsl</c>（首次访问时加载并缓存），
/// 由 <see cref="MaterialShaderKey"/> 折叠材质静态属性、<see cref="ShaderPass"/> 决定着色片段，
/// 生成完整 WGSL 模块；绑定组布局固定（4 组），只有 shader 代码随 (key, pass) 变。
/// </summary>
public static class MaterialShaderCodegen
{
    private const int MaxLights = ShaderConstants.MaxLights;

    private static readonly string HeaderTemplate = LoadShader("ForwardHeader.wgsl");
    private static readonly string ShadeLitTemplate = LoadShader("ForwardShadeLit.wgsl");
    private static readonly string FragmentTemplate = LoadShader("ForwardFragment.wgsl");
    private static readonly string DepthFragmentTemplate = LoadShader("ForwardDepthFragment.wgsl");

    /// <summary>法线贴图片段：屏幕空间导数法计算 TBN（无需切线顶点属性）→ 采样法线纹理 → 覆盖 n。</summary>
    private const string NormalMapCode =
        "    var dp1 = dpdx(in.world_pos);\n" +
        "    var dp2 = dpdy(in.world_pos);\n" +
        "    var duv1 = dpdx(in.uv);\n" +
        "    var duv2 = dpdy(in.uv);\n" +
        "    var nrm = normalize(in.world_normal);\n" +
        "    var dp2perp = cross(dp2, nrm);\n" +
        "    var dp1perp = cross(nrm, dp1);\n" +
        "    var tangent = dp2perp * duv1.x + dp1perp * duv2.x;\n" +
        "    var bitangent = dp2perp * duv1.y + dp1perp * duv2.y;\n" +
        "    var invmax = inverseSqrt(max(dot(tangent, tangent), dot(bitangent, bitangent)));\n" +
        "    var tbn = mat3x3f(tangent * invmax, bitangent * invmax, nrm);\n" +
        "    var n_ts = textureSample(normal_tex, samp, in.uv).rgb * 2.0 - 1.0;\n" +
        "    n_ts = vec3f(n_ts.x * mp.normal_strength.x, n_ts.y * mp.normal_strength.x, n_ts.z);\n" +
        "    n = normalize(tbn * normalize(n_ts));";

    /// <summary>按材质 key + pass 生成完整 WGSL 源码（纯函数）。</summary>
    public static string Generate(MaterialShaderKey key, ShaderPass pass)
    {
        string header = HeaderTemplate.Replace("{{MAX_LIGHTS}}", MaxLights.ToString());
        var sb = new System.Text.StringBuilder(header);

        switch (pass)
        {
            case ShaderPass.Forward:
                if (key.ShadingModel != ShadingModel.Unlit)
                    sb.Append(ShadeLitTemplate);
                sb.Append(BuildForwardFragment(key));
                break;

            case ShaderPass.ShadowDepth:
            case ShaderPass.DepthOnly:
                sb.Append(BuildDepthFragment(key));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(pass), pass, null);
        }

        return sb.ToString();
    }

    private static string BuildForwardFragment(MaterialShaderKey key) => FragmentTemplate
        .Replace("{{BASE_COLOR_TEXTURE}}",
            key.TextureFlags.HasFlag(TextureFlags.BaseColor)
                ? "    base = base * textureSample(base_color_tex, samp, in.uv);"
                : "")
        .Replace("{{MR_TEXTURE}}",
            key.TextureFlags.HasFlag(TextureFlags.MetallicRoughness)
                ? "    var mr = textureSample(mr_tex, samp, in.uv);\n    metallic = mr.r;\n    roughness = mr.g;"
                : "")
        .Replace("{{NORMAL_TEXTURE}}",
            key.TextureFlags.HasFlag(TextureFlags.Normal)
                ? NormalMapCode
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

    private static string BuildDepthFragment(MaterialShaderKey key) => DepthFragmentTemplate
        .Replace("{{MASK}}",
            key.BlendMode == BlendMode.Masked
                ? "    if (textureSample(mask_tex, samp, in.uv).r < 0.5) { discard; }"
                : "");

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
