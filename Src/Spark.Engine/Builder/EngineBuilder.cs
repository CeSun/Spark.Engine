using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine.Builder;

public class EngineBuilder
{
    public static EngineBuilder Create(string[] args)
    {
        var builder =  new EngineBuilder();

        builder.Services.AddSingleton(new EngineOptions()
        {
            Width = 800,
            Height = 600
        });

        builder.Services.AddSingleton(sp => new WindowManager(sp));

        return builder;
    }

    public ServiceCollection Services { get; } = new ServiceCollection();

    public EngineApplication Build()
    {
        var provider = Services.BuildServiceProvider();

        return new EngineApplication(provider);
    }
}
