using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Editor.Panels;
using Spark.Engine;

namespace Editor.Windows;

public class WindowBase(Engine engine)
{

    protected Engine _engine = engine;

    protected readonly List<BasePanel> _panels = [];
    public void Open()
    {
        foreach (var panel in _panels)
        {
            panel.AddToViewPort();
        }
    }

    public void Close()
    {
        foreach (var panel in _panels)
        {
            panel.RemoveFromViewPort();
        }
    }
}

