using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Util;

public class Singleton<T> where T : class, new ()
{

    static Lazy<T> __Instance = new Lazy<T> ();
    static T? _Instance;
    

    static object LockFlag = new object();
    public static T Instance
    {
        get
        {
            if (_Instance == null)
            {
                lock (LockFlag)
                {
                    if (_Instance == null)
                    {
                        _Instance = new T();
                    }
                }
                if (_Instance == null)
                {
                    throw new Exception($"实例化{typeof(T).FullName}失败！");
                }
               
            }

            return _Instance;
        }
    }

}
