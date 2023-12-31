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

    List<ImGUIContext> ImGUICanvasList = new List<ImGUIContext>();
    public void AddCanvas(ImGUIContext imGUICanvas)
    {
        ImGUICanvasList.Add(imGUICanvas);
       
    }

    public void RemoveCanvase(ImGUIContext imGUICanvas)
    {
        ImGUICanvasList.Remove(imGUICanvas);
        

    }
    Level CurrentLevel { get; set; }

    public ImGuiSystem(Level level)
    {
        CurrentLevel = level;
    }


    ImGuiController? Controller;
    public void Init()
    {
        try
        {
            Controller = new ImGuiController(CurrentLevel.Engine.Gl, CurrentLevel.CurrentWorld.Engine.View, CurrentLevel.CurrentWorld.Engine.Input);
            ref var v = ref ImGui.GetIO().WantSaveIniSettings;
            v = false;
            List<byte> data = new List<byte>();
            using (var sr = FileSystem.Instance.GetStreamReader("Fonts/simhei.ttf"))
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
                fixed(void* p = CollectionsMarshal.AsSpan(data))
                {
                }
            }

            var io = ImGui.GetIO();
            unsafe
            {

                var config = ImGuiNative.ImFontConfig_ImFontConfig();
                var font = io.Fonts.AddFontFromFileTTF("Fonts/simhei.ttf", 13, config, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());
            }
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
        Controller?.Dispose();
    }
}
