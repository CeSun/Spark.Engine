using Spark.Engine.Editor;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class CookPackageTests
{
    [Fact]
    public void WindowsCookWritesDeterministicPackageWithDependencies()
    {
        var path = Path.Combine(Path.GetTempPath(), "spark-cook-" + Guid.NewGuid().ToString("N") + ".pak");
        var firstGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondGuid = Guid.Parse("00000000-0000-0000-0000-000000000002");
        try
        {
            new WindowsCookBackend().Cook(
            [
                new CookedAsset { AssetGuid = secondGuid, AssetType = 2, Payload = [4, 5], Dependencies = [firstGuid] },
                new CookedAsset { AssetGuid = firstGuid, AssetType = 1, Payload = [1, 2, 3] },
            ], path);

            var package = WindowsCookBackend.Load(path);
            Assert.Equal(CookTargetPlatform.Windows, package.TargetPlatform);
            Assert.Equal(2, package.Assets.Count);
            Assert.Equal(firstGuid, package.Assets[0].AssetGuid);
            Assert.Equal(secondGuid, package.Assets[1].AssetGuid);
            Assert.Equal<byte[]>([4, 5], package.Assets[1].Payload);
            Assert.Equal(firstGuid, Assert.Single(package.Assets[1].Dependencies));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CookRejectsDuplicateAssetIds()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Path.GetTempPath(), "spark-cook-" + Guid.NewGuid().ToString("N") + ".pak");
        try
        {
            Assert.Throws<InvalidDataException>(() => new WindowsCookBackend().Cook(
            [
                new CookedAsset { AssetGuid = id },
                new CookedAsset { AssetGuid = id },
            ], path));
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
