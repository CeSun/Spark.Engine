using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>编辑器场景的稳定内存表示；它是保存和 RuntimeWorld 实例化的共同输入。</summary>
public sealed class SceneDocument
{
    public const ushort CurrentFormatVersion = 2;
    public Guid SceneGuid { get; set; } = Guid.NewGuid();
    public ushort FormatVersion { get; init; } = CurrentFormatVersion;
    public List<SceneActorDocument> Actors { get; } = [];

    public static SceneDocument Capture(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var document = new SceneDocument();
        foreach (var actor in world.Actors.OrderBy(a => a.ActorGuid))
        {
            var actorDocument = new SceneActorDocument
            {
                ActorGuid = actor.ActorGuid,
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
                    MeshAssetGuid = (component as StaticMeshComponent)?.Mesh?.AssetGuid,
                    MaterialAssetGuid = (component as StaticMeshComponent)?.Material?.AssetGuid,
                });
                if (scene != null)
                    actorDocument.Components[^1].Sockets = scene.Sockets.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            }

            document.Actors.Add(actorDocument);
        }

        return document;
    }

    public void Save(string path) => SceneDocumentBinary.Write(this, path);

    public static SceneDocument Load(string path) => SceneDocumentBinary.Read(path);

    /// <summary>
    /// 从文档创建全新的运行时 World。该过程只恢复 Actor/Component 类型、GUID、层级和变换，
    /// 不复用编辑器对象；资产实例化和自定义 Actor 工厂由后续扩展接入。
    /// </summary>
    public World InstantiateWorld(Spark.Engine.Resources.ResourceManager resourceManager)
    {
        ArgumentNullException.ThrowIfNull(resourceManager);
        var world = new World(resourceManager);
        var components = new Dictionary<Guid, SceneComponent>();
        var actorRecords = Actors.OrderBy(a => a.ActorGuid).ToArray();
        var actors = new Dictionary<Guid, Actor>();

        try
        {
            foreach (var actorRecord in actorRecords)
            {
                var actor = new Actor { ActorGuid = actorRecord.ActorGuid, Name = actorRecord.Name };
                actors.Add(actor.ActorGuid, actor);
                foreach (var componentRecord in actorRecord.Components.OrderBy(c => c.ComponentGuid))
                {
                    var type = Type.GetType(componentRecord.ComponentType, throwOnError: false);
                    if (type == null || !typeof(ActorComponent).IsAssignableFrom(type) || type.IsAbstract)
                        throw new InvalidDataException($"Cannot instantiate component type '{componentRecord.ComponentType}'.");
                    if (Activator.CreateInstance(type) is not ActorComponent component)
                        throw new InvalidDataException($"Component type '{componentRecord.ComponentType}' has no public parameterless constructor.");

                    component.ComponentGuid = componentRecord.ComponentGuid;
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

            return world;
        }
        catch
        {
            world.Dispose();
            throw;
        }
    }
}

public sealed class SceneActorDocument
{
    public Guid ActorGuid { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid? RootComponentGuid { get; init; }
    public List<SceneComponentDocument> Components { get; } = [];
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
    public Guid? MeshAssetGuid { get; init; }
    public Guid? MaterialAssetGuid { get; init; }
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
                writer.Write(document.Actors.Count);

                foreach (var actor in document.Actors.OrderBy(a => a.ActorGuid))
                {
                    writer.Write(actor.ActorGuid.ToByteArray());
                    WriteString(writer, actor.Name);
                    WriteNullableGuid(writer, actor.RootComponentGuid);
                    writer.Write(actor.Components.Count);
                    foreach (var component in actor.Components.OrderBy(c => c.ComponentGuid))
                    {
                        writer.Write(component.ComponentGuid.ToByteArray());
                        WriteString(writer, component.ComponentType);
                        WriteNullableGuid(writer, component.ParentComponentGuid);
                        WriteNullableString(writer, component.AttachSocketName);
                        WriteNullableGuid(writer, component.MeshAssetGuid);
                        WriteNullableGuid(writer, component.MaterialAssetGuid);
                        WriteVector3(writer, component.RelativeLocation);
                        WriteQuaternion(writer, component.RelativeRotation);
                        WriteVector3(writer, component.RelativeScale);
                        writer.Write(component.Sockets.Count);
                        foreach (var socket in component.Sockets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                        {
                            writer.Write(socket.Key);
                            WriteMatrix4x4(writer, socket.Value);
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
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: false);

        var magic = reader.ReadBytes(Magic.Length);
        if (!magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Invalid scene file magic.");

        var version = reader.ReadUInt16();
        if (version != SceneDocument.CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported scene format version {version}; supported version is {SceneDocument.CurrentFormatVersion}.");

        if (reader.ReadByte() != 1)
            throw new InvalidDataException("The file is not a SceneDocument.");
        _ = reader.ReadByte(); // flags reserved for future versions

        var document = new SceneDocument
        {
            FormatVersion = version,
            SceneGuid = new Guid(reader.ReadBytes(16)),
        };

        var actorCount = ReadCount(reader, "actor");
        for (var actorIndex = 0; actorIndex < actorCount; actorIndex++)
        {
            var actor = new SceneActorDocument
            {
                ActorGuid = new Guid(reader.ReadBytes(16)),
                Name = ReadString(reader),
                RootComponentGuid = ReadNullableGuid(reader),
            };
            var componentCount = ReadCount(reader, "component");
            for (var componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                actor.Components.Add(new SceneComponentDocument
                {
                    ComponentGuid = new Guid(reader.ReadBytes(16)),
                    ComponentType = ReadString(reader),
                    ParentComponentGuid = ReadNullableGuid(reader),
                    AttachSocketName = ReadNullableString(reader),
                    MeshAssetGuid = ReadNullableGuid(reader),
                    MaterialAssetGuid = ReadNullableGuid(reader),
                    RelativeLocation = ReadVector3(reader),
                    RelativeRotation = ReadQuaternion(reader),
                    RelativeScale = ReadVector3(reader),
                });
                var socketCount = ReadCount(reader, "socket");
                for (var socketIndex = 0; socketIndex < socketCount; socketIndex++)
                    actor.Components[^1].Sockets.Add(ReadString(reader), ReadMatrix4x4(reader));
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
    private static Guid? ReadNullableGuid(BinaryReader reader) => reader.ReadBoolean() ? new Guid(reader.ReadBytes(16)) : null;
    private static void WriteVector3(BinaryWriter writer, Vector3 value) { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); }
    private static Vector3 ReadVector3(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
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
}
