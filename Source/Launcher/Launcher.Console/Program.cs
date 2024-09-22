using Spark.Platform.Desktop;
using Spark.Core;
using Spark.Platform.Common;

var platform = new DesktopPlatform { View = null, FileSystem = new DesktopFileSystem(), GraphicsApi = null, InputContext = null };

var engine = new Engine(platform);

engine.Game = GameBuilder.CreateGame();

var app = new ConsoleApplication(engine);

app.Run();