using Spark.Engine;
using Spark.Engine.Desktop;

var builder = EngineBuilder.Create(args);

builder.UseDesktop();

var game = builder.Build();

game.Run();