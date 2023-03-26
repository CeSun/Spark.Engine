using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core.Assets;

public abstract class Asset
{

    public event Action? OnLoadCompleted;
    public Asset(string path, bool IsAsync)
    {
        Path = path;
        IsLoaded = false;
        IsValid = false;
        if (IsAsync)
        {
            var fun = async () =>
            {
                await AsyncLoad();
                IsLoaded = true;
                OnLoadCompleted?.Invoke();
            };
            fun();

        }
        else
        {
            AsyncLoad().Wait();
            IsLoaded = true;
            OnLoadCompleted?.Invoke();
        }

    }

    protected Asset()
    {
        Path = "";
    }
    public string Path { get; protected set; }

    public bool IsLoaded { get; protected set; }

    public bool IsValid { get; protected set; }
    
    protected abstract Task AsyncLoad();

}
