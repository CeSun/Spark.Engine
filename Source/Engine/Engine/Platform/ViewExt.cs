using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Platform;

public class View
{
    public static IView? Instance { get; private set; }

    public static void Init(IView view)
    {
        Instance = view;
    }
}
