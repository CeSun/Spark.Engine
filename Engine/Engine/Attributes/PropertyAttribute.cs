using Spark.Engine.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class PropertyAttribute : Attribute
{
    public string? Category { get; set; }
    public string? DisplayName { get; set; }

    public bool DefaultComponent { get; set; }

    public string? AttachTo { get; set; }
    public string? AttachSocket { get; set; }

}
