namespace Spark.Core.Platform;

public interface IFileSystem
{
    StreamReader GetStream(string Path);

    StreamReader GetStream(string ModuleName, string Path);
}

