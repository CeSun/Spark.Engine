using Spark.Engine.Platforms;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine.Desktop;

internal class DesktopWindowManager : IWindowManager
{
    public IWindow? MainWindow => throw new NotImplementedException();

    public void CreateMainWindow(string title, int width, int height)
    {
        throw new NotImplementedException();
    }

    public IWindow CreateWindow(string title, int width, int height)
    {
        throw new NotImplementedException();
    }

    public IWindow DestroyWindow(IWindow window)
    {
        throw new NotImplementedException();
    }
}
