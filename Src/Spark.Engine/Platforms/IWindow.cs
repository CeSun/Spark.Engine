using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Spark.Engine.Platforms;

public interface IWindow
{
    Vector2 Size { get; set; }

    string Title { get; set; }

    bool IsClosing { get; }

    void Initialize();

    void Uninitialize();

    void PollEvents();

    void Close();
}
