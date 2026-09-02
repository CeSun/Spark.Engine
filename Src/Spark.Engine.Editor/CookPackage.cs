using System.Text;

namespace Spark.Engine.Editor;

/// <summary>Cook 目标平台；新增平台只需实现 <see cref="ICookBackend"/>。</summary>
public enum CookTargetPlatform : byte
{
    Windows = 1,
}

public sealed class CookedAsset
{
    public Guid AssetGuid { get; init; }
    public byte AssetType { get; init; }
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public IReadOnlyList<Guid> Dependencies { get; init; } = Array.Empty<Guid>();
}

public sealed class CookPackage
{
    public const ushort CurrentFormatVersion = 1;
    public Guid PackageGuid { get; init; } = Guid.NewGuid();
    public CookTargetPlatform TargetPlatform { get; init; }
    public IReadOnlyList<CookedAsset> Assets { get; init; } = Array.Empty<CookedAsset>();
}

public interface ICookBackend
{
    CookTargetPlatform TargetPlatform { get; }
    void Cook(IEnumerable<CookedAsset> assets, string outputPath);
}

/// <summary>
/// Windows-first Cook 后端。包是版本化二进制，写入采用临时文件替换，失败不会留下半包。
/// 当前不做增量 Cook；每次调用都会按 AssetGuid 确定性重建完整包。
/// </summary>
public sealed class WindowsCookBackend : ICookBackend
{
    private static readonly byte[] Magic = "PAK0"u8.ToArray();
    public CookTargetPlatform TargetPlatform => CookTargetPlatform.Windows;

    public void Cook(IEnumerable<CookedAsset> assets, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var package = new CookPackage
        {
            TargetPlatform = TargetPlatform,
            Assets = NormalizeAssets(assets),
        };
        Write(package, outputPath);
    }

    public static CookPackage Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        var magic = reader.ReadBytes(Magic.Length);
        if (!magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Invalid cooked package magic.");
        var version = reader.ReadUInt16();
        if (version != CookPackage.CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported cooked package version {version}; supported version is {CookPackage.CurrentFormatVersion}.");
        var platform = (CookTargetPlatform)reader.ReadByte();
        if (!Enum.IsDefined(platform))
            throw new InvalidDataException($"Unknown cooked package target platform {(byte)platform}.");
        _ = reader.ReadByte(); // flags reserved
        var packageGuid = new Guid(ReadExactly(reader, 16));
        var assetCount = ReadCount(reader, "asset");
        var assets = new List<CookedAsset>(assetCount);
        var seen = new HashSet<Guid>();
        for (var i = 0; i < assetCount; i++)
        {
            var assetGuid = new Guid(ReadExactly(reader, 16));
            if (!seen.Add(assetGuid)) throw new InvalidDataException($"Duplicate cooked asset {assetGuid}.");
            var assetType = reader.ReadByte();
            var dependencyCount = ReadCount(reader, "dependency");
            var dependencies = new List<Guid>(dependencyCount);
            for (var d = 0; d < dependencyCount; d++) dependencies.Add(new Guid(ReadExactly(reader, 16)));
            var payloadLength = reader.ReadInt64();
            if (payloadLength < 0 || payloadLength > int.MaxValue || payloadLength > stream.Length - stream.Position)
                throw new InvalidDataException("Invalid cooked asset payload length.");
            assets.Add(new CookedAsset
            {
                AssetGuid = assetGuid,
                AssetType = assetType,
                Dependencies = dependencies,
                Payload = reader.ReadBytes((int)payloadLength),
            });
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected trailing data in cooked package.");
        return new CookPackage { PackageGuid = packageGuid, TargetPlatform = platform, Assets = assets };
    }

    private static void Write(CookPackage package, string outputPath)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null) Directory.CreateDirectory(directory);
        var tempPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false))
            {
                writer.Write(Magic);
                writer.Write(CookPackage.CurrentFormatVersion);
                writer.Write((byte)package.TargetPlatform);
                writer.Write((byte)0);
                writer.Write(package.PackageGuid.ToByteArray());
                writer.Write(package.Assets.Count);
                foreach (var asset in package.Assets)
                {
                    writer.Write(asset.AssetGuid.ToByteArray());
                    writer.Write(asset.AssetType);
                    writer.Write(asset.Dependencies.Count);
                    foreach (var dependency in asset.Dependencies.OrderBy(guid => guid)) writer.Write(dependency.ToByteArray());
                    writer.Write((long)asset.Payload.Length);
                    writer.Write(asset.Payload);
                }
            }
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static IReadOnlyList<CookedAsset> NormalizeAssets(IEnumerable<CookedAsset> source)
    {
        var assets = source.ToArray();
        if (assets.Length > 1_000_000) throw new InvalidDataException("Too many assets for a cooked package.");
        var seen = new HashSet<Guid>();
        foreach (var asset in assets)
        {
            if (asset.AssetGuid == Guid.Empty) throw new InvalidDataException("Cooked assets require a non-empty AssetGuid.");
            if (!seen.Add(asset.AssetGuid)) throw new InvalidDataException($"Duplicate cooked asset {asset.AssetGuid}.");
            ArgumentNullException.ThrowIfNull(asset.Payload);
            ArgumentNullException.ThrowIfNull(asset.Dependencies);
        }
        return assets.OrderBy(asset => asset.AssetGuid).ToArray();
    }

    private static int ReadCount(BinaryReader reader, string kind)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > 1_000_000) throw new InvalidDataException($"Invalid {kind} count {count}.");
        return count;
    }

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count) throw new InvalidDataException("Unexpected end of cooked package.");
        return bytes;
    }
}
