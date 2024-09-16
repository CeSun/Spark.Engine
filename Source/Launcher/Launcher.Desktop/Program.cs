using Desktop;
using Silk.NET.Input;
using Silk.NET.Input.Sdl;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Spark;
using Spark.Platform.Common;

SdlWindowing.RegisterPlatform();
SdlWindowing.Use();

WindowOptions options = WindowOptions.Default;

options.API = new GraphicsAPI { API = ContextAPI.OpenGLES, Flags = ContextFlags.Default, Profile = ContextProfile.Core, Version = new APIVersion(3, 0) };

var window = Window.Create(options);

window.Initialize();

var gl = GL.GetApi(window);

var platform = new DesktopPlatform { View = window, FileSystem = new DesktopFileSystem(), GraphicsApi = gl, InputContext = window.CreateInput() };

var engine = new Engine(platform);

var app = new WindowApplication(engine);

app.Run();