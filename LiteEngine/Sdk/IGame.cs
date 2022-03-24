using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Sdk;
public interface IGame
{
    public void OnUpdate(float deltaTime);
    public void OnRender();

    public void OnInit();

    public void OnFini();

    public void OnLevelLoaded();

    public void OnRoundStart();

    public void OnRoundEnd();


}
