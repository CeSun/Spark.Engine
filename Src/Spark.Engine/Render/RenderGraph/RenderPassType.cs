namespace Spark.Engine.Render.RenderGraph;

/// <summary>pass 类型的输入/输出引脚描述。</summary>
public readonly struct RenderPassPin
{
    public RenderPassPin(string name, ResourceAccess access, bool optional = false)
    {
        Name = name;
        Access = access;
        Optional = optional;
    }

    /// <summary>引脚名（节点定义里按此名连线）。</summary>
    public string Name { get; }

    /// <summary>资源访问类型（Sample / RenderTarget）。</summary>
    public ResourceAccess Access { get; }

    /// <summary>是否允许不连接（如「无阴影贴图」时 Forward 的 shadowDepth 输入）。</summary>
    public bool Optional { get; }
}

/// <summary>pass 类型的可调参数描述（编辑器据此生成控件；值为字符串，装配时由 bind 解析）。</summary>
public readonly struct RenderPassParameter
{
    public RenderPassParameter(string name, string defaultValue)
    {
        Name = name;
        DefaultValue = defaultValue;
    }

    public string Name { get; }

    public string DefaultValue { get; }
}

/// <summary>
/// 可复用的 pass 类型：声明输入/输出引脚与参数，并提供 bind 委托把「节点实例 + 已解析资源」装进 <see cref="RenderGraph"/>。
/// 引脚/参数是元数据（编辑器面板 / 校验用）；GPU 执行代码在 bind 委托里（由管线注册时闭包捕获）。
/// </summary>
public sealed class RenderPassType
{
    private readonly Action<RenderGraph, RenderPassBindContext> _bind;

    public RenderPassType(
        string name,
        IReadOnlyList<RenderPassPin> inputs,
        IReadOnlyList<RenderPassPin> outputs,
        IReadOnlyList<RenderPassParameter> parameters,
        Action<RenderGraph, RenderPassBindContext> bind)
    {
        Name = name;
        Inputs = inputs;
        Outputs = outputs;
        Parameters = parameters;
        _bind = bind;
    }

    public string Name { get; }

    public IReadOnlyList<RenderPassPin> Inputs { get; }

    public IReadOnlyList<RenderPassPin> Outputs { get; }

    public IReadOnlyList<RenderPassParameter> Parameters { get; }

    /// <summary>把节点实例绑定到图（可添加 0..N 个 pass；如 Forward 按相机数展开）。</summary>
    public void Bind(RenderGraph graph, RenderPassBindContext context) => _bind(graph, context);
}

/// <summary>bind 时的上下文：已解析的引脚资源 + 参数 + 帧上下文。</summary>
public readonly struct RenderPassBindContext
{
    public RenderPassBindContext(
        IReadOnlyDictionary<string, RenderGraphResource> inputs,
        IReadOnlyDictionary<string, RenderGraphResource> outputs,
        IReadOnlyDictionary<string, string> parameters,
        RenderGraphFrameContext frame)
    {
        Inputs = inputs;
        Outputs = outputs;
        Parameters = parameters;
        Frame = frame;
    }

    /// <summary>按引脚名解析的输入资源句柄（可选引脚未连接则不在其中）。</summary>
    public IReadOnlyDictionary<string, RenderGraphResource> Inputs { get; }

    /// <summary>按引脚名解析的输出资源句柄。</summary>
    public IReadOnlyDictionary<string, RenderGraphResource> Outputs { get; }

    /// <summary>节点参数（名 → 字符串值，已合并类型默认值）。</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>帧级动态输入（快照 / 目标注册表等）。</summary>
    public RenderGraphFrameContext Frame { get; }
}

/// <summary>pass 类型注册表（名称 → 类型），编辑器节点面板的发现来源。</summary>
public sealed class RenderPassTypeRegistry
{
    private readonly Dictionary<string, RenderPassType> _types = new();

    public void Register(RenderPassType type) => _types[type.Name] = type;

    public bool TryGet(string name, out RenderPassType? type) => _types.TryGetValue(name, out type);

    public IEnumerable<RenderPassType> Types => _types.Values;
}
