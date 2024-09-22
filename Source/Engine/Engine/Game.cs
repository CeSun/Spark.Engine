﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core;

public interface IGame
{
    public void BeginPlay(World world);
    public void Update(World world, double deltaTime);

    public void EndPlay(World world);
}
