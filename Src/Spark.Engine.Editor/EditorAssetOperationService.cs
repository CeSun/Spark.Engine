using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

public enum EditorAssetReferenceKind : byte
{
    Asset,
    Scene,
}

/// <summary>阻止删除的资产引用；Depth=1 为直接引用，更大的值为传递引用。</summary>
public sealed record EditorAssetReference(
    EditorAssetReferenceKind Kind,
    Guid TargetAssetGuid,
    Guid? ReferrerAssetGuid,
    string Referrer,
    int Depth);

public sealed class EditorAssetReferencedException : InvalidOperationException
{
    public EditorAssetReferencedException(IReadOnlyList<EditorAssetReference> references)
        : base(CreateMessage(references))
    {
        References = references;
    }

    public IReadOnlyList<EditorAssetReference> References { get; }

    private static string CreateMessage(IReadOnlyList<EditorAssetReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        return references.Count == 0
            ? "The asset is referenced and cannot be deleted."
            : $"Delete blocked by {references.Count} reference(s): " +
              string.Join(", ", references.Take(3).Select(reference => reference.Referrer)) +
              (references.Count > 3 ? ", ..." : string.Empty);
    }
}

public sealed record EditorAssetDeleteResult(
    IReadOnlyList<Guid> RemovedAssetGuids,
    string RecoveryPath);

