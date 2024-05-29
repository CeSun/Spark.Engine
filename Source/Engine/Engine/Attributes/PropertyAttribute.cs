using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class PropertyAttribute : Attribute
{
    public bool IgnoreSerialize = false;
    public bool IsDisplay = true;

    public string? DisplayName;

    public bool IsReadOnly = false;

    public string? CategoryName;
}


public static class AttributeHelper
{
    public static T? GetAttribute<T>(this PropertyInfo property) where T : Attribute
    {
        foreach(var attribute in property.GetCustomAttributes(true))
        {
            if (attribute is T att) 
                return att;
        }
        return null;
    }
}