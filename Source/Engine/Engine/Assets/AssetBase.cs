using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public abstract class AssetBase: ISerializable
{
    public string Path = string.Empty;

    abstract public void Deserialize(BinaryReader Reader, Engine engine);

    abstract public void Serialize(BinaryWriter Writer, Engine engine);
}
