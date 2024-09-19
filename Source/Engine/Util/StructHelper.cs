using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Util;


public static class StructPointerHelper
{
    public static void Free(this ref IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return;
        unsafe
        {
            delegate* unmanaged[Cdecl]<IntPtr, void> fun = *(delegate* unmanaged[Cdecl]<IntPtr, void>*)ptr;
            if (fun != null)
            {
                fun(ptr);
            }
        }
        Marshal.FreeHGlobal(ptr);
        ptr = IntPtr.Zero;
    }

    public static unsafe IntPtr Malloc<T>(in T t) where T : struct
    {
        var p = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        ref T t2 = ref Unsafe.AsRef<T>((void*)p);
        t2 = t;
        return p;
    }
}