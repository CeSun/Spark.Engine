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
    public class GameAssemblyLoadContext(Engine engine) : AssemblyLoadContext
    {
        private static GameAssemblyLoadContext? _instance;
        public static GameAssemblyLoadContext Instance
        {
            get 
            {
                if (_instance == null)
                    throw new Exception();
                return _instance;
            }
        }
        public static void InitInstance(Engine engine)
        {
            _instance = new GameAssemblyLoadContext(engine);
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
            if (FileSystem.Instance.FileExits($"{engine.GameName}/{assemblyName.Name}.dll"))
            {
                using var stream = FileSystem.Instance.GetStreamReader($"{engine.GameName}/{assemblyName.Name}.dll");
                return this.LoadFromStream(stream.BaseStream);
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
