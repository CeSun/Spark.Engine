using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core.Network;

public class NetDriver
{
    public bool IsServer { get; private set; }

    public void InitServer(IPEndPoint iPEndPoint)
    {
        IsServer = true;
    }

    public void InitClient(IPEndPoint iPEndPoint)
    {
        IsServer = false;
    }

    public void Update()
    {

    }
}
