using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core
{
    public class Mesh
    {
        public Mesh(List<Vertex> vertices, List<int> indices, Material textures)
        {
            this.vertices = vertices;
            this.indices = indices;
            this.textures = textures;
            unsafe
            {
                Vao = GL.GenVertexArray();
                Vbo = GL.GenBuffer();
                Ebo = GL.GenBuffer();
                GL.BindVertexArray(Vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
                var array = vertices.ToArray();
                fixed (Vertex* v = &array[0])
                {
                    GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(Vertex), (IntPtr)v, BufferUsageHint.StaticDraw);

                }
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, Ebo);
                var indicesArray = indices.ToArray();
                fixed (int* i = &indicesArray[0])
                {
                    GL.BufferData(BufferTarget.ElementArrayBuffer, vertices.Count * sizeof(int), (IntPtr)i, BufferUsageHint.StaticDraw);
                }
                GL.EnableVertexAttribArray(0);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, sizeof(Vertex), 0 );
                GL.EnableVertexAttribArray(1);
                GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, sizeof(Vertex), sizeof(GlmSharp.vec3));
                GL.EnableVertexAttribArray(2);
                GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, sizeof(Vertex), sizeof(GlmSharp.vec3) * 2);
            }
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }

        List<Vertex>? vertices = null;
        List<int>? indices = null;
        Material? textures = null;
        int Vao;
        int Vbo;
        int Ebo;
        public void Draw(double deltaTime)
        {
            foreach(var texture in textures)
            {
                GL.BindTexture(TextureTarget.Texture2D, texture.Id);

            }
        }
    }
}
