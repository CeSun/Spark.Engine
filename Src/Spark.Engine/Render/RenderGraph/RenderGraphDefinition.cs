using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 可序列化的渲染图定义（图形化配置的输入侧）：资源声明 + 节点实例。
/// 由 <see cref="RenderGraphAssembler"/> 装配成可执行的 <see cref="RenderGraph"/>；可 JSON 持久化 / 由编辑器产出。
/// 与 <see cref="RenderGraphDescription"/>（编译后 dump 输出）互为输入/输出两侧。
/// </summary>
public sealed class RenderGraphDefinition
{
    /// <summary>资源声明（transient 纹理 / external 目标）。</summary>
    public List<ResourceDeclaration> Resources { get; set; } = new();

    /// <summary>节点实例（pass 类型 + 引脚连线 + 参数覆写）。</summary>
    public List<NodeDeclaration> Nodes { get; set; } = new();
}

/// <summary>一个资源的声明：transient（宽高/格式/用途）或 external（目标 Id）。</summary>
public sealed class ResourceDeclaration
{
    /// <summary>图内逻辑名（节点引脚按此名连线）。</summary>
    public string Name { get; set; } = "";

    /// <summary>true = external 目标（窗口 backbuffer / 持久贴图）；false = transient 纹理。</summary>
    public bool IsExternal { get; set; }

    /// <summary>external 时：RenderTarget.Id。</summary>
    public int ExternalTargetId { get; set; }

    // —— 以下仅 transient 有效 ——

    public uint Width { get; set; }

    public uint Height { get; set; }

    /// <summary>TextureFormat 枚举名（如 "Depth24Plus"）。</summary>
    public string Format { get; set; } = "";

    /// <summary>TextureUsage 标志位数值。</summary>
    public uint Usage { get; set; }
}

/// <summary>一个节点实例：pass 类型 + 引脚连线 + 参数覆写。</summary>
public sealed class NodeDeclaration
{
    /// <summary>图内唯一 Id（调试/编辑器用）。</summary>
    public string Id { get; set; } = "";

    /// <summary>pass 类型名（对应 <see cref="RenderPassType.Name"/>）。</summary>
    public string Type { get; set; } = "";

    /// <summary>引脚名 → 资源逻辑名（可选引脚可缺省）。</summary>
    public Dictionary<string, string> Pins { get; set; } = new();

    /// <summary>参数名 → 值（缺省用类型默认值）。</summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary><see cref="RenderGraphDefinition"/> 的 JSON 序列化。</summary>
public static class RenderGraphDefinitionSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToJson(RenderGraphDefinition definition) => JsonSerializer.Serialize(definition, Options);

    public static RenderGraphDefinition? FromJson(string json)
        => JsonSerializer.Deserialize<RenderGraphDefinition>(json, Options);
}
