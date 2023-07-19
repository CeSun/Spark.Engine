using System.Numerics;

namespace Spark.Engine.Assets;

public class StaticMesh : AssetBase
{
    public StaticMesh()
    {
        Sectors = new List<Sector>();
    }
    public class Sector
    {
        public required List<Vertex> Vertices;
        public required List<uint> Indices;
        public required Material Material;
        public Sector() 
        {
        }

    }
    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord;
    }

    public List<Sector> Sectors;

}
