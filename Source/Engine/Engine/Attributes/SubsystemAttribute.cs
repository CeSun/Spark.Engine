using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class SubsystemAttribute : Attribute
{
    public bool Enable { get; set; } = false;
}
