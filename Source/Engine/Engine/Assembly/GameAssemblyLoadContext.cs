using Spark.Engine.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assembly
{
    public class GameAssemblyLoadContext : AssemblyLoadContext
    {
        Engine Engine;
        public GameAssemblyLoadContext(Engine engine)
        {
            Engine = engine;
        }
        private static GameAssemblyLoadContext? _Instance;
        public static GameAssemblyLoadContext Instance
        {
            get 
            {
                if (_Instance == null)
                    throw new Exception();
                return _Instance;
            }
        }
        public static void InitInstance(Engine engine)
        {
            _Instance = new GameAssemblyLoadContext(engine);
        }

        protected override System.Reflection.Assembly? Load(AssemblyName assemblyName)
        {

            foreach(var ctx in All)
            {
                foreach(var assembly in ctx.Assemblies)
                {
                    if (assembly.GetName().FullName == assemblyName.FullName)
                    {
                        return assembly;
                    }
                }
            }
            if (FileSystem.Instance.FileExits($"{Engine.GameName}/{assemblyName.Name}.dll"))
            {
                using (var stream = FileSystem.Instance.GetStreamReader($"{Engine.GameName}/{assemblyName.Name}.dll"))
                {
                    return this.LoadFromStream(stream.BaseStream);
                }
            }
            else
            {
                return base.Load(assemblyName);
            }
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            return base.LoadUnmanagedDll(unmanagedDllName);
        }
    }
}
