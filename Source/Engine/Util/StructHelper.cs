using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Util;


public static class UnsafeHelper
{
    public static unsafe IntPtr Malloc<T>(in T t) where T : struct
    {
        var p = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        ref T t2 = ref Unsafe.AsRef<T>((void*)p);
        t2 = t;
        return p;
    }
    public unsafe static ref T AsRef<T>(nint ptr) where T : struct
    {
        return ref Unsafe.AsRef<T>((void*)ptr);
    }
}