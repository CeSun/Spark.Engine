using Spark.Engine.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

[ActorInfo(DisplayOnEditor = false)]
public class GameMode : Actor
{
    List<PlayerController> _PlayerControllers = new List<PlayerController>();

    public IReadOnlyList<PlayerController> PlayerControllers => _PlayerControllers;


    public GameMode(Level level, string Name = "") : base(level, Name)
    {

    }

    [Property]
    public Type DefaultPlayerControllerClass { get; set; } = typeof(PlayerController);

    [Property]
    public Type DefaultPawnClass { get; set; } = typeof(Pawn);

    protected override void OnBeginPlay()
    {
        base.OnBeginPlay();

        var pc = (PlayerController)Activator.CreateInstance(DefaultPlayerControllerClass, [this.CurrentLevel, ""]);
        PlayerConnect(pc);

    }
    public void PlayerConnect(PlayerController playerController)
    {
        _PlayerControllers.Add(playerController);
        OnPlayerConnect(playerController);
    }

    public virtual void OnPlayerConnect(PlayerController playerController)
    {



    }

    public void PlayerDisconnect(PlayerController playerController)
    {
        _PlayerControllers.Remove(playerController);
        OnPlayerDisconnect(playerController);
    }
    public virtual void OnPlayerDisconnect(PlayerController playerController) 
    { 

    }
}
