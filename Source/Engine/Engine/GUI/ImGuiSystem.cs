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


    private void LoadFont(string Path)
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
                ImGui.GetIO().Fonts.AddFontFromMemoryTTF((nint)p, 14, 14, 0, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());
            }
        }
    }


    ImGuiController? Controller;
    public void Init()
    {
        try
        {
            Controller = new ImGuiController(CurrentLevel.Engine.Gl, CurrentLevel.CurrentWorld.Engine.View, CurrentLevel.CurrentWorld.Engine.Input,null, () =>
            {
                ref var flags = ref ImGui.GetIO().ConfigFlags;
                flags |= ImGuiConfigFlags.DockingEnable;
                ImGui.StyleColorsDark();
                LoadFont("Fonts/msyh.ttc");
                LoadFont("Fonts/forkawesome-webfont.ttf");

                unsafe
                {
                    ImGui.GetIO().NativePtr->IniFilename = (byte*)0;//(byte*)Marshal.StringToHGlobalAnsi(CurrentLevel.Engine.GameName + "/ImguiLayout.ini");

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

    public void Render(double DeltaTime)
    {
        if (Controller == null)
            return;
        if (CurrentLevel.Engine.Gl == null)
            return;
        CurrentLevel.CurrentWorld.Engine.Gl.Viewport(new System.Drawing.Size(CurrentLevel.CurrentWorld.Engine.WindowSize.X, CurrentLevel.CurrentWorld.Engine.WindowSize.Y));
        
        Controller?.Update((float)DeltaTime);
        ImGUICanvasList.ForEach(item => item.Render(DeltaTime));
        CurrentLevel.Engine.Gl.PushGroup("GUI Pass");
        Controller?.Render();
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
        Controller?.Dispose();
    }
}
