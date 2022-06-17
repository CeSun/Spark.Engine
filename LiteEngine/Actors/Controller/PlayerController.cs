using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.Input;
namespace Spark.Core.Actors;

public class PlayerController : Controller
{

    private Dictionary<string, Action<Key>> ActionBinds;

    private Dictionary<string, Action<float>> AxisBinds;

    public PlayerController() : base()
    {
        ActionBinds = new Dictionary<string, Action<Key>>();
        AxisBinds = new Dictionary<string, Action<float>>();

    }


    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

    }

}
