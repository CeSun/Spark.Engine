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

namespace Spark.Engine.GUI;

public class ImGuiSystem
{

    List<ImGUIWindow> ImGUICanvasList = new List<ImGUIWindow>();
    public void AddCanvas(ImGUIWindow imGUICanvas)
    {
        ImGUICanvasList.Add(imGUICanvas);
       
    }

    public void RemoveCanvas(ImGUIWindow imGUICanvas)
    {
        ImGUICanvasList.Remove(imGUICanvas);
        

    }
    Level CurrentLevel { get; set; }

    public ImGuiSystem(Level level)
    {
        CurrentLevel = level;
    }

    private ImFontPtr LoadFont(string Path, int fontSize, char[] glyphRanges)
    {
        unsafe
        {
            fixed(void* p = glyphRanges)
            {
                return LoadFont(Path, fontSize, (nint)p);
            }
        }
    }
    private ImFontPtr LoadFont(string Path, int fontSize, nint glyphRanges)
    {
        List<byte> data = new List<byte>();
        using (var sr = FileSystem.Instance.GetStreamReader(Path))
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

    Dictionary<string, ImFontPtr> _Fonts = new Dictionary<string, ImFontPtr>();

    public IReadOnlyDictionary<string, ImFontPtr> Fonts => _Fonts;

    ImGuiController? Controller;
    public void Init()
    {
        try
        {
            Controller = new ImGuiController(CurrentLevel.Engine.GraphicsApi, CurrentLevel.CurrentWorld.Engine.View, CurrentLevel.CurrentWorld.Engine.Input,null, () =>
            {
                ref var flags = ref ImGui.GetIO().ConfigFlags;
                flags |= ImGuiConfigFlags.DockingEnable;
                ImGui.StyleColorsDark();
                _Fonts.Add("msyh", LoadFont("Fonts/msyh.ttc", 14, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull()));

                _Fonts.Add("forkawesome", LoadFont("Fonts/forkawesome-webfont.ttf", 14,
                [
                    (char)0xf000,
                    (char)0xf372
                ]));

                unsafe
                {
                    ImGui.GetIO().NativePtr->IniFilename = (byte*)0;//(byte*)Marshal.StringToHGlobalAnsi(CurrentLevel.Engine.GameName + "/ImguiLayout.ini");

                    var io = ImGui.GetIO();
                    io.WantSaveIniSettings = false;
                }
                if (FileSystem.Instance.FileExits(CurrentLevel.Engine.GameName + "/ImguiLayout.ini"))
                {
                    using var sr = FileSystem.Instance.GetStreamReader(CurrentLevel.Engine.GameName + "/ImguiLayout.ini");
                    ImGui.LoadIniSettingsFromMemory(sr.ReadToEnd());
                }
            });
        
        } 
        catch (Exception e)
        { 
            Console.WriteLine(e.ToString());
        }
    }

    public void Render(double DeltaTime)
    {
        if (Controller == null)
            return;
        if (CurrentLevel.Engine.GraphicsApi == null)
            return;
        CurrentLevel.CurrentWorld.Engine.GraphicsApi.Viewport(new System.Drawing.Size(CurrentLevel.CurrentWorld.Engine.WindowSize.X, CurrentLevel.CurrentWorld.Engine.WindowSize.Y));
        
        Controller?.Update((float)DeltaTime);
        ImGUICanvasList.ForEach(item => item.Render(DeltaTime));
        CurrentLevel.Engine.GraphicsApi.PushGroup("GUI Pass");
        Controller?.Render();
        CurrentLevel.Engine.GraphicsApi.PopGroup();

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
        Controller?.Dispose();
    }
}
