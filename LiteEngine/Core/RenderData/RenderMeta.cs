using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.RenderData
{
    public class RenderMeta<TVertex> where TVertex : struct
    {
        public RenderMeta(List<TVertex> vertices, Material material)
        {
            Vertices = vertices;
            Material = material;
        }
        public RenderType RenderType { get; set; }

        public Material Material { get; set; }
        public List<TVertex> Vertices { get; set; }
    }
    public enum RenderType
    {
        Triangle,
        Line
    }
    public struct Vertex
    {

    }

    public struct VertexLine
    {

    }
}
