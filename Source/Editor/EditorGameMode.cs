﻿using Spark.Engine;
using Spark.Engine.Actors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Spark.Util;
namespace Editor;

public class EditorGameMode : GameMode
{
    public EditorGameMode(Level level, string Name = "") : base(level, Name)
    {
        DefaultPlayerControllerClass = typeof(PlayerController);
    }

    protected override void OnBeginPlay()
    {
        base.OnBeginPlay();
    }
}