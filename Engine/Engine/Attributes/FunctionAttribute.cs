using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class FunctionAttribute : Attribute
{
    public FunctionAttribute()
    {
    }
    public ERpcType RpcType { get; set; }

    public bool IsReliable { get; set; }

}

public enum ERpcType
{
    None,
    Client,
    Server,
    NetMutlicast
}