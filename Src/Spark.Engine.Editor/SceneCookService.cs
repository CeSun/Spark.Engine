using Spark.Engine.Resources;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

public sealed record SceneCookResult(Guid SceneGuid, int AssetCount, string OutputPath);

/// <summary>从 SceneDocument 的资产引用构建传递依赖闭包，并交给目标平台 Cook 后端。</summary>
public sealed class SceneCookService
{
    public const byte SceneAssetType = byte.MaxValue;
    private readonly ICookBackend _backend;

    public SceneCookService(ICookBackend? backend = null)
    {
        _backend = backend ?? new WindowsCookBackend();
    }

    public SceneCookResult CookScene(string scenePath, IAssetRegistry assetRegistry, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        ArgumentNullException.ThrowIfNull(assetRegistry);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var fullScenePath = Path.GetFullPath(scenePath);
        var document = SceneDocument.Load(fullScenePath);
        if (document.SceneGuid == Guid.Empty)
            throw new InvalidDataException("Cooked scenes require a non-empty SceneGuid.");

        var directDependencies = document.Actors
            .SelectMany(actor => actor.Components)
            .SelectMany(component => component.Properties.Values)
            .Where(property => property.Kind == ScenePropertyKind.AssetReference)
            .Select(property => property.Get<Guid>())
            .Where(guid => guid != Guid.Empty)
            .Distinct()
            .OrderBy(guid => guid)
            .ToArray();
        var cookedAssets = new List<CookedAsset>();
        var pending = new Queue<Guid>(directDependencies);
        var visited = new HashSet<Guid>();
        while (pending.TryDequeue(out var assetGuid))
        {
            if (!visited.Add(assetGuid))
                continue;
            var registeredDependencies = assetRegistry.Records
                .FirstOrDefault(record => record.AssetGuid == assetGuid)?.Dependencies;
            var data = AssetFileCodec.Encode(
                assetRegistry.Resolve(assetGuid), registeredDependencies);
            if (data.AssetGuid != assetGuid)
                throw new InvalidDataException(
                    $"Asset registry resolved '{assetGuid}' as resource '{data.AssetGuid}'.");
            cookedAssets.Add(new CookedAsset
            {
                AssetGuid = data.AssetGuid,
                AssetType = (byte)data.AssetType,
                Dependencies = data.Dependencies,
                Payload = data.Payload,
            });
            foreach (var dependency in data.Dependencies)
                pending.Enqueue(dependency);
        }

        cookedAssets.Add(new CookedAsset
        {
            AssetGuid = document.SceneGuid,
            AssetType = SceneAssetType,
            Dependencies = directDependencies,
            Payload = File.ReadAllBytes(fullScenePath),
        });
        var fullOutputPath = Path.GetFullPath(outputPath);
        _backend.Cook(cookedAssets, fullOutputPath);
        return new SceneCookResult(document.SceneGuid, cookedAssets.Count, fullOutputPath);
    }
}

/// <summary>从 Cook 包解析资产依赖并创建独立 RuntimeWorld。</summary>
public static class CookedPackageRuntimeLoader
{
    public static World LoadWorld(
        string packagePath,
        Guid sceneGuid,
        ResourceManager resourceManager,
        RuntimeActorFactory? runtimeActorFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(resourceManager);
        var package = WindowsCookBackend.Load(packagePath);
        if (package.TargetPlatform != CookTargetPlatform.Windows)
            throw new InvalidDataException(
                $"Package target '{package.TargetPlatform}' is not supported by the Windows runtime loader.");
        var assets = package.Assets.ToDictionary(asset => asset.AssetGuid);
        if (!assets.TryGetValue(sceneGuid, out var scene) || scene.AssetType != SceneCookService.SceneAssetType)
            throw new InvalidDataException($"Cooked scene '{sceneGuid}' was not found in the package.");

        var registry = new PackageAssetRegistry(assets);
        World? world = null;
        try
        {
            foreach (var dependency in scene.Dependencies)
                registry.Resolve(dependency);
            var document = SceneDocument.Deserialize(scene.Payload);
            if (document.SceneGuid != sceneGuid)
                throw new InvalidDataException(
                    $"Cooked scene payload '{document.SceneGuid}' does not match package entry '{sceneGuid}'.");
            world = document.InstantiateWorld(resourceManager, registry, runtimeActorFactory);
            foreach (var resource in registry.LoadedResources)
                world.OwnResource(resource);
            return world;
        }
        catch
        {
            world?.Dispose();
            registry.DisposeLoadedResources();
            throw;
        }
    }

    private sealed class PackageAssetRegistry : IAssetRegistry
    {
        private readonly IReadOnlyDictionary<Guid, CookedAsset> _assets;
        private readonly Dictionary<Guid, SceneResource> _loaded = [];
        private readonly HashSet<Guid> _loading = [];

        public PackageAssetRegistry(IReadOnlyDictionary<Guid, CookedAsset> assets)
        {
            _assets = assets;
            Records = assets.Values
                .Where(asset => asset.AssetType != SceneCookService.SceneAssetType)
                .Select(asset => new AssetRecord
                {
                    AssetGuid = asset.AssetGuid,
                    AssetType = Enum.IsDefined((EngineAssetType)asset.AssetType)
                        ? ((EngineAssetType)asset.AssetType).ToString()
                        : asset.AssetType.ToString(),
                    Dependencies = asset.Dependencies,
                    ImportStatus = AssetImportStatus.Unknown,
                })
                .ToArray();
        }

        public IReadOnlyCollection<AssetRecord> Records { get; }
        public IReadOnlyCollection<SceneResource> LoadedResources => _loaded.Values.ToArray();

        public bool TryResolve(Guid assetGuid, out SceneResource? resource)
        {
            try
            {
                resource = Resolve(assetGuid);
                return true;
            }
            catch (InvalidDataException)
            {
                resource = null;
                return false;
            }
        }

        public SceneResource Resolve(Guid assetGuid)
        {
            if (_loaded.TryGetValue(assetGuid, out var loaded))
                return loaded;
            if (!_loading.Add(assetGuid))
                throw new InvalidDataException($"Cooked asset dependency cycle contains '{assetGuid}'.");
            try
            {
                if (!_assets.TryGetValue(assetGuid, out var asset) ||
                    asset.AssetType == SceneCookService.SceneAssetType ||
                    !Enum.IsDefined((EngineAssetType)asset.AssetType))
                    throw new InvalidDataException($"Cooked asset '{assetGuid}' is missing or has an unsupported type.");
                foreach (var dependency in asset.Dependencies)
                    Resolve(dependency);
                var resource = AssetFileCodec.Decode(new AssetFileData(
                    (EngineAssetType)asset.AssetType,
                    asset.AssetGuid,
                    asset.Dependencies,
                    asset.Payload), this);
                _loaded.Add(assetGuid, resource);
                return resource;
            }
            finally
            {
                _loading.Remove(assetGuid);
            }
        }

        public void Register(SceneResource resource, string? sourcePath = null, string? cookedPath = null,
            IEnumerable<Guid>? dependencies = null, string? contentHash = null,
            AssetImportStatus importStatus = AssetImportStatus.Imported)
            => throw new NotSupportedException("A cooked package registry is read-only.");

        public void RegisterMetadata(AssetRecord record)
            => throw new NotSupportedException("A cooked package registry is read-only.");

        public void DisposeLoadedResources()
        {
            foreach (var resource in _loaded.Values)
                resource.Dispose();
            _loaded.Clear();
        }
    }
}
