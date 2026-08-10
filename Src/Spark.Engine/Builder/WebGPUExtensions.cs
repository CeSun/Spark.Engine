using Microsoft.Extensions.DependencyInjection;
using Silk.NET.WebGPU;
using Spark.Engine.Platforms;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine.Builder;

public static class WebGPUExtensions
{
    public unsafe static EngineBuilder InitializeWebGPU(this EngineBuilder builder)
    {
        var webGPU = WebGPU.GetApi();

        var instanceDescriptor = new InstanceDescriptor();

        var webGPUInstance = webGPU.CreateInstance(ref instanceDescriptor);

        builder.Services.AddSingleton(webGPU);

        builder.Services.AddSingleton(new WebGPUInstance(webGPUInstance));

        return builder;
    }
}

public unsafe class WebGPUInstance
{
    public Instance* Instance { get; }
    public WebGPUInstance(Instance* instance)
    {
        Instance = instance;
    }
}
