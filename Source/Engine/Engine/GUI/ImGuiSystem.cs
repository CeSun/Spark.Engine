using ImGuiNET;
using Silk.NET.OpenGLES.Extensions.ImGui;
using Spark.Engine.Platform;
using System.Runtime.InteropServices;

namespace Spark.Engine.GUI;

public class ImGuiSystem(Level level)
{
    private Level CurrentLevel { get; } = level;

    private static ImFontPtr LoadFont(string path, int fontSize, char[] glyphRanges)
    {
        unsafe
        {
            fixed(void* p = glyphRanges)
            {
                return LoadFont(path, fontSize, (nint)p);
            }
        }
    }
    private static ImFontPtr LoadFont(string path, int fontSize, nint glyphRanges)
    {
        List<byte> data = new List<byte>();
        using (var sr = FileSystem.Instance.GetStreamReader(path))
        {
            var br = new BinaryReader(sr.BaseStream);

            byte[] buffer = new byte[1024];
            while (true)
            {
                var len = br.Read(buffer, 0, buffer.Length);
                if (len <= 0)
                {
                    break;
                }
                data.AddRange(buffer.Take(len));
            }
        }
        unsafe
        {
            fixed (void* p = CollectionsMarshal.AsSpan(data))
            {
                return ImGui.GetIO().Fonts.AddFontFromMemoryTTF((nint)p, fontSize, fontSize, 0, glyphRanges);
            }
        }
    }

    private readonly Dictionary<string, ImFontPtr> _fonts = [];

    public IReadOnlyDictionary<string, ImFontPtr> Fonts => _fonts;

    ImGuiController? _controller;
    public void Init()
    {
        try
        {
            _controller = new ImGuiController(CurrentLevel.Engine.Gl, CurrentLevel.CurrentWorld.Engine.View, CurrentLevel.CurrentWorld.Engine.Input,null, () =>
            {
                ref var flags = ref ImGui.GetIO().ConfigFlags;
                flags |= ImGuiConfigFlags.DockingEnable;
                ImGui.StyleColorsDark();
                _fonts.Add("msyh", LoadFont("Fonts/msyh.ttc", 14, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull()));

                _fonts.Add("forkawesome", LoadFont("Fonts/forkawesome-webfont.ttf", 14,
                [
                    (char)0xf000,
                    (char)0xf372
                ]));

                unsafe
                {
                    ImGui.GetIO().NativePtr->IniFilename = (byte*)0;
                    var io = ImGui.GetIO();
                    io.WantSaveIniSettings = false;
                }
                if (FileSystem.Instance.FileExits(CurrentLevel.Engine.GameName + "/ImguiLayout.ini"))
                {
                    ImGui.LoadIniSettingsFromMemory(FileSystem.Instance.GetStreamReader(CurrentLevel.Engine.GameName + "/ImguiLayout.ini").ReadToEnd());
                }
            });
        
        } 
        catch (Exception e)
        { 
            Console.WriteLine(e.ToString());
        }
    }

    public void Render(double deltaTime)
    {
        if (_controller == null)
            return;
        if (CurrentLevel.Engine.Gl == null)
            return;

        CurrentLevel.Engine.Gl.Viewport(new System.Drawing.Size(CurrentLevel.CurrentWorld.Engine.WindowSize.X, CurrentLevel.CurrentWorld.Engine.WindowSize.Y));
        
        _controller?.Update((float)deltaTime);
        CurrentLevel.Engine.Gl.PushGroup("GUI Pass");
        _controller?.Render();
        CurrentLevel.Engine.Gl.PopGroup();

    }

    public void Fini()
    {
        if (CurrentLevel.CurrentWorld.Engine.IsMobile)
            return;
        var ini = ImGui.SaveIniSettingsToMemory();
        using(var sw = FileSystem.Instance.GetStreamWriter(CurrentLevel.Engine.GameName + "/ImguiLayout.ini"))
        {
            sw.Write(ini);
        }
        _controller?.Dispose();
    }
}
