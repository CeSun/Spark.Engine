namespace Spark.Core.Platform;

public interface IFileSystem
{
    bool Exists(string Path);
    StreamReader GetStream(string Path);
    bool Exists(string ModuleName, string Path);
    StreamReader GetStream(string ModuleName, string Path);
}

