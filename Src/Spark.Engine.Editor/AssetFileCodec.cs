using System.Numerics;
using System.Text;
using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

public enum EngineAssetType : byte
{
    StaticMesh = 1,
    Material = 2,
}

/// <summary>引擎 `.asset` 文件的首版编解码；格式为固定头加类型专属 Payload。</summary>
public static class AssetFileCodec
{
    private static readonly byte[] Magic = "ASET"u8.ToArray();
    public const ushort CurrentFormatVersion = 1;

    public static void Save(SceneResource resource, string path, IEnumerable<Guid>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (resource.AssetGuid == Guid.Empty)
            throw new InvalidDataException("Asset files require a non-empty AssetGuid.");

        var type = resource switch
        {
            StaticMesh => EngineAssetType.StaticMesh,
            Material => EngineAssetType.Material,
            _ => throw new NotSupportedException($"Asset type '{resource.GetType().FullName}' is not supported.")
        };
        var payload = EncodePayload(resource, type);
        var dependencySet = new HashSet<Guid>(dependencies ?? Array.Empty<Guid>());
        if (resource is Material material)
        {
            AddTexture(material.BaseColorTexture);
            AddTexture(material.NormalTexture);
            AddTexture(material.EmissiveTexture);
            AddTexture(material.MetallicRoughnessTexture);
            AddTexture(material.MaskTexture);
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null)
            Directory.CreateDirectory(directory);
        var tempPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false))
            {
                writer.Write(Magic);
                writer.Write(CurrentFormatVersion);
                writer.Write((byte)type);
                writer.Write((byte)0); // flags reserved
                writer.Write(resource.AssetGuid.ToByteArray());
                var orderedDependencies = dependencySet.Where(guid => guid != Guid.Empty).OrderBy(guid => guid).ToArray();
                writer.Write(orderedDependencies.Length);
                foreach (var dependency in orderedDependencies)
                    writer.Write(dependency.ToByteArray());
                writer.Write((long)payload.Length);
                writer.Write(payload);
            }
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        void AddTexture(Texture2D? texture)
        {
            if (texture != null)
                dependencySet.Add(texture.AssetGuid);
        }
    }

    public static AssetRecord ReadMetadata(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        var (type, assetGuid, dependencies, payloadLength) = ReadHeader(reader, stream);
        stream.Seek(payloadLength, SeekOrigin.Current);
        if (stream.Position != stream.Length)
            throw new InvalidDataException("Unexpected trailing data in asset file.");
        return new AssetRecord
        {
            AssetGuid = assetGuid,
            AssetType = type.ToString(),
            Dependencies = dependencies,
            ImportStatus = AssetImportStatus.Unknown,
        };
    }

    public static SceneResource Load(string path, IAssetRegistry? registry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        var (type, assetGuid, _, payloadLength) = ReadHeader(reader, stream);
        if (payloadLength > int.MaxValue)
            throw new InvalidDataException("Asset payload is too large.");
        var payload = reader.ReadBytes((int)payloadLength);
        if (payload.Length != (int)payloadLength || stream.Position != stream.Length)
            throw new InvalidDataException("Unexpected trailing or truncated data in asset file.");

        using var payloadStream = new MemoryStream(payload, writable: false);
        using var payloadReader = new BinaryReader(payloadStream, Encoding.UTF8, leaveOpen: false);
        return type switch
        {
            EngineAssetType.StaticMesh => DecodeStaticMesh(payloadReader, assetGuid),
            EngineAssetType.Material => DecodeMaterial(payloadReader, assetGuid, registry),
            _ => throw new InvalidDataException($"Unsupported asset type {(byte)type}.")
        };
    }

    private static byte[] EncodePayload(SceneResource resource, EngineAssetType type)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            if (type == EngineAssetType.StaticMesh)
            {
                var mesh = (StaticMesh)resource;
                writer.Write(mesh.Vertices.Length);
                foreach (var vertex in mesh.Vertices.Span)
                {
                    WriteVector3(writer, vertex.Position);
                    WriteVector3(writer, vertex.Color);
                    WriteVector2(writer, vertex.Uv);
                    WriteVector3(writer, vertex.Normal);
                }
                writer.Write(mesh.Indices.Length);
                foreach (var index in mesh.Indices.Span)
                    writer.Write(index);
            }
            else
            {
                var material = (Material)resource;
                writer.Write((byte)material.ShadingModel);
                writer.Write((byte)material.BlendMode);
                writer.Write((byte)material.CullMode);
                WriteVector4(writer, material.BaseColor);
                writer.Write(material.Metallic);
                writer.Write(material.Roughness);
                WriteVector4(writer, material.EmissiveColor);
                writer.Write(material.EmissiveStrength);
                writer.Write(material.NormalStrength);
                WriteNullableGuid(writer, material.BaseColorTexture?.AssetGuid);
                WriteNullableGuid(writer, material.NormalTexture?.AssetGuid);
                WriteNullableGuid(writer, material.EmissiveTexture?.AssetGuid);
                WriteNullableGuid(writer, material.MetallicRoughnessTexture?.AssetGuid);
                WriteNullableGuid(writer, material.MaskTexture?.AssetGuid);
            }
        }
        return stream.ToArray();
    }

    private static StaticMesh DecodeStaticMesh(BinaryReader reader, Guid assetGuid)
    {
        var vertexCount = ReadCount(reader, "vertex");
        var vertices = new StaticMeshVertex[vertexCount];
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = new StaticMeshVertex(ReadVector3(reader), ReadVector3(reader), ReadVector2(reader), ReadVector3(reader));
        var indexCount = ReadCount(reader, "index");
        var indices = new uint[indexCount];
        for (var i = 0; i < indices.Length; i++)
            indices[i] = reader.ReadUInt32();
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Unexpected trailing data in StaticMesh payload.");
        return new StaticMesh(vertices, indices) { AssetGuid = assetGuid };
    }

    private static Material DecodeMaterial(BinaryReader reader, Guid assetGuid, IAssetRegistry? registry)
    {
        var material = new Material
        {
            ShadingModel = (ShadingModel)reader.ReadByte(),
            BlendMode = (BlendMode)reader.ReadByte(),
            CullMode = (MaterialCullMode)reader.ReadByte(),
            BaseColor = ReadVector4(reader),
            Metallic = reader.ReadSingle(),
            Roughness = reader.ReadSingle(),
            EmissiveColor = ReadVector4(reader),
            EmissiveStrength = reader.ReadSingle(),
            NormalStrength = reader.ReadSingle(),
            AssetGuid = assetGuid,
        };
        material.BaseColorTexture = ResolveTexture(ReadNullableGuid(reader), registry);
        material.NormalTexture = ResolveTexture(ReadNullableGuid(reader), registry);
        material.EmissiveTexture = ResolveTexture(ReadNullableGuid(reader), registry);
        material.MetallicRoughnessTexture = ResolveTexture(ReadNullableGuid(reader), registry);
        material.MaskTexture = ResolveTexture(ReadNullableGuid(reader), registry);
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Unexpected trailing data in Material payload.");
        return material;
    }

    private static Texture2D? ResolveTexture(Guid? guid, IAssetRegistry? registry)
    {
        if (guid is not { } textureGuid)
            return null;
        if (registry == null || registry.Resolve(textureGuid) is not Texture2D texture)
            throw new InvalidDataException($"Texture asset '{textureGuid}' could not be resolved.");
        return texture;
    }

    private static (EngineAssetType Type, Guid AssetGuid, IReadOnlyList<Guid> Dependencies, long PayloadLength) ReadHeader(BinaryReader reader, FileStream stream)
    {
        if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Invalid asset file magic.");
        var version = reader.ReadUInt16();
        if (version != CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported asset format version {version}; supported version is {CurrentFormatVersion}.");
        var type = (EngineAssetType)reader.ReadByte();
        if (!Enum.IsDefined(type))
            throw new InvalidDataException($"Unknown asset type {(byte)type}.");
        _ = reader.ReadByte();
        var assetGuid = new Guid(ReadExactly(reader, 16));
        if (assetGuid == Guid.Empty)
            throw new InvalidDataException("Asset files require a non-empty AssetGuid.");
        var dependencyCount = ReadCount(reader, "dependency");
        var dependencies = new List<Guid>(dependencyCount);
        for (var i = 0; i < dependencyCount; i++)
            dependencies.Add(new Guid(ReadExactly(reader, 16)));
        var payloadLength = reader.ReadInt64();
        if (payloadLength < 0 || payloadLength > int.MaxValue || payloadLength > stream.Length - stream.Position)
            throw new InvalidDataException("Invalid asset payload length.");
        return (type, assetGuid, dependencies, payloadLength);
    }

    private static int ReadCount(BinaryReader reader, string kind)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > 1_000_000)
            throw new InvalidDataException($"Invalid {kind} count {count}.");
        return count;
    }

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count)
            throw new InvalidDataException("Unexpected end of asset file.");
        return bytes;
    }

    private static void WriteVector2(BinaryWriter writer, Vector2 value) { writer.Write(value.X); writer.Write(value.Y); }
    private static Vector2 ReadVector2(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle());
    private static void WriteVector3(BinaryWriter writer, Vector3 value) { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); }
    private static Vector3 ReadVector3(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private static void WriteVector4(BinaryWriter writer, Vector4 value) { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); writer.Write(value.W); }
    private static Vector4 ReadVector4(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private static void WriteNullableGuid(BinaryWriter writer, Guid? value) { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value.ToByteArray()); }
    private static Guid? ReadNullableGuid(BinaryReader reader) => reader.ReadBoolean() ? new Guid(ReadExactly(reader, 16)) : null;
}
