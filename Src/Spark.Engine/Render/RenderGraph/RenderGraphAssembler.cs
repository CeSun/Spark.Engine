using Silk.NET.WebGPU;

namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 运行时装配器：把可序列化的 <see cref="RenderGraphDefinition"/> + <see cref="RenderPassTypeRegistry"/> 装配成
/// 可执行的 <see cref="RenderGraph"/>（注册/导入资源 → 按节点类型绑定 pass）。装配后由调用方 Compile + Execute。
/// </summary>
public static class RenderGraphAssembler
{
    public static RenderGraph Assemble(
        RenderGraphDefinition definition,
        RenderPassTypeRegistry registry,
        RenderGraphFrameContext frame)
    {
        var graph = new RenderGraph(frame.WebGpu, frame.Logger);
        var resources = new Dictionary<string, RenderGraphResource>();

        // 1. 注册/导入资源
        foreach (var resource in definition.Resources)
        {
            if (resource.IsExternal)
            {
                if (!frame.Targets.TryGet(resource.ExternalTargetId, out var target) || target == null)
                    throw new InvalidOperationException(
                        $"External target {resource.ExternalTargetId} not found for resource '{resource.Name}'");
                resources[resource.Name] = graph.ImportTexture(target);
            }
            else
            {
                var desc = new TextureResourceDesc(
                    resource.Width,
                    resource.Height,
                    Enum.Parse<TextureFormat>(resource.Format),
                    (TextureUsage)resource.Usage);
                resources[resource.Name] = graph.RegisterTexture(desc);
            }
        }

        // 2. 实例化节点
        foreach (var node in definition.Nodes)
        {
            if (!registry.TryGet(node.Type, out var type) || type == null)
                throw new InvalidOperationException($"Unknown pass type '{node.Type}' for node '{node.Id}'");

            var inputs = new Dictionary<string, RenderGraphResource>();
            var outputs = new Dictionary<string, RenderGraphResource>();
            ResolvePins(type.Inputs, node, resources, inputs);
            ResolvePins(type.Outputs, node, resources, outputs);

            var parameters = MergeParameters(type, node);
            var bindContext = new RenderPassBindContext(inputs, outputs, parameters, frame);
            type.Bind(graph, bindContext);
        }

        return graph;
    }

    private static void ResolvePins(
        IReadOnlyList<RenderPassPin> pins,
        NodeDeclaration node,
        Dictionary<string, RenderGraphResource> resources,
        Dictionary<string, RenderGraphResource> resolved)
    {
        foreach (var pin in pins)
        {
            if (node.Pins.TryGetValue(pin.Name, out var resourceName))
            {
                if (!resources.TryGetValue(resourceName, out var handle))
                    throw new InvalidOperationException(
                        $"Node '{node.Id}' pin '{pin.Name}' references unknown resource '{resourceName}'");
                resolved[pin.Name] = handle;
            }
            else if (!pin.Optional)
            {
                throw new InvalidOperationException($"Node '{node.Id}' is missing required pin '{pin.Name}'");
            }
        }
    }

    private static Dictionary<string, string> MergeParameters(RenderPassType type, NodeDeclaration node)
    {
        var merged = new Dictionary<string, string>();
        foreach (var parameter in type.Parameters)
            merged[parameter.Name] = parameter.DefaultValue;
        foreach (var (name, value) in node.Parameters)
            merged[name] = value;
        return merged;
    }
}
