using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 把 <see cref="RenderGraphDescription"/> 渲染成可视化文本：
/// Mermaid（可粘贴进 Markdown / mermaid.live）、DOT（Graphviz）、JSON（序列化/持久化）。
/// </summary>
public static class RenderGraphVisualizer
{
    /// <summary>输出 Mermaid flowchart（pass 与资源为节点，读写为带标签的边）。</summary>
    public static string ToMermaid(RenderGraphDescription graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");

        foreach (var resource in graph.Resources)
            sb.AppendLine($"  {ResNode(resource.Id)}[\"{EscapeMermaid(resource.Label)}\"]");

        for (int i = 0; i < graph.Passes.Count; i++)
        {
            var pass = graph.Passes[i];
            var node = PassNode(i);
            var label = EscapeMermaid(pass.Name) + (pass.IsCulled ? " (culled)" : "");
            sb.AppendLine($"  {node}[\"{label}\"]");

            foreach (var read in pass.Reads)
                sb.AppendLine($"  {ResNode(read.ResourceId)} -- \"read {read.Access}\" --> {node}");
            foreach (var write in pass.Writes)
                sb.AppendLine($"  {node} -- \"write {write.Access}\" --> {ResNode(write.ResourceId)}");
        }

        return sb.ToString();
    }

    /// <summary>输出 DOT（Graphviz digraph，资源椭圆 / pass 方框）。</summary>
    public static string ToDot(RenderGraphDescription graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("digraph RenderGraph {");
        sb.AppendLine("  rankdir=LR;");
        sb.AppendLine("  node [fontname=\"sans-serif\"];");

        foreach (var resource in graph.Resources)
            sb.AppendLine($"  {ResNode(resource.Id)} [shape=ellipse, label=\"{EscapeDot(resource.Label)}\"];");

        for (int i = 0; i < graph.Passes.Count; i++)
        {
            var pass = graph.Passes[i];
            var node = PassNode(i);
            var label = EscapeDot(pass.Name) + (pass.IsCulled ? " (culled)" : "");
            sb.AppendLine($"  {node} [shape=box, label=\"{label}\"];");

            foreach (var read in pass.Reads)
                sb.AppendLine($"  {ResNode(read.ResourceId)} -> {node} [label=\"{read.Access}\"];");
            foreach (var write in pass.Writes)
                sb.AppendLine($"  {node} -> {ResNode(write.ResourceId)} [label=\"{write.Access}\"];");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>序列化为 JSON（枚举转字符串、缩进输出），供持久化/加载器后续使用。</summary>
    public static string ToJson(RenderGraphDescription graph)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };
        return JsonSerializer.Serialize(graph, options);
    }

    private static string ResNode(int id) => $"res_{id}";
    private static string PassNode(int index) => $"pass_{index}";

    private static string EscapeMermaid(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "&quot;").Replace("\n", " ");

    private static string EscapeDot(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
}
