using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Spark.Engine.SceneGen;

/// <summary>
/// 由标记了 <c>[SceneProxy]</c> 的组件生成：
/// 1) SceneProxy 子类 + payload struct（传输半区的机械样板）；
/// 2) 组件的 partial（_proxy + 生命周期 + SyncProxy + 钩子）；
/// 3) SceneSnapshot 的 partial（分类 payload 缓冲字段 + ClearPayloads）。
/// 语义部分（Bounds 规则、渲染消费）仍手写，见组件里的 OnProxyMapped 钩子。
/// </summary>
[Generator]
public sealed class SceneProxyGenerator : IIncrementalGenerator
{
    private const string SceneProxyAttributeName = "Spark.Engine.Render.SceneProxyAttribute";
    private const string ScenePayloadAttributeName = "Spark.Engine.Render.ScenePayloadAttribute";
    private const string SceneResourceInterfaceName = "Spark.Engine.Render.ISceneResource";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var components = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                SceneProxyAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => Extract(ctx))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        context.RegisterSourceOutput(components, static (spc, model) =>
        {
            spc.AddSource($"{model.ProxyName}.g.cs", SourceText.From(EmitProxyAndPayload(model), Encoding.UTF8));
            spc.AddSource($"{model.ComponentName}.g.cs", SourceText.From(EmitComponentPartial(model), Encoding.UTF8));
        });

        var all = components.Collect();
        context.RegisterSourceOutput(all, static (spc, models) =>
        {
            spc.AddSource("SceneSnapshot.g.cs", SourceText.From(EmitSnapshotPartial(models), Encoding.UTF8));
        });
    }

    private static ComponentModel? Extract(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol symbol)
            return null;

        var attr = ctx.Attributes.FirstOrDefault();
        if (attr is null || attr.ConstructorArguments.Length < 1)
            return null;

        string category = GetEnumMemberName(attr.ConstructorArguments[0]);
        string snapshotField = SnapshotFieldFor(category);

        var payloads = new List<PayloadMember>();
        foreach (var member in symbol.GetMembers())
        {
            bool isPayload = member is IFieldSymbol or IPropertySymbol
                && member.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ScenePayloadAttributeName);

            if (!isPayload)
                continue;

            ITypeSymbol? type = member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null,
            };
            if (type is null)
                continue;

            int position = member.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue;
            payloads.Add(new PayloadMember(member.Name, TypeName(type), IsResource(type), position));
        }

        // 按源码声明顺序排序，保证生成代码确定性
        payloads.Sort(static (a, b) => a.Position.CompareTo(b.Position));

        string componentName = symbol.Name;
        string baseName = componentName.EndsWith("Component", StringComparison.Ordinal)
            ? componentName.Substring(0, componentName.Length - "Component".Length)
            : componentName;

        return new ComponentModel(
            componentName,
            symbol.ContainingNamespace.ToDisplayString(),
            category,
            snapshotField,
            baseName + "SceneProxy",
            baseName + "Payload",
            payloads);
    }

    private static string GetEnumMemberName(TypedConstant constant)
    {
        if (constant.Type is not INamedTypeSymbol enumType || constant.Value is null)
            return "None";

        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol field && field.HasConstantValue && Equals(field.ConstantValue, constant.Value))
                return field.Name;
        }

        return "None";
    }

    /// <summary>由类别名推导快照字段名：Mesh 结尾为不规则复数（+es），其余 +s。</summary>
    private static string SnapshotFieldFor(string category) =>
        category.EndsWith("Mesh", StringComparison.Ordinal) ? category + "es" : category + "s";

    private static string TypeName(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Boolean => "bool",
        SpecialType.System_Single => "float",
        SpecialType.System_Double => "double",
        SpecialType.System_Int32 => "int",
        SpecialType.System_UInt32 => "uint",
        SpecialType.System_Int64 => "long",
        SpecialType.System_UInt64 => "ulong",
        SpecialType.System_String => "string",
        _ => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
    };

    /// <summary>判断成员类型是否实现 ISceneResource（按名匹配，生成器不引用引擎类型）。</summary>
    private static bool IsResource(ITypeSymbol type) =>
        type.AllInterfaces.Any(static i => i.ToDisplayString() == SceneResourceInterfaceName);

    private static string EmitProxyAndPayload(ComponentModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace Spark.Engine.Render;");
        sb.AppendLine();

        // payload struct
        sb.AppendLine($"public readonly struct {model.PayloadName}");
        sb.AppendLine("{");
        foreach (var p in model.Payloads)
            sb.AppendLine($"    public readonly {p.FieldType} {p.FieldName};");
        sb.AppendLine();
        string ctorParams = string.Join(", ", model.Payloads.Select(p => $"{p.FieldType} {LowerFirst(p.FieldName)}"));
        sb.AppendLine($"    public {model.PayloadName}({ctorParams})");
        sb.AppendLine("    {");
        foreach (var p in model.Payloads)
            sb.AppendLine($"        {p.FieldName} = {LowerFirst(p.FieldName)};");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        // proxy class
        sb.AppendLine($"public sealed partial class {model.ProxyName} : SceneProxy");
        sb.AppendLine("{");
        foreach (var p in model.Payloads)
            sb.AppendLine($"    public {p.FieldType} {p.FieldName} {{ get; set; }}");
        sb.AppendLine();
        sb.AppendLine("    public override void Capture(SceneSnapshot snapshot) =>");
        sb.AppendLine($"        snapshot.AddObject(ProxyId, SceneCategory.{model.Category}, WorldTransform, Bounds, Visibility,");
        string payloadArgs = string.Join(", ", model.Payloads.Select(p => p.FieldName));
        sb.AppendLine($"            snapshot.{model.SnapshotField}, new {model.PayloadName}({payloadArgs}));");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EmitComponentPartial(ComponentModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Spark.Engine.Render;");
        sb.AppendLine();
        sb.AppendLine($"namespace {model.ComponentNamespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {model.ComponentName}");
        sb.AppendLine("{");
        sb.AppendLine($"    private {model.ProxyName}? _proxy;");
        sb.AppendLine();
        sb.AppendLine("    public override void BeginPlay()");
        sb.AppendLine("    {");
        sb.AppendLine("        base.BeginPlay();");
        sb.AppendLine("        var scene = Owner?.World?.Scene;");
        sb.AppendLine("        if (scene != null)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _proxy = new {model.ProxyName}();");
        sb.AppendLine("            SyncProxy();");
        sb.AppendLine("            scene.Register(_proxy);");
        sb.AppendLine("        }");
        sb.AppendLine("        OnBeginPlay();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void Update(float deltaTime)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.Update(deltaTime);");
        sb.AppendLine("        SyncProxy();");
        sb.AppendLine("        OnUpdate(deltaTime);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void EndPlay()");
        sb.AppendLine("    {");
        sb.AppendLine("        base.EndPlay();");
        sb.AppendLine("        var scene = Owner?.World?.Scene;");
        sb.AppendLine("        if (_proxy != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            scene?.Unregister(_proxy.ProxyId);");
        sb.AppendLine("            _proxy.Dispose();");
        sb.AppendLine("            _proxy = null;");
        sb.AppendLine("        }");
        sb.AppendLine("        OnEndPlay();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void SyncProxy()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_proxy == null)");
        sb.AppendLine("            return;");
        sb.AppendLine();
        sb.AppendLine("        _proxy.WorldTransform = WorldTransform;");
        foreach (var p in model.Payloads)
        {
            sb.AppendLine(p.IsResource
                ? $"        _proxy.{p.FieldName} = {p.Name}?.ResourceId ?? 0;"
                : $"        _proxy.{p.FieldName} = {p.Name};");
            if (p.IsResource)
                sb.AppendLine($"        Owner?.World?.Scene?.MeshLibrary?.EnsureUploaded({p.Name});");
        }
        sb.AppendLine("        OnProxyMapped(_proxy);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    partial void OnBeginPlay();");
        sb.AppendLine("    partial void OnUpdate(float deltaTime);");
        sb.AppendLine("    partial void OnEndPlay();");
        sb.AppendLine($"    partial void OnProxyMapped({model.ProxyName} proxy);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EmitSnapshotPartial(ImmutableArray<ComponentModel> models)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace Spark.Engine.Render;");
        sb.AppendLine();
        // 按字段名排序，保证生成输出确定性（Collect 本身无序）
        var sorted = models.OrderBy(static m => m.SnapshotField, StringComparer.Ordinal).ToArray();

        sb.AppendLine("public sealed partial class SceneSnapshot");
        sb.AppendLine("{");
        foreach (var model in sorted)
            sb.AppendLine($"    public readonly FrameBuffer<{model.PayloadName}> {model.SnapshotField} = new();");
        sb.AppendLine();
        sb.AppendLine("    partial void ClearPayloads()");
        sb.AppendLine("    {");
        foreach (var model in sorted)
            sb.AppendLine($"        {model.SnapshotField}.Clear();");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string LowerFirst(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);

    private sealed class ComponentModel
    {
        public readonly string ComponentName;
        public readonly string ComponentNamespace;
        public readonly string Category;
        public readonly string SnapshotField;
        public readonly string ProxyName;
        public readonly string PayloadName;
        public readonly List<PayloadMember> Payloads;

        public ComponentModel(
            string componentName,
            string componentNamespace,
            string category,
            string snapshotField,
            string proxyName,
            string payloadName,
            List<PayloadMember> payloads)
        {
            ComponentName = componentName;
            ComponentNamespace = componentNamespace;
            Category = category;
            SnapshotField = snapshotField;
            ProxyName = proxyName;
            PayloadName = payloadName;
            Payloads = payloads;
        }
    }

    private readonly struct PayloadMember
    {
        public readonly string Name;      // 组件成员名（如 Mesh）
        public readonly string Type;      // 组件成员类型名（资源成员不使用）
        public readonly bool IsResource;  // 类型实现 ISceneResource
        public readonly int Position;

        /// <summary>payload/proxy 字段名：资源成员降级为 {Name}Id。</summary>
        public string FieldName => IsResource ? Name + "Id" : Name;

        /// <summary>payload/proxy 字段类型：资源成员降级为 int。</summary>
        public string FieldType => IsResource ? "int" : Type;

        public PayloadMember(string name, string type, bool isResource, int position)
        {
            Name = name;
            Type = type;
            IsResource = isResource;
            Position = position;
        }
    }
}
