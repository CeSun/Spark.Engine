using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>编辑器场景的稳定内存表示；它是保存和 RuntimeWorld 实例化的共同输入。</summary>
public sealed class SceneDocument
{
    public const ushort CurrentFormatVersion = 7;
    public Guid SceneGuid { get; set; } = Guid.NewGuid();
    public ushort FormatVersion { get; init; } = CurrentFormatVersion;
    public List<SceneActorDocument> Actors { get; } = [];
    public List<SceneEditorFolderDocument> EditorFolders { get; } = [];
    public List<SceneEditorLevelDocument> EditorLevels { get; } = [];
    public List<SceneDataLayerDocument> DataLayers { get; } = [];
    public List<SceneUnloadedActorDocument> UnloadedActors { get; } = [];

    public static SceneDocument Capture(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var document = new SceneDocument();
        var outliner = EditorWorldOutlinerData.For(world);
        foreach (var folder in outliner.Folders.OrderBy(folder => folder.FolderGuid))
            document.EditorFolders.Add(new SceneEditorFolderDocument
            {
                FolderGuid = folder.FolderGuid,
                ParentFolderGuid = folder.ParentFolderGuid,
                Name = folder.Name,
            });
        foreach (var level in outliner.Levels.OrderBy(level => level.LevelGuid))
            document.EditorLevels.Add(new SceneEditorLevelDocument
            {
                LevelGuid = level.LevelGuid,
                Name = level.Name,
            });
        foreach (var layer in outliner.DataLayers.OrderBy(layer => layer.DataLayerGuid))
            document.DataLayers.Add(new SceneDataLayerDocument
            {
                DataLayerGuid = layer.DataLayerGuid,
                Name = layer.Name,
            });
        foreach (var unloaded in outliner.UnloadedActors.OrderBy(actor => actor.ActorGuid))
        {
            var record = new SceneUnloadedActorDocument
            {
                ActorGuid = unloaded.ActorGuid,
                Label = unloaded.Label,
                ActorType = unloaded.ActorType,
                EditorLevelGuid = unloaded.LevelGuid,
            };
            record.EditorDataLayerGuids.AddRange(unloaded.DataLayerGuids);
            document.UnloadedActors.Add(record);
        }
        foreach (var actor in world.EnumerateActors(includePendingActors: true).OrderBy(a => a.ActorGuid))
        {
            if (Attribute.IsDefined(actor.GetType(), typeof(SceneTransientAttribute), inherit: true))
                continue;
            var actorDocument = CaptureActor(actor);
            actorDocument.EditorFolderGuid = outliner.GetActorFolder(actor.ActorGuid);
            actorDocument.EditorLevelGuid = outliner.GetActorLevel(actor.ActorGuid);
            actorDocument.EditorDataLayerGuids.AddRange(outliner.GetActorDataLayers(actor.ActorGuid));
            document.Actors.Add(actorDocument);
        }

        return document;
    }

