using Spark.Platform.Desktop;
using Spark.Core;
using Spark.Platform.Common;
using Silk.NET.Input;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Input.Sdl;

SdlWindowing.RegisterPlatform();

SdlWindowing.Use();

SdlInput.RegisterPlatform();

SdlInput.Use();

WindowOptions options = WindowOptions.Default;

options.FramesPerSecond = 0;

options.UpdatesPerSecond = 0;

options.VSync = false;

options.ShouldSwapAutomatically = false;

options.API = new GraphicsAPI { API = ContextAPI.OpenGLES, Flags = ContextFlags.Default, Profile = ContextProfile.Core, Version = new APIVersion(3, 0) };

var window = Window.Create(options);

window.Initialize();

var gl = GL.GetApi(window);

var platform = new DesktopPlatform { View = window, FileSystem = new DesktopFileSystem(), GraphicsApi = gl, InputContext = window.CreateInput() };

var engine = new Engine(platform, new GameConfig());

var app = new RenderApplication(engine);

app.Run();