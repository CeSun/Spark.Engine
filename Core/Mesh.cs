using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core
{
    public class Mesh: RenderComponent
    {
        public Mesh(List<Vertex> vertices, List<int> indices, Material textures)
        {
            this.vertices = vertices;
            this.indices = indices;
            this.material = textures;
            unsafe
            {
                Vao = GL.GenVertexArray();
                Vbo = GL.GenBuffer();
                Ebo = GL.GenBuffer();
                GL.BindVertexArray(Vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
                fixed (Vertex* v = &CollectionsMarshal.AsSpan(vertices)[0])
                {
                    GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(Vertex), (IntPtr)v, BufferUsageHint.StaticDraw);
                }
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, Ebo);
                fixed (int* i = &CollectionsMarshal.AsSpan(indices)[0])
                {
                    GL.BufferData(BufferTarget.ElementArrayBuffer, vertices.Count * sizeof(int), (IntPtr)i, BufferUsageHint.StaticDraw);
                }
                GL.EnableVertexAttribArray(0);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, sizeof(Vertex), 0 );
                GL.EnableVertexAttribArray(1);
                GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, sizeof(Vertex), sizeof(Vector3));
                GL.EnableVertexAttribArray(2);
                GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, sizeof(Vertex), sizeof(Vector3) * 2);
            }
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }

        List<Vertex> vertices;
        List<int> indices;
        Material material;
        int Vao;
        int Vbo;
        int Ebo;
        public override void Draw(double deltaTime)
        {
            if (material.Shader == null)
                throw new Exception("123");
            if (Owner == null)
                throw new Exception("123");

            material.Shader?.Use();
            material.Shader?.SetMatrix4("model", Owner.Transform);
            material.Shader?.SetMatrix4("view", Camera.Current.ViewMat);
            material.Shader?.SetMatrix4("projection", Camera.Current.PerspectiveMat);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, material[0].Id);
            GL.BindVertexArray(Vao);
            GL.DrawElements(PrimitiveType.Triangles, indices.Count, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
        }
    }
}
