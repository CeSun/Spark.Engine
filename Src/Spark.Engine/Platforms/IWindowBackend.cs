using System;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine.Platforms;

public interface IWindowBackend
{
    public IWindow CreateWindow(string title, int width, int height);
}
