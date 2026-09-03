using Demo;
using Spark.Engine.Builder;
using Spark.Engine.Desktop;
using Spark.Engine.Editor;
using Spark.Engine.Render.Pipeline.BlinnPhong;

// 桌面入口：只负责引导引擎 + 选择平台；演示内容（场景搭建）在 Demo 项目里
var builder = EngineBuilder.Create(args);

builder.InitializeWebGPU();
builder.UseDesktop();
builder.UseBlinnPhong();
builder.UseEditor(DemoApp.ConfigureEditor, projectDirectory: "Demo");

var game = builder.Build();

game.InitializeCallback = DemoApp.Initialize;

game.Run();
