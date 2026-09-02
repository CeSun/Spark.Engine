using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
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
/// 无 GPU 依赖的 glTF 2.0 StaticMesh 导入器。首版明确拒绝动画、骨骼等运行时数据，
/// 但保留节点层级和局部变换供编辑器创建 Actor/Component。
/// </summary>
public sealed class GltfStaticMeshImporter
{
    private const int FloatComponent = 5126;
    private const int UnsignedByteComponent = 5121;
    private const int UnsignedShortComponent = 5123;
    private const int UnsignedIntComponent = 5125;

    public GltfImportResult Import(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("glTF file was not found.", fullPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".gltf", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("The first glTF importer supports .gltf JSON files; .glb is not supported yet.");

        using var json = JsonDocument.Parse(File.ReadAllBytes(fullPath));
        var root = json.RootElement;
        if (!root.TryGetProperty("asset", out var asset) ||
            !asset.TryGetProperty("version", out var version) ||
            !string.Equals(version.GetString(), "2.0", StringComparison.Ordinal))
            throw new InvalidDataException("Only glTF 2.0 assets are supported.");
        var buffers = LoadBuffers(root, Path.GetDirectoryName(fullPath)!);
        var meshes = ParseMeshes(root, buffers);
        var nodes = ParseNodes(root, meshes);
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

    private static IReadOnlyList<byte[]> LoadBuffers(JsonElement root, string directory)
    {
        var result = new List<byte[]>();
        if (!root.TryGetProperty("buffers", out var buffers))
            return result;

        foreach (var buffer in buffers.EnumerateArray())
        {
            if (!buffer.TryGetProperty("uri", out var uriElement))
                throw new InvalidDataException("A .gltf buffer without a URI requires .glb support.");
            var uri = uriElement.GetString() ?? string.Empty;
            byte[] data;
            if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = uri.IndexOf(',');
                if (comma < 0 || !uri[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Only base64 data URIs are supported for glTF buffers.");
                try { data = Convert.FromBase64String(uri[(comma + 1)..]); }
                catch (FormatException ex) { throw new InvalidDataException("Invalid base64 glTF buffer URI.", ex); }
            }
            else
            {
                var externalPath = Path.GetFullPath(Path.Combine(directory, uri.Replace('/', Path.DirectorySeparatorChar)));
                data = File.ReadAllBytes(externalPath);
            }

            if (buffer.TryGetProperty("byteLength", out var length) && data.Length < length.GetInt32())
                throw new InvalidDataException("glTF buffer is shorter than its declared byteLength.");
            result.Add(data);
        }
        return result;
    }

    private static IReadOnlyList<StaticMesh?> ParseMeshes(JsonElement root, IReadOnlyList<byte[]> buffers)
    {
        var result = new List<StaticMesh?>();
        if (!root.TryGetProperty("meshes", out var meshes))
            return result;

        foreach (var mesh in meshes.EnumerateArray())
        {
            var vertices = new List<StaticMeshVertex>();
            var indices = new List<uint>();
            if (!mesh.TryGetProperty("primitives", out var primitives))
                throw new InvalidDataException("glTF mesh has no primitives.");

            foreach (var primitive in primitives.EnumerateArray())
            {
                var mode = primitive.TryGetProperty("mode", out var modeProperty) ? modeProperty.GetInt32() : 4;
                if (mode != 4)
                    throw new NotSupportedException("Only glTF TRIANGLES primitives are supported.");
                if (!primitive.TryGetProperty("attributes", out var attributes) ||
                    !attributes.TryGetProperty("POSITION", out var positionAccessor))
                    throw new InvalidDataException("Every glTF primitive must provide POSITION.");

                var positions = ReadVector(positionAccessor.GetInt32(), root, buffers, 3);
                var normals = attributes.TryGetProperty("NORMAL", out var normalAccessor)
                    ? ReadVector(normalAccessor.GetInt32(), root, buffers, 3) : null;
                var uvs = attributes.TryGetProperty("TEXCOORD_0", out var uvAccessor)
                    ? ReadVector(uvAccessor.GetInt32(), root, buffers, 2) : null;
                var colors = attributes.TryGetProperty("COLOR_0", out var colorAccessor)
                    ? ReadVector(colorAccessor.GetInt32(), root, buffers, 4, allowVec3: true) : null;

                if (normals != null && normals.Count != positions.Count ||
                    uvs != null && uvs.Count != positions.Count ||
                    colors != null && colors.Count != positions.Count)
                    throw new InvalidDataException("glTF vertex attribute counts do not match POSITION.");

                var vertexOffset = vertices.Count;
                for (var i = 0; i < positions.Count; i++)
                {
                    var color = colors == null ? Vector3.One : new Vector3(colors[i].X, colors[i].Y, colors[i].Z);
                    vertices.Add(new StaticMeshVertex(
                        new Vector3(positions[i].X, positions[i].Y, positions[i].Z), color,
                        uvs == null ? Vector2.Zero : new Vector2(uvs[i].X, uvs[i].Y),
                        normals == null ? Vector3.UnitY : new Vector3(normals[i].X, normals[i].Y, normals[i].Z)));
                }

                if (primitive.TryGetProperty("indices", out var indexAccessor))
                {
                    foreach (var index in ReadIndices(indexAccessor.GetInt32(), root, buffers))
                    {
                        if (index >= positions.Count)
                            throw new InvalidDataException("glTF index references a vertex outside the primitive.");
                        indices.Add((uint)vertexOffset + index);
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

    private static IReadOnlyList<GltfNodeAsset> ParseNodes(JsonElement root, IReadOnlyList<StaticMesh?> meshes)
    {
        var result = new List<GltfNodeAsset>();
        if (!root.TryGetProperty("nodes", out var nodes))
            return result;

        foreach (var (node, index) in nodes.EnumerateArray().Select((node, index) => (node, index)))
        {
            var transform = Matrix4x4.Identity;
            if (node.TryGetProperty("matrix", out var matrix))
            {
                var values = matrix.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                if (values.Length != 16) throw new InvalidDataException("glTF node matrix must contain 16 values.");
                // glTF matrices are column-major; System.Numerics uses row-vector composition.
                transform = new Matrix4x4(
                    values[0], values[4], values[8], values[12],
                    values[1], values[5], values[9], values[13],
                    values[2], values[6], values[10], values[14],
                    values[3], values[7], values[11], values[15]);
            }
            else
            {
                var translation = ReadVectorProperty(node, "translation", 3, Vector4.Zero);
                var rotation = ReadVectorProperty(node, "rotation", 4, new Vector4(0, 0, 0, 1));
                var scale = ReadVectorProperty(node, "scale", 3, new Vector4(1, 1, 1, 0));
                transform = Matrix4x4.CreateScale(scale.X, scale.Y, scale.Z) *
                            Matrix4x4.CreateFromQuaternion(new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W)) *
                            Matrix4x4.CreateTranslation(translation.X, translation.Y, translation.Z);
            }

            int? sourceMeshIndex = null;
            StaticMesh? mesh = null;
            if (node.TryGetProperty("mesh", out var meshIndex))
            {
                var indexValue = meshIndex.GetInt32();
                if ((uint)indexValue >= (uint)meshes.Count)
                    throw new InvalidDataException("glTF node references an invalid mesh.");
                sourceMeshIndex = indexValue;
                mesh = meshes[indexValue];
            }
            result.Add(new GltfNodeAsset
            {
                Index = index,
                Name = node.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                LocalTransform = transform,
                MeshIndex = sourceMeshIndex,
                Mesh = mesh,
            });
        }

        foreach (var parent in nodes.EnumerateArray().Select((node, index) => (node, index)))
        {
            if (!parent.node.TryGetProperty("children", out var children)) continue;
            foreach (var child in children.EnumerateArray())
            {
                var childIndex = child.GetInt32();
                if ((uint)childIndex >= (uint)result.Count || result[childIndex].ParentIndex.HasValue)
                    throw new InvalidDataException("glTF node hierarchy contains an invalid or multiply-parented child.");
                result[childIndex].ParentIndex = parent.index;
            }
        }
        return result;
    }

    private static Vector4 ReadVectorProperty(JsonElement element, string name, int components, Vector4 fallback)
    {
        if (!element.TryGetProperty(name, out var value)) return fallback;
        var values = value.EnumerateArray().Select(v => v.GetSingle()).ToArray();
        if (values.Length != components) throw new InvalidDataException($"glTF node {name} must have {components} values.");
        return components switch
        {
            3 => new Vector4(values[0], values[1], values[2], 0),
            _ => new Vector4(values[0], values[1], values[2], values[3]),
        };
    }

    private static List<Vector4> ReadVector(int accessorIndex, JsonElement root, IReadOnlyList<byte[]> buffers, int expectedComponents, bool allowVec3 = false)
    {
        var accessor = GetArrayElement(root, "accessors", accessorIndex);
        var type = accessor.GetProperty("type").GetString();
        var components = type switch { "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, _ => 0 };
        if (components != expectedComponents && !(allowVec3 && components == 3))
            throw new InvalidDataException($"glTF accessor type '{type}' is not supported for this attribute.");
        if (accessor.GetProperty("componentType").GetInt32() != FloatComponent)
            throw new InvalidDataException("Only float glTF vertex attributes are supported.");
        var values = ReadAccessorBytes(accessor, root, buffers, sizeof(float) * components);
        var result = new List<Vector4>(values.Count);
        foreach (var bytes in values)
        {
            var v = new float[components];
            for (var i = 0; i < components; i++) v[i] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[(i * 4)..]));
            result.Add(new Vector4(v[0], v[1], components > 2 ? v[2] : 0, components > 3 ? v[3] : 0));
        }
        return result;
    }

    private static IEnumerable<uint> ReadIndices(int accessorIndex, JsonElement root, IReadOnlyList<byte[]> buffers)
    {
        var accessor = GetArrayElement(root, "accessors", accessorIndex);
        if (accessor.GetProperty("type").GetString() != "SCALAR") throw new InvalidDataException("glTF indices must be SCALAR.");
        var componentType = accessor.GetProperty("componentType").GetInt32();
        var width = componentType switch { UnsignedByteComponent => 1, UnsignedShortComponent => 2, UnsignedIntComponent => 4, _ => 0 };
        if (width == 0) throw new InvalidDataException("glTF indices must use unsigned byte, short, or int.");
        foreach (var bytes in ReadAccessorBytes(accessor, root, buffers, width))
            yield return width switch { 1 => bytes.Span[0], 2 => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Span), _ => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Span) };
    }

    private static List<ReadOnlyMemory<byte>> ReadAccessorBytes(JsonElement accessor, JsonElement root, IReadOnlyList<byte[]> buffers, int elementSize)
    {
        if (!accessor.TryGetProperty("bufferView", out var viewProperty)) throw new InvalidDataException("Sparse or accessor-only glTF data is not supported.");
        var view = GetArrayElement(root, "bufferViews", viewProperty.GetInt32());
        var bufferIndex = view.GetProperty("buffer").GetInt32();
        if ((uint)bufferIndex >= (uint)buffers.Count) throw new InvalidDataException("glTF bufferView references an invalid buffer.");
        var bytes = buffers[bufferIndex];
        var viewOffset = view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0;
        var accessorOffset = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;
        var count = accessor.GetProperty("count").GetInt32();
        var stride = view.TryGetProperty("byteStride", out var strideProperty) ? strideProperty.GetInt32() : elementSize;
        if (stride < elementSize || viewOffset < 0 || accessorOffset < 0 || count < 0)
            throw new InvalidDataException("Invalid glTF accessor layout.");
        var start = checked(viewOffset + accessorOffset);
        var result = new List<ReadOnlyMemory<byte>>(count);
        for (var i = 0; i < count; i++)
        {
            var offset = checked(start + i * stride);
            if (offset < 0 || offset + elementSize > bytes.Length) throw new InvalidDataException("glTF accessor exceeds its buffer.");
            result.Add(new ReadOnlyMemory<byte>(bytes, offset, elementSize));
        }
        return result;
    }

    private static JsonElement GetArrayElement(JsonElement root, string property, int index)
    {
        if (!root.TryGetProperty(property, out var array) || index < 0 || index >= array.GetArrayLength())
            throw new InvalidDataException($"glTF references invalid {property} index {index}.");
        return array[index];
    }
}
