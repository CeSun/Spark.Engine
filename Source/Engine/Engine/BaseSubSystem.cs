﻿
namespace Spark.Engine;

public class BaseSubSystem(Engine engine)
{
    public virtual bool ReceiveUpdate => false;

    public virtual bool ReceiveRender => false;
    public Engine CurrentEngine { get; set; } = engine;

    public virtual void BeginPlay()
    {

    }

    public virtual void Update(double deltaTime)
    {

    }
    public virtual void Render(double deltaTime)
    {

    }
    public virtual void EndPlay() 
    { 
    
    }
}
