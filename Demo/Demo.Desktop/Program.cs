using Spark.Engine.Builder;
using Spark.Engine.Desktop;

var builder = EngineBuilder.Create(args);

builder.InitializeWebGPU();

builder.UseDesktop();

var game = builder.Build();

game.Run();