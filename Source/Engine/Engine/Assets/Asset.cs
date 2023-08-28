using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public abstract class Asset
{

    public event Action? OnLoadCompleted;
    public Asset(string path)
    {
        Path = path;
        IsLoaded = false;
        IsValid = false;
        try
        {
            LoadAsset();
            IsValid = true;
        }
        catch
        {
            IsValid = false;
            throw;
        }
        finally
        {
            IsLoaded = true;
            OnLoadCompleted?.Invoke();
        }

    }

    public void Load()
    {

    }

    public async ValueTask LoadAsync()
    {
        await Task.Yield();
    }

    protected Asset()
    {
        Path = "";
    }
    public string Path { get; protected set; }

    public bool IsLoaded { get; protected set; }

    public bool IsValid { get; protected set; }

    protected abstract void LoadAsset();

}
