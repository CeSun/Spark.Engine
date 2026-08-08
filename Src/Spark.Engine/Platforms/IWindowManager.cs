using System;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine.Platforms;

public interface IWindowManager
{
    public IWindow CreateWindow(string title, int width, int height);

    public IReadOnlyList<IWindow> Windows { get; }
}
