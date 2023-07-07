using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Util;

public class Singleton<T> where T : StaticCreateInterface<T>
{
    public static T Instance 
    { 
        get
        {
            if (_Instance == null)
                _Instance = T.Create();
            return _Instance;
        }
    }

    public static T? _Instance;
}


public interface StaticCreateInterface<T> where T : StaticCreateInterface<T>
{
    public abstract static T Create();
}