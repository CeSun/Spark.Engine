using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Util;

public static class AssemblyHelper
{
    public static Type? GetType(string Name)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return null;
        foreach(var ctx in AssemblyLoadContext.All)
        {
            foreach (var assembly in ctx.Assemblies)
            {
                var type = assembly.GetType(Name);
                if (type != null)
                    return type;
            }
        }
        return null;
    }
}
