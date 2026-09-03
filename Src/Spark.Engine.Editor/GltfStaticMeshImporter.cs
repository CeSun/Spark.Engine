using System.Numerics;
using SharpGLTF.Schema2;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>glTF 节点的编辑器中间表示；首版只填充 StaticMesh。</summary>
public sealed class GltfNodeAsset
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? ParentIndex { get; internal set; }
    public Matrix4x4 LocalTransform { get; init; } = Matrix4x4.Identity;
    public int? MeshIndex { get; init; }
    public StaticMesh? Mesh { get; init; }
}

public sealed class GltfImportResult
{
    public string SourcePath { get; init; } = string.Empty;
    public IReadOnlyList<StaticMesh?> Meshes { get; init; } = Array.Empty<StaticMesh?>();
    public IReadOnlyList<GltfNodeAsset> Nodes { get; init; } = Array.Empty<GltfNodeAsset>();
}

/// <summary>
/// 基于 SharpGLTF、无 GPU 依赖的 glTF 2.0 StaticMesh 导入器。
/// 支持 .gltf 和 .glb，保留节点层级和局部变换供编辑器创建 Actor/Component。
/// </summary>
public sealed class GltfStaticMeshImporter
{
    public GltfImportResult Import(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("glTF file was not found.", fullPath);
        var extension = Path.GetExtension(fullPath);
        if (!string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Only .gltf and .glb model files are supported.");

        var model = ModelRoot.Load(fullPath);
        var meshes = ParseMeshes(model);
        var nodes = ParseNodes(model, meshes);
        return new GltfImportResult { SourcePath = fullPath, Meshes = meshes, Nodes = nodes };
    }

    /// <summary>把导入结果构建为尚未加入 World 的 Actor/Component 层级。</summary>
    public IReadOnlyList<Actor> BuildActors(GltfImportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var actors = new Actor[result.Nodes.Count];
        var roots = new SceneComponent[result.Nodes.Count];
        for (var i = 0; i < result.Nodes.Count; i++)
        {
            var node = result.Nodes[i];
            var actor = new Actor { Name = string.IsNullOrWhiteSpace(node.Name) ? $"Node_{i}" : node.Name };
            SceneComponent component = node.Mesh == null
                ? new SceneComponent()
                : new StaticMeshComponent { Mesh = node.Mesh };
            component.RelativeTransform = node.LocalTransform;
            actor.AddOwnedComponent(component);
            actors[i] = actor;
            roots[i] = component;
        }

        for (var i = 0; i < roots.Length; i++)
        {
            if (result.Nodes[i].ParentIndex is not { } parent)
                continue;
            if ((uint)parent >= (uint)roots.Length)
                throw new InvalidDataException($"glTF node {i} references invalid parent {parent}.");
            roots[i].AttachToComponent(roots[parent], AttachmentTransformRules.KeepRelativeTransform);
        }

        return actors;
    }

    /// <summary>把导入结果转为编辑器 World 中的 Actor/Component 层级。</summary>
    public IReadOnlyList<Actor> CreateActors(GltfImportResult result, World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var actors = BuildActors(result);
        foreach (var actor in actors)
            world.AddActor(actor);
        return actors;
    }

    private static IReadOnlyList<StaticMesh?> ParseMeshes(ModelRoot model)
    {
        var result = new List<StaticMesh?>();
        foreach (var mesh in model.LogicalMeshes)
        {
            var vertices = new List<StaticMeshVertex>();
            var indices = new List<uint>();
            foreach (var primitive in mesh.Primitives)
            {
                if (primitive.DrawPrimitiveType != PrimitiveType.TRIANGLES)
                    throw new NotSupportedException("Only glTF TRIANGLES primitives are supported.");

                var positionAccessor = primitive.GetVertexAccessor("POSITION");
                if (positionAccessor == null)
                    throw new InvalidDataException("Every glTF primitive must provide POSITION.");

                var positions = positionAccessor.AsVector3Array();
                var normals = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
                var uvs = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                var colors = primitive.GetVertexAccessor("COLOR_0")?.AsColorArray();

                if (normals != null && normals.Count != positions.Count ||
                    uvs != null && uvs.Count != positions.Count ||
                    colors != null && colors.Count != positions.Count)
                    throw new InvalidDataException("glTF vertex attribute counts do not match POSITION.");

                var vertexOffset = vertices.Count;
                for (var i = 0; i < positions.Count; i++)
                {
                    var position = positions[i];
                    var color = colors == null ? Vector3.One : new Vector3(colors[i].X, colors[i].Y, colors[i].Z);
                    vertices.Add(new StaticMeshVertex(
                        position, color,
                        uvs == null ? Vector2.Zero : uvs[i],
                        normals == null ? Vector3.UnitY : normals[i]));
                }

                var sourceIndices = primitive.GetIndices();
                if (sourceIndices != null)
                {
                    foreach (var index in sourceIndices)
                    {
                        if (index >= (uint)positions.Count)
                            throw new InvalidDataException("glTF index references a vertex outside the primitive.");
                        indices.Add(checked((uint)vertexOffset + index));
                    }
                }
                else
                {
                    for (uint i = 0; i < positions.Count; i++)
                        indices.Add((uint)vertexOffset + i);
                }
            }

            result.Add(vertices.Count == 0 ? null : new StaticMesh(vertices.ToArray(), indices.ToArray()));
        }
        return result;
    }

    private static IReadOnlyList<GltfNodeAsset> ParseNodes(ModelRoot model, IReadOnlyList<StaticMesh?> meshes)
    {
        var result = new List<GltfNodeAsset>();
        foreach (var node in model.LogicalNodes)
        {
            var sourceMeshIndex = node.Mesh?.LogicalIndex;
            result.Add(new GltfNodeAsset
            {
                Index = node.LogicalIndex,
                Name = node.Name ?? string.Empty,
                ParentIndex = node.VisualParent?.LogicalIndex,
                LocalTransform = node.LocalMatrix,
                MeshIndex = sourceMeshIndex,
                Mesh = sourceMeshIndex.HasValue ? meshes[sourceMeshIndex.Value] : null,
            });
        }
        return result;
    }
}
