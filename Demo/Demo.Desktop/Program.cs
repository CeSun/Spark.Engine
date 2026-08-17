using Demo;
using Spark.Engine.Builder;
using Spark.Engine.Desktop;
using Spark.Engine.Render.Pipeline.Forward;

// 桌面入口：只负责引导引擎 + 选择平台；演示内容（场景搭建）在 Demo 项目里
var builder = EngineBuilder.Create(args);

builder.InitializeWebGPU();
builder.UseDesktop();
builder.UseForward();

var game = builder.Build();

game.InitializeCallback = DemoApp.Initialize;

game.Run();
