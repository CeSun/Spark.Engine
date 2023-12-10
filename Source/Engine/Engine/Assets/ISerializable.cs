using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public interface ISerializable
{
    public void Serialize(StreamWriter Writer, Engine engine);

    public void Deserialize(StreamReader Reader, Engine engine);
}


public static class MagicCode
{
    public static int Asset = 19980625;
    public static int Texture = 1;
    public static int TextureCube = 2;
    public static int StaticMesh = 3;
    public static int SkeletalMesh = 4;

}