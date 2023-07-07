using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Util;

public class ManualResetEventWithValue<T>
{
    ManualResetEvent Handle = new ManualResetEvent(false);
    T? Value;
    public void Set(T value)
    {
        Value = value;
        Handle.Set();
    }
    public T WaitForValue()
    {
        Handle.WaitOne();
        if (Value == null)
            throw new Exception(); // todo
        return Value;
    }


}
