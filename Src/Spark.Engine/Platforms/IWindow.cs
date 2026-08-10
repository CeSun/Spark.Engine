using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Spark.Engine.Platforms;

public interface IWindow
{
    public Vector2 Size { get; }
    public void Initialize();
    public void Close();
    void PollEvents();
}
