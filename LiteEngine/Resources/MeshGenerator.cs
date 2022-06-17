using Spark.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core.Resources
{
    public class MeshGenerator
    {
		public static Mesh GenBall()
		{
			List<Vertex> vertices = new List<Vertex>();
			List<uint> indices = new List<uint>();
			var X_SEGMENTS = 50;
			var Y_SEGMENTS = 50;
			for (int y = 0; y <= Y_SEGMENTS; y++)
			{
				for (int x = 0; x <= X_SEGMENTS; x++)
				{
					float xSegment = (float)x / (float)X_SEGMENTS;
					float ySegment = (float)y / (float)Y_SEGMENTS;
					float xPos = (float)(Math.Cos(xSegment * 2.0f * Math.PI) * Math.Sin(ySegment * Math.PI));
					float yPos = (float)Math.Cos(ySegment * Math.PI);
					float zPos = (float)(Math.Sin(xSegment * 2.0f * Math.PI) * Math.Sin(ySegment * Math.PI));
					vertices.Add(new Vertex { Location = new System.Numerics.Vector3((float)xPos, (float)yPos, (float)zPos), Color = new System.Numerics.Vector3(1, 1, 0) });
				}
			}
			for (int i = 0; i < Y_SEGMENTS; i++)
			{
				for (int j = 0; j < X_SEGMENTS; j++)
				{
					indices.Add((uint)(i * (X_SEGMENTS + 1) + j));
					indices.Add((uint)((i + 1) * (X_SEGMENTS + 1) + j));
					indices.Add((uint)((i + 1) * (X_SEGMENTS + 1) + j + 1));
					indices.Add((uint)(i * (X_SEGMENTS + 1) + j));
					indices.Add((uint)((i + 1) * (X_SEGMENTS + 1) + j + 1));
					indices.Add((uint)(i * (X_SEGMENTS + 1) + j + 1));
				}
			}
			var shader = Shader.LoadShader("default");
			return new Mesh(vertices, indices, new Material(null, shader));
		}
    }
}
