using LiteEngine;
using System.Numerics;


namespace LiteEngine.Core.Actors;

public partial class Actor
{
    public Game GameInstance { get => Game.Instance; }
    public World World { get; internal set; }
}