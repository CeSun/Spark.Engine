using Microsoft.CodeAnalysis;
using System;
using System.IO;

namespace SourceGenerator
{
    [Generator]
    public class ShaderSourceGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var d = Directory.GetCurrentDirectory() + "/Shader";
            foreach(var dir in Directory.GetDirectories(d))
            {

            }
            context.AddSource("ShaderCode.cs", @"
            public static class ShaderSource 
            {
                
            }
        " + d);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }
    }
}

