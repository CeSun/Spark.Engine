using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Sdk;
public interface IGameUI
{
    void OnInit();

    void OnUpdate(float deltaTime);

    void OnRender();

    void OnFini();
}
