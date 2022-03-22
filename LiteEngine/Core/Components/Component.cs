using LiteEngine.Core.Actors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Components;
public class Component
{
    public Component()
    {
        Name = "Component";
    }
    public string Name { get; set; }

    private List<Component> SubComponents = new List<Component>();

    private Component? _Parent = null;

    public Component? Parent { get => _Parent; }

    public void Attach(Component parent)
    {
        if (this.Parent != null)
            throw new Exception("该组件已经有父级了！");
        parent.SubComponents.Add(this);
        this._Parent = parent;
    }

    public Game GameInstance { get => Game.Instance; }
}
