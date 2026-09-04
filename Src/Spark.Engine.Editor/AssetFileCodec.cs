using System.Numerics;
using System.Text;
using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

public enum EngineAssetType : byte
{
    StaticMesh = 1,
    Material = 2,
    Texture2D = 3,
    Actor = 4,
}

public sealed record AssetFileData(
    EngineAssetType AssetType,
    Guid AssetGuid,
    IReadOnlyList<Guid> Dependencies,
    byte[] Payload);

/// <summary>引擎 `.asset` 文件的首版编解码；格式为固定头加类型专属 Payload。</summary>
public static class AssetFileCodec
{
    private static readonly byte[] Magic = "ASET"u8.ToArray();
    public const ushort CurrentFormatVersion = 1;

    public static void Save(SceneResource resource, string path, IEnumerable<Guid>? dependencies = null)
    {
        var data = Encode(resource, dependencies);
        Save(data, path);
    }

    /// <summary>保存已经编码的资产数据；用于不加载资源对象即可安全重写资产身份。</summary>
    public static void Save(AssetFileData data, string path)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Enum.IsDefined(data.AssetType))
            throw new InvalidDataException($"Unsupported asset type {(byte)data.AssetType}.");
        if (data.AssetGuid == Guid.Empty)
            throw new InvalidDataException("Asset files require a non-empty AssetGuid.");
        ArgumentNullException.ThrowIfNull(data.Dependencies);
        ArgumentNullException.ThrowIfNull(data.Payload);

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
                writer.Write((byte)data.AssetType);
                writer.Write((byte)0); // flags reserved
                writer.Write(data.AssetGuid.ToByteArray());
                writer.Write(data.Dependencies.Count);
                foreach (var dependency in data.Dependencies)
                    writer.Write(dependency.ToByteArray());
                writer.Write((long)data.Payload.Length);
                writer.Write(data.Payload);
            }
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public static AssetFileData Encode(SceneResource resource, IEnumerable<Guid>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (resource.AssetGuid == Guid.Empty)
            throw new InvalidDataException("Asset files require a non-empty AssetGuid.");
        var type = GetAssetType(resource);
        var dependencySet = new HashSet<Guid>(dependencies ?? Array.Empty<Guid>());
        if (resource is Material material)
        {
            AddTexture(material.BaseColorTexture);
            AddTexture(material.NormalTexture);
            AddTexture(material.EmissiveTexture);
            AddTexture(material.MetallicRoughnessTexture);
            AddTexture(material.MaskTexture);
        }
        if (resource is ActorAsset actorAsset)
        {
            foreach (var property in actorAsset.Document.Components.SelectMany(component => component.Properties.Values))
            {
                if (property.Kind == ScenePropertyKind.AssetReference && property.Value is Guid assetGuid)
                    dependencySet.Add(assetGuid);
            }
        }
        var orderedDependencies = dependencySet
            .Where(guid => guid != Guid.Empty)
            .OrderBy(guid => guid)
            .ToArray();
        return new AssetFileData(type, resource.AssetGuid, orderedDependencies, EncodePayload(resource, type));

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

    /// <summary>读取完整的编码资产数据，但不解析类型专属 Payload。</summary>
    public static AssetFileData ReadData(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        var (type, assetGuid, dependencies, payloadLength) = ReadHeader(reader, stream);
        if (payloadLength > int.MaxValue)
            throw new InvalidDataException("Asset payload is too large.");
        var payload = reader.ReadBytes((int)payloadLength);
        if (payload.Length != (int)payloadLength || stream.Position != stream.Length)
            throw new InvalidDataException("Unexpected trailing or truncated data in asset file.");
        return new AssetFileData(type, assetGuid, dependencies, payload);
    }

    /// <summary>按 GUID 映射重写资产身份、依赖表以及已知 Payload 内的资源引用。</summary>
    public static AssetFileData RemapAssetGuids(
        AssetFileData data,
        IReadOnlyDictionary<Guid, Guid> guidMap)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(guidMap);
        var assetGuid = guidMap.TryGetValue(data.AssetGuid, out var mappedAssetGuid)
            ? mappedAssetGuid
            : data.AssetGuid;
        var dependencies = data.Dependencies
            .Select(guid => guidMap.TryGetValue(guid, out var mapped) ? mapped : guid)
            .Distinct()
            .OrderBy(guid => guid)
            .ToArray();
        var payload = data.AssetType == EngineAssetType.Material
            ? RemapMaterialPayload(data.Payload, guidMap)
            : data.AssetType == EngineAssetType.Actor
                ? RemapActorPayload(data.Payload, guidMap)
                : data.Payload.ToArray();
        return new AssetFileData(data.AssetType, assetGuid, dependencies, payload);
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
            EngineAssetType.Texture2D => DecodeTexture2D(payloadReader, assetGuid),
            EngineAssetType.Actor => DecodeActor(payload, assetGuid),
            _ => throw new InvalidDataException($"Unsupported asset type {(byte)type}.")
        };
    }

    public static SceneResource Decode(AssetFileData data, IAssetRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(data.Payload);
        using var stream = new MemoryStream(data.Payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        return data.AssetType switch
        {
            EngineAssetType.StaticMesh => DecodeStaticMesh(reader, data.AssetGuid),
            EngineAssetType.Material => DecodeMaterial(reader, data.AssetGuid, registry),
            EngineAssetType.Texture2D => DecodeTexture2D(reader, data.AssetGuid),
            EngineAssetType.Actor => DecodeActor(data.Payload, data.AssetGuid),
            _ => throw new InvalidDataException($"Unsupported asset type {(byte)data.AssetType}.")
        };
    }

    private static EngineAssetType GetAssetType(SceneResource resource) => resource switch
    {
        StaticMesh => EngineAssetType.StaticMesh,
        Material => EngineAssetType.Material,
        Texture2D => EngineAssetType.Texture2D,
        ActorAsset => EngineAssetType.Actor,
        _ => throw new NotSupportedException($"Asset type '{resource.GetType().FullName}' is not supported.")
    };

    private static byte[] RemapMaterialPayload(
        byte[] payload,
        IReadOnlyDictionary<Guid, Guid> guidMap)
    {
        using var input = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: false);
        using var output = new MemoryStream(payload.Length);
        using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
        {
            // 三个枚举字段 + 12 个 float 参数与当前 Material Payload 格式保持逐字节等价。
            writer.Write(reader.ReadByte());
            writer.Write(reader.ReadByte());
            writer.Write(reader.ReadByte());
            for (var index = 0; index < 12; index++)
                writer.Write(reader.ReadSingle());
            for (var slot = 0; slot < 5; slot++)
            {
                var hasGuid = reader.ReadBoolean();
                writer.Write(hasGuid);
                if (!hasGuid)
                    continue;
                var guid = new Guid(reader.ReadBytes(16));
                writer.Write((guidMap.TryGetValue(guid, out var mapped) ? mapped : guid).ToByteArray());
            }
        }
        if (input.Position != input.Length)
            throw new InvalidDataException("Unexpected trailing data in Material payload.");
        return output.ToArray();
    }

    private static byte[] EncodePayload(SceneResource resource, EngineAssetType type)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            switch (type)
            {
                case EngineAssetType.StaticMesh:
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
                    break;
                }
                case EngineAssetType.Material:
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
                    break;
                }
                case EngineAssetType.Texture2D:
                {
                    var texture = (Texture2D)resource;
                    writer.Write(texture.Width);
                    writer.Write(texture.Height);
                    writer.Write(texture.PixelData.Length);
                    writer.Write(texture.PixelData.Span);
                    break;
                }
                case EngineAssetType.Actor:
                {
                    var actor = (ActorAsset)resource;
                    var tempPath = Path.Combine(Path.GetTempPath(), $"spark-actor-{Guid.NewGuid():N}.scene");
                    try
                    {
                        var document = new SceneDocument();
                        document.Actors.Add(actor.Document);
                        document.Save(tempPath);
                        writer.Write(File.ReadAllBytes(tempPath));
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    break;
                }
                default:
                    throw new InvalidDataException($"Unsupported asset type {(byte)type}.");
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

    private static Texture2D DecodeTexture2D(BinaryReader reader, Guid assetGuid)
    {
        var width = reader.ReadUInt32();
        var height = reader.ReadUInt32();
        if (width == 0 || height == 0)
            throw new InvalidDataException("Texture2D dimensions must be greater than zero.");

        var expectedByteCount = (ulong)width * height * 4;
        if (expectedByteCount > int.MaxValue)
            throw new InvalidDataException("Texture2D pixel data is too large.");
        var byteCount = reader.ReadInt32();
        if (byteCount != (int)expectedByteCount)
            throw new InvalidDataException(
                $"Texture2D pixel data length {byteCount} does not match {width}x{height} RGBA8 dimensions.");
        var pixels = ReadExactly(reader, byteCount);
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Unexpected trailing data in Texture2D payload.");
        return new Texture2D(width, height, pixels) { AssetGuid = assetGuid };
    }

    private static ActorAsset DecodeActor(byte[] payload, Guid assetGuid)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"spark-actor-load-{Guid.NewGuid():N}.scene");
        try
        {
            File.WriteAllBytes(tempPath, payload);
            var document = SceneDocument.Load(tempPath);
            var actor = document.Actors.SingleOrDefault()
                ?? throw new InvalidDataException("Actor asset does not contain an Actor definition.");
            return new ActorAsset(actor) { AssetGuid = assetGuid };
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static byte[] RemapActorPayload(byte[] payload, IReadOnlyDictionary<Guid, Guid> guidMap)
    {
        var asset = DecodeActor(payload, Guid.NewGuid());
        var source = asset.Document;
        var document = new SceneActorDocument
        {
            ActorGuid = source.ActorGuid,
            ActorType = source.ActorType,
            Name = source.Name,
            RootComponentGuid = source.RootComponentGuid,
            EditorFolderGuid = source.EditorFolderGuid,
            EditorLevelGuid = source.EditorLevelGuid,
        };
        document.EditorDataLayerGuids.AddRange(source.EditorDataLayerGuids);
        foreach (var component in source.Components)
        {
            var clone = new SceneComponentDocument
            {
                ComponentGuid = component.ComponentGuid,
                ComponentType = component.ComponentType,
                ParentComponentGuid = component.ParentComponentGuid,
                AttachSocketName = component.AttachSocketName,
                RelativeLocation = component.RelativeLocation,
                RelativeRotation = component.RelativeRotation,
                RelativeScale = component.RelativeScale,
                Sockets = component.Sockets.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                Properties = component.Properties.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Kind == ScenePropertyKind.AssetReference && pair.Value.Value is Guid guid && guidMap.TryGetValue(guid, out var mapped)
                        ? new ScenePropertyValue(pair.Value.Kind, mapped)
                        : pair.Value,
                    StringComparer.Ordinal),
            };
            document.Components.Add(clone);
        }
        var tempPath = Path.Combine(Path.GetTempPath(), $"spark-actor-remap-{Guid.NewGuid():N}.scene");
        try
        {
            var scene = new SceneDocument();
            scene.Actors.Add(document);
            scene.Save(tempPath);
            return File.ReadAllBytes(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
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
