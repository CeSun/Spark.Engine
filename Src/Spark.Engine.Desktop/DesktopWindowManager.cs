using Spark.Engine.Platforms;
using System;
using System.Collections.Generic;
using System.Text;
using SN = Silk.NET;
using SNW = Silk.NET.Windowing;

namespace Spark.Engine.Desktop;

public class DesktopWindowManager : IWindowManager
{
    internal List<DesktopWindow> windows = new List<DesktopWindow>();

    public IReadOnlyList<IWindow> Windows => windows.AsReadOnly();

    public IWindow CreateWindow(string title, int width, int height)
    {
        var options = SNW.WindowOptions.Default with
        {
            Size = new SN.Maths.Vector2D<int>(width, height),
            Title = title
        };

        var window = SNW.Window.Create(options);

        var desktopWindow = new DesktopWindow(window, this);

        return desktopWindow;
    }
}
