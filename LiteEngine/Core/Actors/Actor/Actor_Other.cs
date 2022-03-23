using LiteEngine;
using System.Numerics;


namespace LiteEngine.Core.Actors;

public partial class Actor
{
    public Engine EngineInstance { get => Engine.Instance; }
    public World World { get; internal set; }
}