    internal static SceneActorDocument CaptureActor(Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var actorDocument = new SceneActorDocument
        {
            ActorGuid = actor.ActorGuid,
            ActorType = actor.GetType().AssemblyQualifiedName ?? actor.GetType().FullName ?? actor.GetType().Name,
            Name = actor.Name,
            RootComponentGuid = actor.RootComponent?.ComponentGuid,
        };

        foreach (var component in actor.Components.OrderBy(c => c.ComponentGuid))
        {
            var scene = component as SceneComponent;
            actorDocument.Components.Add(new SceneComponentDocument
            {
                ComponentGuid = component.ComponentGuid,
                ComponentType = component.GetType().AssemblyQualifiedName ?? component.GetType().FullName ?? component.GetType().Name,
                ParentComponentGuid = scene?.AttachParent?.ComponentGuid,
                AttachSocketName = scene?.AttachSocketName,
                RelativeLocation = scene?.RelativeLocation ?? Vector3.Zero,
                RelativeRotation = scene?.RelativeRotation ?? Quaternion.Identity,
                RelativeScale = scene?.RelativeScale ?? Vector3.One,
                Properties = ScenePropertySerializer.Capture(component),
            });
            if (scene != null)
                actorDocument.Components[^1].Sockets = scene.Sockets.ToDictionary(
                    pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        return actorDocument;
    }

    public void Save(string path) => SceneDocumentBinary.Write(this, path);

    public static SceneDocument Load(string path) => SceneDocumentBinary.Read(path);

    public static SceneDocument Deserialize(ReadOnlyMemory<byte> data) => SceneDocumentBinary.Read(data);

    /// <summary>兼容旧的委托解析入口。</summary>
    public World InstantiateWorld(ResourceManager resourceManager, Func<Guid, SceneResource?> assetResolver)
    {
        ArgumentNullException.ThrowIfNull(assetResolver);
        return InstantiateWorld(resourceManager, new DelegateAssetRegistry(assetResolver), null);
    }

    /// <summary>
    /// 从文档创建全新的运行时 World。资产解析和自定义组件创建分别通过注册表和 RuntimeActorFactory 扩展。
    /// </summary>
    public World InstantiateWorld(ResourceManager resourceManager, IAssetRegistry? assetRegistry = null,
        RuntimeActorFactory? runtimeActorFactory = null)
        => InstantiateWorld(resourceManager, assetRegistry, runtimeActorFactory, isRuntimeWorld: true);

    /// <summary>创建编辑器 World；共享 Registry 资产，但不复制运行时材质、不注入 gameplay 行为。</summary>
    public World InstantiateEditorWorld(ResourceManager resourceManager, IAssetRegistry? assetRegistry = null,
        RuntimeActorFactory? runtimeActorFactory = null)
        => InstantiateWorld(resourceManager, assetRegistry, runtimeActorFactory, isRuntimeWorld: false);

    private World InstantiateWorld(ResourceManager resourceManager, IAssetRegistry? assetRegistry,
        RuntimeActorFactory? runtimeActorFactory, bool isRuntimeWorld)
    {
        ArgumentNullException.ThrowIfNull(resourceManager);
        var world = new World(resourceManager);
        runtimeActorFactory ??= new RuntimeActorFactory();
        var components = new Dictionary<Guid, SceneComponent>();
        var actorRecords = Actors.OrderBy(a => a.ActorGuid).ToArray();
        var runtimeMaterials = new Dictionary<Guid, Material>();

        try
        {
            foreach (var actorRecord in actorRecords)
            {
                var actor = runtimeActorFactory.CreateActor(actorRecord);
                foreach (var componentRecord in actorRecord.Components.OrderBy(c => c.ComponentGuid))
                {
                    var component = runtimeActorFactory.CreateComponent(componentRecord);

                    component.ComponentGuid = componentRecord.ComponentGuid;
                    ScenePropertySerializer.Restore(component, componentRecord.Properties, ResolveAsset);
                    actor.AddOwnedComponent(component);
                    if (component is SceneComponent scene)
                    {
                        scene.RelativeLocation = componentRecord.RelativeLocation;
                        scene.RelativeRotation = componentRecord.RelativeRotation;
                        scene.RelativeScale = componentRecord.RelativeScale;
                        foreach (var socket in componentRecord.Sockets)
                            scene.DefineSocket(socket.Key, socket.Value);
                        components.Add(component.ComponentGuid, scene);
                    }
                }
                world.AddActor(actor);
                if (actorRecord.RootComponentGuid is { } rootGuid && components.TryGetValue(rootGuid, out var root) && ReferenceEquals(root.Owner, actor))
                    actor.SetRootComponent(root);
            }

            foreach (var actorRecord in actorRecords)
            {
                foreach (var componentRecord in actorRecord.Components)
                {
                    if (componentRecord.ParentComponentGuid is not { } parentGuid)
                        continue;
                    if (!components.TryGetValue(componentRecord.ComponentGuid, out var child) || !components.TryGetValue(parentGuid, out var parent))
                        throw new InvalidDataException($"Component '{componentRecord.ComponentGuid}' references a missing parent '{parentGuid}'.");
                    child.AttachToComponent(parent, AttachmentTransformRules.KeepRelativeTransform, componentRecord.AttachSocketName);
                }
            }

            if (!isRuntimeWorld)
            {
                EditorWorldOutlinerData.For(world).RestorePersistentData(
                    EditorFolders.Select(folder => new EditorActorFolder(
                        folder.FolderGuid, folder.ParentFolderGuid, folder.Name)),
                    Actors.Select(actor => (actor.ActorGuid, actor.EditorFolderGuid)),
                    EditorLevels.Select(level => new EditorWorldLevel(level.LevelGuid, level.Name)),
                    DataLayers.Select(layer => new EditorWorldDataLayer(layer.DataLayerGuid, layer.Name)),
                    Actors.Select(actor => (actor.ActorGuid, actor.EditorLevelGuid,
                        (IReadOnlyList<Guid>)actor.EditorDataLayerGuids)),
                    UnloadedActors.Select(actor => new EditorUnloadedActorDescriptor(
                        actor.ActorGuid, actor.Label, actor.ActorType, actor.EditorLevelGuid,
                        actor.EditorDataLayerGuids)));
            }
            if (isRuntimeWorld)
                runtimeActorFactory.InitializeWorld(world, this);
            return world;
        }
        catch
        {
            world.Dispose();
            throw;
        }

        Material ResolveMaterial(Guid materialGuid)
        {
            if (!isRuntimeWorld)
            {
                if (assetRegistry == null || assetRegistry.Resolve(materialGuid) is not Material editorMaterial)
                    throw new InvalidDataException($"Material asset '{materialGuid}' could not be resolved as Material.");
                return editorMaterial;
            }
            if (runtimeMaterials.TryGetValue(materialGuid, out var existing))
                return existing;
            if (assetRegistry == null || assetRegistry.Resolve(materialGuid) is not Material source)
                throw new InvalidDataException($"Material asset '{materialGuid}' could not be resolved as Material.");
            var runtimeCopy = world.OwnResource(source.CreateRuntimeCopy());
            runtimeMaterials.Add(materialGuid, runtimeCopy);
            return runtimeCopy;
        }

        SceneResource ResolveAsset(Guid assetGuid, Type expectedType)
        {
            SceneResource resource = typeof(Material).IsAssignableFrom(expectedType)
                ? ResolveMaterial(assetGuid)
                : assetRegistry?.Resolve(assetGuid)
                    ?? throw new InvalidDataException($"Asset '{assetGuid}' is not registered.");
            if (!expectedType.IsInstanceOfType(resource))
                throw new InvalidDataException(
                    $"Asset '{assetGuid}' is {resource.GetType().Name}, expected {expectedType.Name}.");
            return resource;
        }
    }
}

public sealed class SceneActorDocument
{
    public Guid ActorGuid { get; init; }
    public string ActorType { get; init; } = typeof(Actor).AssemblyQualifiedName!;
    public string Name { get; init; } = string.Empty;
    public Guid? RootComponentGuid { get; init; }
    public Guid? EditorFolderGuid { get; set; }
    public Guid? EditorLevelGuid { get; set; }
    public List<Guid> EditorDataLayerGuids { get; } = [];
    public List<SceneComponentDocument> Components { get; } = [];
}

public sealed class SceneEditorLevelDocument
{
    public Guid LevelGuid { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class SceneDataLayerDocument
{
    public Guid DataLayerGuid { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class SceneUnloadedActorDocument
{
    public Guid ActorGuid { get; init; }
    public string Label { get; init; } = string.Empty;
    public string ActorType { get; init; } = string.Empty;
    public Guid? EditorLevelGuid { get; init; }
    public List<Guid> EditorDataLayerGuids { get; } = [];
}

public sealed class SceneEditorFolderDocument
{
    public Guid FolderGuid { get; init; }
    public Guid? ParentFolderGuid { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class SceneComponentDocument
{
    public Guid ComponentGuid { get; init; }
    public string ComponentType { get; init; } = string.Empty;
    public Guid? ParentComponentGuid { get; init; }
    public string? AttachSocketName { get; init; }
    public Vector3 RelativeLocation { get; init; }
    public Quaternion RelativeRotation { get; init; } = Quaternion.Identity;
    public Vector3 RelativeScale { get; init; } = Vector3.One;
    public Dictionary<string, ScenePropertyValue> Properties { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, Matrix4x4> Sockets { get; set; } = new(StringComparer.Ordinal);
}

internal static class SceneDocumentBinary
{
    private static readonly byte[] Magic = "SCNE"u8.ToArray();

    public static void Write(SceneDocument document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (document.FormatVersion != SceneDocument.CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported scene format version {document.FormatVersion}; supported version is {SceneDocument.CurrentFormatVersion}.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory != null)
            Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: false))
            {
                writer.Write(Magic);
                writer.Write(document.FormatVersion);
                writer.Write((byte)1); // AssetType.Scene
                writer.Write((byte)0); // Flags
                writer.Write(document.SceneGuid.ToByteArray());
                writer.Write(document.EditorFolders.Count);
                foreach (var folder in document.EditorFolders.OrderBy(folder => folder.FolderGuid))
                {
                    writer.Write(folder.FolderGuid.ToByteArray());
                    WriteNullableGuid(writer, folder.ParentFolderGuid);
                    WriteString(writer, folder.Name);
                }
                writer.Write(document.EditorLevels.Count);
                foreach (var level in document.EditorLevels.OrderBy(level => level.LevelGuid))
                {
                    writer.Write(level.LevelGuid.ToByteArray());
                    WriteString(writer, level.Name);
                }
                writer.Write(document.DataLayers.Count);
                foreach (var layer in document.DataLayers.OrderBy(layer => layer.DataLayerGuid))
                {
                    writer.Write(layer.DataLayerGuid.ToByteArray());
                    WriteString(writer, layer.Name);
                }
                writer.Write(document.UnloadedActors.Count);
                foreach (var actor in document.UnloadedActors.OrderBy(actor => actor.ActorGuid))
                {
                    writer.Write(actor.ActorGuid.ToByteArray());
                    WriteString(writer, actor.Label);
                    WriteString(writer, actor.ActorType);
                    WriteNullableGuid(writer, actor.EditorLevelGuid);
                    writer.Write(actor.EditorDataLayerGuids.Count);
                    foreach (var layerGuid in actor.EditorDataLayerGuids.OrderBy(value => value))
                        writer.Write(layerGuid.ToByteArray());
                }
                writer.Write(document.Actors.Count);

                foreach (var actor in document.Actors.OrderBy(a => a.ActorGuid))
                {
                    writer.Write(actor.ActorGuid.ToByteArray());
                    WriteString(writer, actor.ActorType);
                    WriteString(writer, actor.Name);
                    WriteNullableGuid(writer, actor.RootComponentGuid);
                    WriteNullableGuid(writer, actor.EditorFolderGuid);
                    WriteNullableGuid(writer, actor.EditorLevelGuid);
                    writer.Write(actor.EditorDataLayerGuids.Count);
                    foreach (var layerGuid in actor.EditorDataLayerGuids.OrderBy(value => value))
                        writer.Write(layerGuid.ToByteArray());
                    writer.Write(actor.Components.Count);
                    foreach (var component in actor.Components.OrderBy(c => c.ComponentGuid))
                    {
                        writer.Write(component.ComponentGuid.ToByteArray());
                        WriteString(writer, component.ComponentType);
                        WriteNullableGuid(writer, component.ParentComponentGuid);
                        WriteNullableString(writer, component.AttachSocketName);
                        WriteVector3(writer, component.RelativeLocation);
                        WriteQuaternion(writer, component.RelativeRotation);
                        WriteVector3(writer, component.RelativeScale);
                        writer.Write(component.Sockets.Count);
                        foreach (var socket in component.Sockets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                        {
                            writer.Write(socket.Key);
                            WriteMatrix4x4(writer, socket.Value);
                        }
                        writer.Write(component.Properties.Count);
                        foreach (var property in component.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                        {
                            WriteString(writer, property.Key);
                            writer.Write((byte)property.Value.Kind);
                            WritePropertyValue(writer, property.Value);
                        }
                    }
                }
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public static SceneDocument Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(stream);
    }

    public static SceneDocument Read(ReadOnlyMemory<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return Read(stream);
    }

    private static SceneDocument Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: false);

        var magic = reader.ReadBytes(Magic.Length);
        if (!magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Invalid scene file magic.");

        var version = reader.ReadUInt16();
        if (version is not 5 and not 6 && version != SceneDocument.CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported scene format version {version}; supported versions are 5, 6 and {SceneDocument.CurrentFormatVersion}.");

        if (reader.ReadByte() != 1)
            throw new InvalidDataException("The file is not a SceneDocument.");
        _ = reader.ReadByte(); // flags reserved for future versions

        var document = new SceneDocument
        {
            FormatVersion = SceneDocument.CurrentFormatVersion,
            SceneGuid = ReadGuid(reader),
        };

        if (version >= 6)
        {
            var folderCount = ReadCount(reader, "editor folder");
            for (var folderIndex = 0; folderIndex < folderCount; folderIndex++)
                document.EditorFolders.Add(new SceneEditorFolderDocument
                {
                    FolderGuid = ReadGuid(reader),
                    ParentFolderGuid = ReadNullableGuid(reader),
                    Name = ReadString(reader),
                });
        }

        if (version >= 7)
        {
            var levelCount = ReadCount(reader, "editor level");
            for (var levelIndex = 0; levelIndex < levelCount; levelIndex++)
                document.EditorLevels.Add(new SceneEditorLevelDocument
                {
                    LevelGuid = ReadGuid(reader),
                    Name = ReadString(reader),
                });
            var layerCount = ReadCount(reader, "data layer");
            for (var layerIndex = 0; layerIndex < layerCount; layerIndex++)
                document.DataLayers.Add(new SceneDataLayerDocument
                {
                    DataLayerGuid = ReadGuid(reader),
                    Name = ReadString(reader),
                });
            var unloadedActorCount = ReadCount(reader, "unloaded actor");
            for (var actorIndex = 0; actorIndex < unloadedActorCount; actorIndex++)
            {
                var actor = new SceneUnloadedActorDocument
                {
                    ActorGuid = ReadGuid(reader),
                    Label = ReadString(reader),
                    ActorType = ReadString(reader),
                    EditorLevelGuid = ReadNullableGuid(reader),
                };
                var actorLayerCount = ReadCount(reader, "unloaded actor data layer");
                for (var layerIndex = 0; layerIndex < actorLayerCount; layerIndex++)
                    actor.EditorDataLayerGuids.Add(ReadGuid(reader));
                document.UnloadedActors.Add(actor);
            }
        }

        var actorCount = ReadCount(reader, "actor");
        for (var actorIndex = 0; actorIndex < actorCount; actorIndex++)
        {
            var actor = new SceneActorDocument
            {
                ActorGuid = ReadGuid(reader),
                ActorType = ReadString(reader),
                Name = ReadString(reader),
                RootComponentGuid = ReadNullableGuid(reader),
                EditorFolderGuid = version >= 6 ? ReadNullableGuid(reader) : null,
                EditorLevelGuid = version >= 7 ? ReadNullableGuid(reader) : null,
            };
            if (version >= 7)
            {
                var actorLayerCount = ReadCount(reader, "actor data layer");
                for (var layerIndex = 0; layerIndex < actorLayerCount; layerIndex++)
                    actor.EditorDataLayerGuids.Add(ReadGuid(reader));
            }
            var componentCount = ReadCount(reader, "component");
            for (var componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                actor.Components.Add(new SceneComponentDocument
                {
                    ComponentGuid = ReadGuid(reader),
                    ComponentType = ReadString(reader),
                    ParentComponentGuid = ReadNullableGuid(reader),
                    AttachSocketName = ReadNullableString(reader),
                    RelativeLocation = ReadVector3(reader),
                    RelativeRotation = ReadQuaternion(reader),
                    RelativeScale = ReadVector3(reader),
                });
                var socketCount = ReadCount(reader, "socket");
                for (var socketIndex = 0; socketIndex < socketCount; socketIndex++)
                    actor.Components[^1].Sockets.Add(ReadString(reader), ReadMatrix4x4(reader));
                var propertyCount = ReadCount(reader, "property");
                for (var propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    var propertyName = ReadString(reader);
                    var kind = (ScenePropertyKind)reader.ReadByte();
                    if (!Enum.IsDefined(kind))
                        throw new InvalidDataException($"Unknown scene property kind {(byte)kind}.");
                    actor.Components[^1].Properties.Add(propertyName, ReadPropertyValue(reader, kind));
                }
            }
            document.Actors.Add(actor);
        }

        if (stream.Position != stream.Length)
            throw new InvalidDataException("Unexpected trailing data in scene file.");
        return document;
    }

    private static int ReadCount(BinaryReader reader, string kind)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > 1_000_000)
            throw new InvalidDataException($"Invalid {kind} count {count}.");
        return count;
    }

    private static void WriteString(BinaryWriter writer, string value) => writer.Write(value ?? string.Empty);
    private static string ReadString(BinaryReader reader) => reader.ReadString();
    private static void WriteNullableString(BinaryWriter writer, string? value)
    {
        writer.Write(value != null);
        if (value != null) writer.Write(value);
    }
    private static string? ReadNullableString(BinaryReader reader) => reader.ReadBoolean() ? reader.ReadString() : null;
    private static void WriteNullableGuid(BinaryWriter writer, Guid? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue) writer.Write(value.Value.ToByteArray());
    }
    private static Guid? ReadNullableGuid(BinaryReader reader) => reader.ReadBoolean() ? ReadGuid(reader) : null;
    private static Guid ReadGuid(BinaryReader reader) => new(ReadExactly(reader, 16));
    private static void WriteVector3(BinaryWriter writer, Vector3 value) { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); }
    private static Vector3 ReadVector3(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private static void WriteVector4(BinaryWriter writer, Vector4 value) { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); writer.Write(value.W); }
    private static Vector4 ReadVector4(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private static void WriteQuaternion(BinaryWriter writer, Quaternion value) { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); writer.Write(value.W); }
    private static Quaternion ReadQuaternion(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private static void WriteMatrix4x4(BinaryWriter writer, Matrix4x4 value)
    {
        writer.Write(value.M11); writer.Write(value.M12); writer.Write(value.M13); writer.Write(value.M14);
        writer.Write(value.M21); writer.Write(value.M22); writer.Write(value.M23); writer.Write(value.M24);
        writer.Write(value.M31); writer.Write(value.M32); writer.Write(value.M33); writer.Write(value.M34);
        writer.Write(value.M41); writer.Write(value.M42); writer.Write(value.M43); writer.Write(value.M44);
    }
    private static Matrix4x4 ReadMatrix4x4(BinaryReader reader) => new(
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    private static void WritePropertyValue(BinaryWriter writer, ScenePropertyValue property)
    {
        switch (property.Kind)
        {
            case ScenePropertyKind.Null: break;
            case ScenePropertyKind.Boolean: writer.Write(property.Get<bool>()); break;
            case ScenePropertyKind.Int64: writer.Write(property.Get<long>()); break;
            case ScenePropertyKind.UInt64: writer.Write(property.Get<ulong>()); break;
            case ScenePropertyKind.Single: writer.Write(property.Get<float>()); break;
            case ScenePropertyKind.Double: writer.Write(property.Get<double>()); break;
            case ScenePropertyKind.String: WriteString(writer, property.Get<string>()); break;
            case ScenePropertyKind.Guid:
            case ScenePropertyKind.AssetReference: writer.Write(property.Get<Guid>().ToByteArray()); break;
            case ScenePropertyKind.Vector2: WriteVector2(writer, property.Get<Vector2>()); break;
            case ScenePropertyKind.Vector3: WriteVector3(writer, property.Get<Vector3>()); break;
            case ScenePropertyKind.Vector4: WriteVector4(writer, property.Get<Vector4>()); break;
            case ScenePropertyKind.Quaternion: WriteQuaternion(writer, property.Get<Quaternion>()); break;
            case ScenePropertyKind.Matrix4x4: WriteMatrix4x4(writer, property.Get<Matrix4x4>()); break;
            default: throw new InvalidDataException($"Unknown scene property kind {(byte)property.Kind}.");
        }
    }

    private static ScenePropertyValue ReadPropertyValue(BinaryReader reader, ScenePropertyKind kind)
        => kind switch
        {
            ScenePropertyKind.Null => new(kind, null),
            ScenePropertyKind.Boolean => new(kind, reader.ReadBoolean()),
            ScenePropertyKind.Int64 => new(kind, reader.ReadInt64()),
            ScenePropertyKind.UInt64 => new(kind, reader.ReadUInt64()),
            ScenePropertyKind.Single => new(kind, reader.ReadSingle()),
            ScenePropertyKind.Double => new(kind, reader.ReadDouble()),
            ScenePropertyKind.String => new(kind, ReadString(reader)),
            ScenePropertyKind.Guid or ScenePropertyKind.AssetReference => new(kind, new Guid(ReadExactly(reader, 16))),
            ScenePropertyKind.Vector2 => new(kind, ReadVector2(reader)),
            ScenePropertyKind.Vector3 => new(kind, ReadVector3(reader)),
            ScenePropertyKind.Vector4 => new(kind, ReadVector4(reader)),
            ScenePropertyKind.Quaternion => new(kind, ReadQuaternion(reader)),
            ScenePropertyKind.Matrix4x4 => new(kind, ReadMatrix4x4(reader)),
            _ => throw new InvalidDataException($"Unknown scene property kind {(byte)kind}.")
        };

    private static void WriteVector2(BinaryWriter writer, Vector2 value) { writer.Write(value.X); writer.Write(value.Y); }
    private static Vector2 ReadVector2(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle());

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count)
            throw new InvalidDataException("Unexpected end of scene file.");
        return bytes;
    }
}