/// <summary>
/// Content 目录的事务式写操作边界。调用方只提交相对目录或资产 GUID；
/// 服务负责路径约束、GUID 语义、引用保护、磁盘提交和 Registry 同步。
/// </summary>
public sealed class EditorAssetOperationService
{
    private readonly EditorProject _project;
    private readonly AssetRegistry _registry;
    private readonly string _contentRoot;
    private readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public EditorAssetOperationService(EditorProject project, AssetRegistry registry)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _contentRoot = Path.GetFullPath(project.ContentDirectory);
        Directory.CreateDirectory(_contentRoot);
    }

    public string CreateDirectory(string? parentDirectory, string name)
    {
        var parent = ResolveDirectory(parentDirectory, mustExist: true);
        var segment = ValidateName(name, "folder");
        var target = CombineChild(parent, segment);
        EnsureDoesNotExist(target);
        Directory.CreateDirectory(target);
        return ToContentPath(target);
    }

    /// <summary>在 Content 目录创建具有稳定 GUID 的空白 Material 资产。</summary>
    public AssetRecord CreateMaterial(string? directory, string name)
    {
        var parent = ResolveDirectory(directory, mustExist: true);
        var target = CombineChild(parent, ValidateAssetFileName(name));
        EnsureDoesNotExist(target);

        var assetGuid = Guid.NewGuid();
        var staging = target + ".tmp-" + Guid.NewGuid().ToString("N");
        var committed = false;
        using var material = new Material { AssetGuid = assetGuid };
        try
        {
            AssetFileCodec.Save(material, staging);
            var validation = AssetFileCodec.ReadMetadata(staging);
            if (validation.AssetGuid != assetGuid ||
                !string.Equals(validation.AssetType, EngineAssetType.Material.ToString(),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("The created Material asset could not be validated.");
            }

            File.Move(staging, target);
            committed = true;
            return _registry.RegisterAssetFile(target, _contentRoot);
        }
        catch
        {
            _registry.Remove(assetGuid, out _);
            if (committed && File.Exists(target))
                File.Delete(target);
            throw;
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }
    }

    public string RenameDirectory(string directory, string newName)
    {
        var source = ResolveDirectory(directory, mustExist: true, allowRoot: false);
        var target = CombineChild(Path.GetDirectoryName(source)!, ValidateName(newName, "folder"));
        return MoveDirectoryCore(source, target);
    }

    public string MoveDirectory(string directory, string destinationDirectory)
    {
        var source = ResolveDirectory(directory, mustExist: true, allowRoot: false);
        var destination = ResolveDirectory(destinationDirectory, mustExist: true);
        if (IsWithin(destination, source))
            throw new InvalidOperationException("A folder cannot be moved into itself or one of its descendants.");
        return MoveDirectoryCore(source, CombineChild(destination, Path.GetFileName(source)));
    }

    public string CopyDirectory(string directory, string destinationDirectory, string? copyName = null)
    {
        var source = ResolveDirectory(directory, mustExist: true, allowRoot: false);
        var destination = ResolveDirectory(destinationDirectory, mustExist: true);
        if (IsWithin(destination, source))
            throw new InvalidOperationException("A folder cannot be copied into itself or one of its descendants.");
        var target = CombineChild(destination, ValidateName(copyName ?? Path.GetFileName(source) + " Copy", "folder"));
        EnsureDoesNotExist(target);

        var staging = CombineChild(destination, ".spark-copy-" + Guid.NewGuid().ToString("N"));
        var registered = new List<Guid>();
        try
        {
            CopyDirectoryToStaging(source, staging);
            Directory.Move(staging, target);
            foreach (var assetPath in Directory.EnumerateFiles(target, "*.asset", SearchOption.AllDirectories))
                registered.Add(_registry.RegisterAssetFile(assetPath, _contentRoot).AssetGuid);
            return ToContentPath(target);
        }
        catch
        {
            foreach (var assetGuid in registered)
                _registry.Remove(assetGuid, out _);
            if (Directory.Exists(target))
                Directory.Delete(target, recursive: true);
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    public AssetRecord RenameAsset(Guid assetGuid, string newName)
    {
        var record = GetPersistentRecord(assetGuid);
        var source = GetAssetPath(record);
        var fileName = ValidateAssetFileName(newName);
        return MoveAssetCore(record, source, CombineChild(Path.GetDirectoryName(source)!, fileName));
    }

    public AssetRecord MoveAsset(Guid assetGuid, string destinationDirectory)
    {
        var record = GetPersistentRecord(assetGuid);
        var source = GetAssetPath(record);
        var destination = ResolveDirectory(destinationDirectory, mustExist: true);
        return MoveAssetCore(record, source, CombineChild(destination, Path.GetFileName(source)));
    }

    public AssetRecord CopyAsset(Guid assetGuid, string destinationDirectory, string? copyName = null)
    {
        var sourceRecord = GetPersistentRecord(assetGuid);
        var source = GetAssetPath(sourceRecord);
        var destination = ResolveDirectory(destinationDirectory, mustExist: true);
        var defaultName = Path.GetFileNameWithoutExtension(source) + " Copy.asset";
        var target = CombineChild(destination, ValidateAssetFileName(copyName ?? defaultName));
        EnsureDoesNotExist(target);

        var data = AssetFileCodec.ReadData(source);
        var copyGuid = Guid.NewGuid();
        var staging = target + ".tmp-" + Guid.NewGuid().ToString("N");
        var committed = false;
        try
        {
            AssetFileCodec.Save(data with { AssetGuid = copyGuid }, staging);
            var validation = AssetFileCodec.ReadMetadata(staging);
            if (validation.AssetGuid != copyGuid)
                throw new InvalidDataException("The copied asset identity could not be validated.");
            File.Move(staging, target);
            committed = true;
            return _registry.RegisterAssetFile(target, _contentRoot);
        }
        catch
        {
            _registry.Remove(copyGuid, out _);
            if (committed && File.Exists(target))
                File.Delete(target);
            throw;
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }
    }

    public IReadOnlyList<EditorAssetReference> FindReferences(Guid assetGuid, SceneDocument? currentScene = null)
        => FindReferences(new[] { assetGuid }, currentScene);

    public EditorAssetDeleteResult DeleteAsset(Guid assetGuid, SceneDocument? currentScene = null)
    {
        var record = GetPersistentRecord(assetGuid);
        var references = FindReferences(assetGuid, currentScene);
        if (references.Count > 0)
            throw new EditorAssetReferencedException(references);

        var source = GetAssetPath(record);
        EnsureWritableFile(source);
        var recoveryPath = CreateRecoveryPath(ToContentPath(source));
        Directory.CreateDirectory(Path.GetDirectoryName(recoveryPath)!);
        File.Move(source, recoveryPath);
        try
        {
            if (!_registry.Remove(assetGuid, out _))
                throw new InvalidOperationException($"Asset '{assetGuid}' disappeared from the registry during delete.");
            return new EditorAssetDeleteResult(new[] { assetGuid }, recoveryPath);
        }
        catch
        {
            File.Move(recoveryPath, source);
            throw;
        }
    }

    public EditorAssetDeleteResult DeleteDirectory(string directory, SceneDocument? currentScene = null)
    {
        var source = ResolveDirectory(directory, mustExist: true, allowRoot: false);
        EnsureWritableTree(source);
        var records = GetRecordsUnder(source);
        var targets = records.Select(record => record.AssetGuid).ToHashSet();
        var references = FindReferences(targets, currentScene);
        if (references.Count > 0)
            throw new EditorAssetReferencedException(references);

        var recoveryPath = CreateRecoveryPath(ToContentPath(source));
        Directory.CreateDirectory(Path.GetDirectoryName(recoveryPath)!);
        Directory.Move(source, recoveryPath);
        var removed = new List<AssetRecord>();
        try
        {
            foreach (var record in records)
            {
                if (!_registry.Remove(record.AssetGuid, out var removedRecord) || removedRecord == null)
                    throw new InvalidOperationException(
                        $"Asset '{record.AssetGuid}' disappeared from the registry during delete.");
                removed.Add(removedRecord);
            }
            return new EditorAssetDeleteResult(targets.OrderBy(guid => guid).ToArray(), recoveryPath);
        }
        catch
        {
            Directory.Move(recoveryPath, source);
            foreach (var record in removed)
                _registry.RegisterMetadata(record);
            throw;
        }
    }

    private IReadOnlyList<EditorAssetReference> FindReferences(
        IEnumerable<Guid> targetAssetGuids,
        SceneDocument? currentScene)
    {
        var targets = targetAssetGuids.Where(guid => guid != Guid.Empty).Distinct().ToArray();
        var records = _registry.Records.ToArray();
        var results = new List<EditorAssetReference>();

        foreach (var target in targets)
        {
            var depths = new Dictionary<Guid, int> { [target] = 0 };
            var pending = new Queue<Guid>();
            pending.Enqueue(target);
            while (pending.TryDequeue(out var dependency))
            {
                var dependencyDepth = depths[dependency];
                foreach (var referrer in records.Where(record => record.Dependencies.Contains(dependency)))
                {
                    if (depths.ContainsKey(referrer.AssetGuid))
                        continue;
                    var depth = dependencyDepth + 1;
                    depths.Add(referrer.AssetGuid, depth);
                    pending.Enqueue(referrer.AssetGuid);
                    if (!targets.Contains(referrer.AssetGuid))
                    {
                        results.Add(new EditorAssetReference(
                            EditorAssetReferenceKind.Asset,
                            target,
                            referrer.AssetGuid,
                            EditorContentBrowserModel.GetDisplayName(referrer),
                            depth));
                    }
                }
            }

            if (currentScene != null)
            {
                foreach (var sceneReference in EnumerateSceneReferences(currentScene))
                {
                    if (!depths.TryGetValue(sceneReference.AssetGuid, out var assetDepth))
                        continue;
                    results.Add(new EditorAssetReference(
                        EditorAssetReferenceKind.Scene,
                        target,
                        null,
                        $"Scene: {sceneReference.ActorName}/{sceneReference.ComponentType}.{sceneReference.PropertyName}",
                        assetDepth + 1));
                }
            }
        }

        return results
            .Distinct()
            .OrderBy(reference => reference.Depth)
            .ThenBy(reference => reference.Referrer, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.TargetAssetGuid)
            .ToArray();
    }

    private string MoveDirectoryCore(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.Ordinal))
            return ToContentPath(source);
        EnsureDoesNotExist(target, allowCaseOnlySource: source);
        EnsureWritableTree(source);
        var records = GetRecordsUnder(source)
            .Select(record => (Record: record, OldPath: GetAssetPath(record)))
            .ToArray();
        MoveDirectoryTransactional(source, target);
        var relocated = new List<(AssetRecord Record, string OldPath)>();
        try
        {
            foreach (var item in records)
            {
                var relative = Path.GetRelativePath(source, item.OldPath);
                var newPath = Path.Combine(target, relative);
                _registry.Relocate(item.Record.AssetGuid, newPath, ToContentPath(newPath));
                relocated.Add(item);
            }
            return ToContentPath(target);
        }
        catch
        {
            MoveDirectoryTransactional(target, source);
            foreach (var item in relocated)
                _registry.Relocate(item.Record.AssetGuid, item.OldPath, ToContentPath(item.OldPath));
            throw;
        }
    }

    private AssetRecord MoveAssetCore(AssetRecord record, string source, string target)
    {
        if (string.Equals(source, target, StringComparison.Ordinal))
            return record;
        EnsureDoesNotExist(target, allowCaseOnlySource: source);
        EnsureWritableFile(source);
        MoveFileTransactional(source, target);
        try
        {
            _registry.Relocate(record.AssetGuid, target, ToContentPath(target));
            return record;
        }
        catch
        {
            MoveFileTransactional(target, source);
            throw;
        }
    }

    private void CopyDirectoryToStaging(string source, string staging)
    {
        Directory.CreateDirectory(staging);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(staging, Path.GetRelativePath(source, directory)));

        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToArray();
        var assetData = files
            .Where(file => string.Equals(Path.GetExtension(file), ".asset", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(file => file, AssetFileCodec.ReadData, StringComparer.OrdinalIgnoreCase);
        var duplicateGuid = assetData.Values.GroupBy(data => data.AssetGuid).FirstOrDefault(group => group.Count() > 1);
        if (duplicateGuid != null)
            throw new InvalidDataException(
                $"Folder copy found duplicate AssetGuid '{duplicateGuid.Key}' in the source tree.");
        var guidMap = assetData.Values.ToDictionary(data => data.AssetGuid, _ => Guid.NewGuid());

        foreach (var file in files)
        {
            var target = Path.Combine(staging, Path.GetRelativePath(source, file));
            if (assetData.TryGetValue(file, out var data))
            {
                AssetFileCodec.Save(AssetFileCodec.RemapAssetGuids(data, guidMap), target);
            }
            else
            {
                File.Copy(file, target);
            }
        }
    }

    private IReadOnlyList<AssetRecord> GetRecordsUnder(string directory)
        => _registry.Records
            .Where(record =>
            {
                try { return IsWithin(GetAssetPath(record), directory); }
                catch (InvalidOperationException) { return false; }
            })
            .OrderBy(record => record.AssetGuid)
            .ToArray();

    private AssetRecord GetPersistentRecord(Guid assetGuid)
    {
        if (assetGuid == Guid.Empty)
            throw new ArgumentException("AssetGuid cannot be empty.", nameof(assetGuid));
        return _registry.Records.FirstOrDefault(record => record.AssetGuid == assetGuid && record.IsPersistent)
            ?? throw new KeyNotFoundException($"Persistent asset '{assetGuid}' is not registered.");
    }

    private string GetAssetPath(AssetRecord record)
    {
        var candidate = record.CookedPath ?? record.LoaderSourcePath;
        if (string.IsNullOrWhiteSpace(candidate) && !string.IsNullOrWhiteSpace(record.ContentPath))
            candidate = Path.Combine(_contentRoot, record.ContentPath.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(candidate))
            throw new InvalidOperationException($"Asset '{record.AssetGuid}' has no Content file path.");
        var path = Path.GetFullPath(candidate);
        EnsureWithinContent(path);
        EnsureNoReparsePoints(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("The asset file was not found.", path);
        return path;
    }

    private string ResolveDirectory(string? relativeDirectory, bool mustExist, bool allowRoot = true)
    {
        var relative = (relativeDirectory ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathFullyQualified(relative) || relative.Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "." or ".."))
            throw new InvalidDataException("The Content directory path is invalid.");
        var path = Path.GetFullPath(Path.Combine(_contentRoot, relative));
        EnsureWithinContent(path, allowRoot);
        EnsureNoReparsePoints(path);
        if (mustExist && !Directory.Exists(path))
            throw new DirectoryNotFoundException(path);
        return path;
    }

    private string CombineChild(string parent, string name)
    {
        var path = Path.GetFullPath(Path.Combine(parent, name));
        EnsureWithinContent(path);
        return path;
    }

    private void EnsureWithinContent(string path, bool allowRoot = false)
    {
        var fullPath = Path.GetFullPath(path);
        if (allowRoot && string.Equals(fullPath, _contentRoot, _pathComparison))
            return;
        var prefix = _contentRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                     Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, _pathComparison))
            throw new InvalidDataException("The path is outside the project Content directory.");
    }

    private bool IsWithin(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory);
        return string.Equals(fullPath, fullDirectory, _pathComparison) ||
               fullPath.StartsWith(fullDirectory.TrimEnd(
                   Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                   Path.DirectorySeparatorChar, _pathComparison);
    }

    private string ToContentPath(string path)
    {
        EnsureWithinContent(path);
        return Path.GetRelativePath(_contentRoot, path).Replace('\\', '/');
    }

    private string CreateRecoveryPath(string contentPath)
    {
        var recoveryRoot = Path.Combine(_project.SavedDirectory, "Trash", "Content",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N"));
        return Path.Combine(recoveryRoot, contentPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ValidateName(string name, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (trimmed is "." or ".." || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            trimmed.Contains(Path.DirectorySeparatorChar) || trimmed.Contains(Path.AltDirectorySeparatorChar) ||
            (OperatingSystem.IsWindows() && (trimmed.EndsWith('.') || trimmed.EndsWith(' '))))
            throw new InvalidDataException($"The {kind} name '{name}' is invalid.");
        return trimmed;
    }

    private static string ValidateAssetFileName(string name)
    {
        var fileName = ValidateName(name, "asset");
        if (!fileName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            fileName += ".asset";
        return fileName;
    }

    private void EnsureDoesNotExist(string path, string? allowCaseOnlySource = null)
    {
        if (allowCaseOnlySource != null && string.Equals(path, allowCaseOnlySource, _pathComparison))
            return;
        if (File.Exists(path) || Directory.Exists(path))
            throw new IOException($"Content path '{ToContentPath(path)}' already exists.");
    }

    private static void EnsureWritableTree(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            EnsureWritableFile(file);
    }

    private static void EnsureWritableFile(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
            throw new UnauthorizedAccessException($"Content file '{path}' is read-only.");
    }

    private void EnsureNoReparsePoints(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Content path '{current}' uses a symbolic link or junction.");
            if (string.Equals(current, _contentRoot, _pathComparison))
                return;
        }
        throw new InvalidDataException("The Content path could not be validated.");
    }

    private static void MoveFileTransactional(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(source, target, StringComparison.Ordinal))
        {
            var temporary = source + ".rename-" + Guid.NewGuid().ToString("N");
            File.Move(source, temporary);
            try { File.Move(temporary, target); }
            catch { File.Move(temporary, source); throw; }
            return;
        }
        File.Move(source, target);
    }

    private static void MoveDirectoryTransactional(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(source, target, StringComparison.Ordinal))
        {
            var temporary = source + ".rename-" + Guid.NewGuid().ToString("N");
            Directory.Move(source, temporary);
            try { Directory.Move(temporary, target); }
            catch { Directory.Move(temporary, source); throw; }
            return;
        }
        Directory.Move(source, target);
    }

    private static IEnumerable<(Guid AssetGuid, string ActorName, string ComponentType, string PropertyName)>
        EnumerateSceneReferences(SceneDocument document)
    {
        foreach (var actor in document.Actors)
        foreach (var component in actor.Components)
        foreach (var property in component.Properties)
        {
            if (property.Value.Kind == ScenePropertyKind.AssetReference &&
                property.Value.Get<Guid>() is var assetGuid && assetGuid != Guid.Empty)
                yield return (assetGuid, actor.Name, GetShortTypeName(component.ComponentType), property.Key);
        }
    }

    private static string GetShortTypeName(string assemblyQualifiedName)
    {
        var name = assemblyQualifiedName.Split(',', 2)[0];
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[(dot + 1)..] : name;
    }
}
