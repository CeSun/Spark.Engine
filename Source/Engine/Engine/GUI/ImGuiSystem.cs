using ImGuiNET;
using Silk.NET.OpenGLES;
using Silk.NET.OpenGLES.Extensions.ImGui;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Components;
using Spark.Engine.Platform;
using Spark.Util;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;

namespace Spark.Engine.GUI;

public class ImGuiSystem
{
    readonly List<ImGUIWindow> _imGuiCanvasList = [];
    public void AddCanvas(ImGUIWindow imGuiCanvas)
    {
        _imGuiCanvasList.Add(imGuiCanvas);
       
    }

    public void RemoveCanvas(ImGUIWindow imGuiCanvas)
    {
        _imGuiCanvasList.Remove(imGuiCanvas);
        

    }
    Level CurrentLevel { get; set; }

    public ImGuiSystem(Level level)
    {
        CurrentLevel = level;
    }

    private ImFontPtr LoadFont(string path, int fontSize, char[] glyphRanges)
    {
        unsafe
        {
            fixed(void* p = glyphRanges)
            {
                return LoadFont(path, fontSize, (nint)p);
            }
        }
    }
    private ImFontPtr LoadFont(string path, int fontSize, nint glyphRanges)
    {
        List<byte> data = [];
        using (var sr = CurrentLevel.Engine.FileSystem.GetStreamReader(path))
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

    readonly Dictionary<string, ImFontPtr> _fonts = [];

    public IReadOnlyDictionary<string, ImFontPtr> Fonts => _fonts;

    private ImGuiController? _controller;
    public void Init()
    {
        try
        {
            _controller = new ImGuiController(CurrentLevel.Engine.GraphicsApi, CurrentLevel.CurrentWorld.Engine.View, CurrentLevel.CurrentWorld.Engine.Input,null, () =>
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
        CurrentLevel.CurrentWorld.Engine.GraphicsApi.Viewport(new System.Drawing.Size(CurrentLevel.CurrentWorld.Engine.WindowSize.X, CurrentLevel.CurrentWorld.Engine.WindowSize.Y));
        
        _controller?.Update((float)deltaTime);
        _imGuiCanvasList.ForEach(item => item.Render(deltaTime));
        CurrentLevel.Engine.GraphicsApi.PushGroup("GUI Pass");
        _controller?.Render();
        CurrentLevel.Engine.GraphicsApi.PopGroup();

    }

    public void Fini()
    {
        if (CurrentLevel.CurrentWorld.Engine.IsMobile)
            return;
        _controller?.Dispose();
    }
}
