using LiteEngine.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Components;

public class PictureComponent : RenderableComponent
{
    List<Vertex> vertices = new ();

    List<uint> indices = new();

    public PictureComponent(Component parent, string name) : base(parent, name)
    {
        
        for(var i = 0; i < 4; i++)
        {
            var vertex = new Vertex();
            switch (i)
            {
                case 0:
                    vertex.Location = new (-1,1,0);
                    vertex.TexCoord = new(0,1);
                    break;
                case 1:
                    vertex.Location = new(1, 1, 0);
                    vertex.TexCoord = new(1, 1);
                    break;
                case 2:
                    vertex.Location = new(1, -1, 0);
                    vertex.TexCoord = new(1, 0);
                    break;
                case 3:
                    vertex.Location = new(0, 0, 0);
                    vertex.TexCoord = new(-1, -1);
                    break;
            }
            vertex.Normal = new(0, 0, 1);
            vertex.Color = new(1, 0, 0);
            vertices.Add(vertex);
        }
        indices.AddRange(new uint[]{1,3,2,2,1,0});
    }
}
