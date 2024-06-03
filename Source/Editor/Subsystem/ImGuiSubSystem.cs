using System.Runtime.InteropServices;
using Editor.Panels;
using ImGuiNET;
using Silk.NET.OpenGLES.Extensions.ImGui;
using Spark.Engine;
using Spark.Engine.Attributes;

namespace Editor.Subsystem;

[Subsystem(Enable = true)]
public class ImGuiSubSystem : BaseSubSystem
{
    public ImGuiSubSystem(Engine engine) : base(engine)
    {

    }
    public override bool ReceiveRender => true;
    public override bool ReceiveUpdate => true;

    private readonly List<BasePanel> _imGuiCanvasList = [];
    public void AddCanvas(BasePanel imGuiCanvas)
    {
        _imGuiCanvasList.Add(imGuiCanvas);

    }

    public void RemoveCanvas(BasePanel imGuiCanvas)
    {
        _imGuiCanvasList.Remove(imGuiCanvas);
    }


    private ImFontPtr LoadFont(string path, int fontSize, char[] glyphRanges)
    {
        unsafe
        {
            fixed (void* p = glyphRanges)
            {
                return LoadFont(path, fontSize, (nint)p);
            }
        }
    }
    private ImFontPtr LoadFont(string path, int fontSize, nint glyphRanges)
    {
        List<byte> data = [];
        using (var sr = CurrentEngine.FileSystem.GetStreamReader(path))
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
    public override void BeginPlay()
    {
        try
        {
            _controller = new ImGuiController(CurrentEngine.GraphicsApi, CurrentEngine.View, CurrentEngine.Input, null, () =>
            {
                ref var flags = ref ImGui.GetIO().ConfigFlags;
                flags |= ImGuiConfigFlags.DockingEnable;
                ImGui.StyleColorsDark();

                _fonts.Add("msyh", LoadFont("../Fonts/msyh.ttc", 18, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull()));

                _fonts.Add("forkawesome", LoadFont("../Fonts/forkawesome-webfont.ttf", 14,
                [
                    (char)0xf000,
                    (char)0xf372
                ]));


                var style = ImGui.GetStyle();
                style.WindowMenuButtonPosition = ImGuiDir.None;
            });

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    public override void Render(double deltaTime)
    {
        if (_controller == null)
            return;
        CurrentEngine.GraphicsApi.Viewport(new System.Drawing.Size(CurrentEngine.WindowSize.X, CurrentEngine.WindowSize.Y));
        _controller?.Update((float)deltaTime);
        _imGuiCanvasList.ForEach(item => item.Render(deltaTime));
        CurrentEngine.GraphicsApi.PushGroup("GUI Pass");
        _controller?.Render();
        CurrentEngine.GraphicsApi.PopGroup();

    }

    public override void EndPlay()
    {
        _controller?.Dispose();
    }
}
