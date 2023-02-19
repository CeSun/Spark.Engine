using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public class ActorManager
{
    private Dictionary<string, List<Actor>> ActorMaps = new Dictionary<string, List<Actor>>();
    public void RegistActor(Actor actor)
    {
        var TypeName = actor.GetType().FullName;
        if (TypeName == null)
        {
            throw new Exception("类型名字为空");
        }
        List<Actor>? list = null;
        if (!ActorMaps.TryGetValue(TypeName, out list))
        {
            list = new List<Actor>();
        }
        if (list == null)
        {
            throw new Exception("列表初始化失败");
        }
        list.Add(actor);
        ActorMaps[TypeName] = list;
    }

    public List<T>? GetActors<T>() where T : Actor
    {
        var typeName = typeof(T).FullName;
        if (typeName == null)
        {
            throw new Exception("类型信息为空!");
        }
        if (!ActorMaps.TryGetValue(typeName, out var list))
        {
            return null;
        }
        return list as List<T>;
    }
}
