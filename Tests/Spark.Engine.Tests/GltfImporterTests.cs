using System.Numerics;
using System.Text;
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
    public void ImportsBinaryGlbStaticMeshAndPreservesNodeHierarchy()
    {
        var root = Path.Combine(Path.GetTempPath(), "spark-glb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "triangle.glb");
        WriteTriangleGlb(path);
        try
        {
            var result = new GltfStaticMeshImporter().Import(path);

            Assert.Equal(Path.GetFullPath(path), result.SourcePath);
            Assert.Equal(2, result.Nodes.Count);
            Assert.Equal(0, result.Nodes[1].ParentIndex);
            Assert.Equal(new Vector3(2, 3, 4), result.Nodes[1].LocalTransform.Translation);
            var mesh = Assert.IsType<StaticMesh>(result.Nodes[1].Mesh);
            Assert.Equal(3, mesh.Vertices.Length);
            Assert.Equal<uint[]>([0, 1, 2], mesh.Indices.ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(".gltf")]
    [InlineData(".glb")]
    public void ImportServiceWritesStableAssetsWithoutModifyingScene(string extension)
    {
        var root = Path.Combine(Path.GetTempPath(), "spark-gltf-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "triangle" + extension);
        if (extension == ".glb")
            WriteTriangleGlb(sourcePath);
        else
            WriteTriangleGltf(sourcePath);
        try
        {
            using var world = new World(new ResourceManager());
            using var context = new EditorContext(world);
            var result = new GltfImportService().ImportIntoEditor(
                sourcePath, Path.Combine(root, "AssetsA"), context);

            var importedAsset = Assert.Single(result.Assets);
            Assert.True(File.Exists(importedAsset.AssetPath));
            Assert.Equal(importedAsset.AssetGuid, AssetFileCodec.ReadMetadata(importedAsset.AssetPath).AssetGuid);
            Assert.Same(importedAsset.Resource, context.AssetRegistry.Resolve(importedAsset.AssetGuid));
            Assert.Empty(world.EnumerateActors(includePendingActors: true));
            Assert.False(context.IsDirty);
            Assert.False(context.History.CanUndo);
            Assert.False(context.History.CanRedo);

            using var secondWorld = new World(new ResourceManager());
            using var secondContext = new EditorContext(secondWorld);
            var second = new GltfImportService().ImportIntoEditor(
                sourcePath, Path.Combine(root, "AssetsB"), secondContext);
            Assert.Equal(importedAsset.AssetGuid, Assert.Single(second.Assets).AssetGuid);

            foreach (var asset in result.Assets.Concat(second.Assets))
                asset.Resource.Dispose();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(".gltf")]
    [InlineData(".glb")]
    public void EditorUiImportsModelIntoCurrentContentDirectory(string extension)
    {
        var root = Path.Combine(Path.GetTempPath(), "spark-current-model-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sourcePath = Path.Combine(root, "triangle" + extension);
            if (extension == ".glb")
                WriteTriangleGlb(sourcePath);
            else
                WriteTriangleGltf(sourcePath);

            var project = EditorProject.Open(Path.Combine(root, "Project"));
            var targetDirectory = Path.Combine(project.ContentDirectory, "Environment", "Props");
            Directory.CreateDirectory(targetDirectory);
            using var world = new World(new ResourceManager());
            var editor = new EditorUi(world, project: project);
            editor.ContentBrowser.SelectedDirectory = "Environment/Props";

            var result = editor.ImportModel(sourcePath);

            var asset = Assert.Single(result.Assets);
            Assert.Equal(Path.Combine(targetDirectory, "triangle.mesh-0000.asset"), asset.AssetPath);
            var record = Assert.Single(editor.AssetRegistry.Records,
                item => item.AssetGuid == asset.AssetGuid);
            Assert.Equal("Environment/Props/triangle.mesh-0000.asset", record.ContentPath);
            Assert.True(File.Exists(asset.AssetPath));
            Assert.Empty(world.EnumerateActors(includePendingActors: true));
            asset.Resource.Dispose();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteTriangleGltf(string path)
    {
        var bytes = CreateTriangleBuffer();
        var encoded = Convert.ToBase64String(bytes);
        File.WriteAllText(path, CreateTriangleJson($"data:application/octet-stream;base64,{encoded}"));
    }

    private static void WriteTriangleGlb(string path)
    {
        var binaryBytes = CreateTriangleBuffer();
        var jsonBytes = Encoding.UTF8.GetBytes(CreateTriangleJson(bufferUri: null));
        var paddedJsonLength = AlignToFourBytes(jsonBytes.Length);
        var paddedBinaryLength = AlignToFourBytes(binaryBytes.Length);
        var totalLength = checked(12 + 8 + paddedJsonLength + 8 + paddedBinaryLength);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(0x46546C67u); // glTF
        writer.Write(2u);
        writer.Write((uint)totalLength);
        writer.Write((uint)paddedJsonLength);
        writer.Write(0x4E4F534Au); // JSON
        writer.Write(jsonBytes);
        for (var i = jsonBytes.Length; i < paddedJsonLength; i++)
            writer.Write((byte)0x20);
        writer.Write((uint)paddedBinaryLength);
        writer.Write(0x004E4942u); // BIN
        writer.Write(binaryBytes);
        for (var i = binaryBytes.Length; i < paddedBinaryLength; i++)
            writer.Write((byte)0);
    }

    private static string CreateTriangleJson(string? bufferUri)
    {
        var buffer = bufferUri == null
            ? "{ \"byteLength\": 42 }"
            : $"{{ \"byteLength\": 42, \"uri\": \"{bufferUri}\" }}";
        return $$"""
        {
          "asset": { "version": "2.0" },
          "buffers": [{{buffer}}],
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
          ]
        }
        """;
    }

    private static byte[] CreateTriangleBuffer()
    {
        var bytes = new byte[42];
        WriteFloat(bytes, 0, 0); WriteFloat(bytes, 4, 0); WriteFloat(bytes, 8, 0);
        WriteFloat(bytes, 12, 1); WriteFloat(bytes, 16, 0); WriteFloat(bytes, 20, 0);
        WriteFloat(bytes, 24, 0); WriteFloat(bytes, 28, 1); WriteFloat(bytes, 32, 0);
        BitConverter.GetBytes((ushort)0).CopyTo(bytes, 36);
        BitConverter.GetBytes((ushort)1).CopyTo(bytes, 38);
        BitConverter.GetBytes((ushort)2).CopyTo(bytes, 40);
        return bytes;
    }

    private static int AlignToFourBytes(int length) => checked((length + 3) & ~3);

    private static void WriteFloat(byte[] buffer, int offset, float value)
        => BitConverter.GetBytes(value).CopyTo(buffer, offset);
}
