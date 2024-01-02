using Spark.Engine;
using Spark.Engine.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor;

[Subsystem(Enable = true)]
public class EditorSubsystem : BaseSubSystem
{
    public override bool ReceiveUpdate => true;
    public EditorSubsystem(Engine engine) : base(engine)
    {
    }


    public override void BeginPlay()
    {
        base.BeginPlay();

    }
}
