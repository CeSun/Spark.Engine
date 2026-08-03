using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine;

public class EngineBuilder
{
    public static EngineBuilder Create(string[] args)
    {
        return new EngineBuilder();
    }

    public ServiceCollection Services { get; } = new ServiceCollection();

    public EngineApplication Build()
    {
        var provider = Services.BuildServiceProvider();

        return new EngineApplication(provider);
    }
}
