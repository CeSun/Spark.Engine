using System;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine.Platforms;

public interface IWindowManager
{
    public IWindow? MainWindow { get; }

    public void CreateMainWindow(string title, int width, int height);

    public IWindow CreateWindow(string title, int width, int height);

    public IWindow DestroyWindow(IWindow window);
}
