using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public interface ISerializable
{
    public void Serialize(StreamWriter Writer);

    public void Deserialize(StreamReader Reader);
}


public static class MagicCode
{
    public static int Asset = 19980625;
    public static int Texture = 1;

}