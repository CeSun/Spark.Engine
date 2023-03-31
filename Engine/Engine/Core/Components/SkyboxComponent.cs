using Spark.Engine.Core.Actors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using static Spark.Engine.StaticEngine;
using System.Reflection;
using StbImageSharp;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Spark.Engine.Core.Components;

public class SkyboxComponent : PrimitiveComponent
{
    public SkyboxComponent(Actor actor) : base(actor)
    {

        InitRender();
        InitTexture();
    }

    uint Ebo;
    uint Vbo;
    uint Vao;
    uint TextureId;
    unsafe void InitRender()
    {
        float[] Vertex =
        {
            // x, y, z
            -1, 1, -1,
            -1, -1, -1,
            1, -1, -1,
            1, 1, -1,


            -1, 1, 1,
            -1, -1, 1,
            1, -1, 1,
            1, 1, 1,

        };

        uint[] indices =
        {
            0, 1, 2, 2, 3, 0,
            4, 5, 1, 1, 0, 4,
            7, 6, 5, 5, 4, 7,
            3, 2, 6, 6, 7, 3,
            4, 0, 3, 3, 7, 4,
            1, 5, 6, 6, 2, 1
        };
        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        Ebo = gl.GenBuffer();
        gl.BindVertexArray(Vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        fixed (void *data = Vertex)
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(Vertex.Length * sizeof(float)), data, GLEnum.StaticDraw);
        }
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);

        fixed (void* data = indices)
        {
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), data, GLEnum.StaticDraw);
        }
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(float) * 3, (void*)0);

        gl.BindVertexArray(0);

    }

    private unsafe void InitTexture()
    {
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, TextureId);
        string[] filename = {
            "pm_rt.jpg", "pm_lf.jpg", "pm_up.jpg", "pm_dn.jpg", "pm_bk.jpg", "pm_ft.jpg"
        };

        for(int i = 0; i < filename.Length; i++)
        {
            using (var sr = FileSystem.GetStreamReader("Content/Skybox/" + filename[i]))
            {
                var result = ImageResult.FromStream(sr.BaseStream); 
                fixed(void* data = result.Data)
                {
                    gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, (int)GLEnum.Rgb, (uint)result.Width, (uint)result.Height, 0, GLEnum.Rgb, GLEnum.UnsignedByte, data);
                }
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
                gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            }
        }
    }
    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
    }


    public unsafe void RenderSkybox(double DeltaTime)
    {
        gl.DepthMask(false);
        gl.BindVertexArray(Vao);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.TextureCubeMap, TextureId);
        gl.DrawElements(GLEnum.Triangles, (uint)36, GLEnum.UnsignedInt, (void*)0);
        gl.DepthMask(true);
    }

}
