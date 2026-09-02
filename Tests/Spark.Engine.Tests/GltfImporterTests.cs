using System.Numerics;
using Spark.Engine.Components;
using Spark.Engine.Editor;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class GltfImporterTests
{
    [Fact]
    public void ImportsEmbeddedStaticMeshAndPreservesNodeHierarchy()
    {
        var root = Path.Combine(Path.GetTempPath(), "spark-gltf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "triangle.gltf");
        try
        {
            var bytes = new byte[42];
            WriteFloat(bytes, 0, 0); WriteFloat(bytes, 4, 0); WriteFloat(bytes, 8, 0);
            WriteFloat(bytes, 12, 1); WriteFloat(bytes, 16, 0); WriteFloat(bytes, 20, 0);
            WriteFloat(bytes, 24, 0); WriteFloat(bytes, 28, 1); WriteFloat(bytes, 32, 0);
            BitConverter.GetBytes((ushort)0).CopyTo(bytes, 36);
            BitConverter.GetBytes((ushort)1).CopyTo(bytes, 38);
            BitConverter.GetBytes((ushort)2).CopyTo(bytes, 40);
            var encoded = Convert.ToBase64String(bytes);
            var json = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "byteLength": 42, "uri": "data:application/octet-stream;base64,{{encoded}}" }],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 6 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0,0,0], "max": [1,1,0] },
                { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ],
              "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1 }] }],
              "nodes": [
                { "name": "Root", "children": [1] },
                { "name": "Triangle", "mesh": 0, "translation": [2, 3, 4] }
              ],
              "scenes": [{ "nodes": [0] }],
              "scene": 0
            }
            """;
            File.WriteAllText(path, json);

            var result = new GltfStaticMeshImporter().Import(path);

            Assert.Equal(Path.GetFullPath(path), result.SourcePath);
            Assert.Equal(2, result.Nodes.Count);
            Assert.Equal("Root", result.Nodes[0].Name);
            Assert.Equal(0, result.Nodes[1].ParentIndex);
            Assert.Equal(new Vector3(2, 3, 4), result.Nodes[1].LocalTransform.Translation);
            var mesh = result.Nodes[1].Mesh;
            Assert.NotNull(mesh);
            Assert.Equal(3, mesh!.Vertices.Length);
            Assert.Equal<uint[]>([0, 1, 2], mesh.Indices.ToArray());

            using var world = new World(new ResourceManager());
            var actors = new GltfStaticMeshImporter().CreateActors(result, world);
            world.Update(0.016f);
            Assert.Equal(2, actors.Count);
            Assert.Same(actors[1], actors[1].GetComponent<StaticMeshComponent>()!.Owner);
            Assert.Same(mesh, actors[1].GetComponent<StaticMeshComponent>()!.Mesh);
            Assert.Same(actors[0].RootComponent, actors[1].RootComponent!.AttachParent);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RejectsGlbUntilBinaryContainerSupportIsAdded()
    {
        var path = Path.Combine(Path.GetTempPath(), "spark-gltf-" + Guid.NewGuid().ToString("N") + ".glb");
        File.WriteAllBytes(path, []);
        try
        {
            Assert.Throws<NotSupportedException>(() => new GltfStaticMeshImporter().Import(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WriteFloat(byte[] buffer, int offset, float value)
        => BitConverter.GetBytes(value).CopyTo(buffer, offset);
}
