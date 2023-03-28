using Spark.Engine.Core.Assets;
using Spark.Engine.Core.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using static Spark.Engine.StaticEngine;
using Shader = Spark.Engine.Core.Assets.Shader;
using static Spark.Engine.Core.Components.CameraComponent;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using SharpGLTF.Transforms;
using SharpGLTF.Schema2;

namespace Spark.Engine.Core.Render;

public class SceneRenderer
{
    RenderBuffer GloblaBuffer;
    Shader BaseShader;
    Shader DirectionalLightingShader;
    Shader AmbientLightingShader;
    World World { get; set; }

    uint PostProcessVAO = 0;
    uint PostProcessVBO = 0;
    uint PostProcessEBO = 0;
    public SceneRenderer(World world)
    {
        World = world;
        BaseShader = new Shader("/Shader/Deferred/Base");
        DirectionalLightingShader = new Shader("/Shader/Deferred/DirectionalLighting");
        AmbientLightingShader = new Shader("/Shader/Deferred/AmbientLighting");
        GloblaBuffer = new RenderBuffer(Engine.Instance.WindowSize.X, Engine.Instance.WindowSize.Y);
        InitRender();
    }

    public unsafe void InitRender()
    {
        DeferredVertex[] Vertices = new DeferredVertex[4] { 
            new () {Location = new Vector3(-1, 1, 0), TexCoord = new Vector2(0, 1) },
            new () {Location = new Vector3(-1, -1, 0), TexCoord = new Vector2(0, 0) },
            new () {Location = new Vector3(1, -1, 0), TexCoord = new Vector2(1, 0) },
            new () {Location = new Vector3(1, 1, 0), TexCoord = new Vector2(1, 1) },
        };

        uint[] Indices = new uint[6]
        {
            0, 1, 2, 2, 3,0
        };
        PostProcessVAO = gl.GenVertexArray();
        PostProcessVBO = gl.GenBuffer();
        PostProcessEBO = gl.GenBuffer();
        gl.BindVertexArray(PostProcessVAO);
        gl.BindBuffer(GLEnum.ArrayBuffer, PostProcessVBO);
        fixed (DeferredVertex* p = Vertices)
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(Vertices.Length * sizeof(DeferredVertex)), p, GLEnum.StaticDraw);
        }
        gl.BindBuffer(GLEnum.ElementArrayBuffer, PostProcessEBO);
        fixed (uint* p = Indices)
        {
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(Indices.Length * sizeof(uint)), p, GLEnum.StaticDraw);
        }
        // Location
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(DeferredVertex), (void*)0);
        // TexCoord
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, GLEnum.Float, false, (uint)sizeof(DeferredVertex), (void*)(sizeof(Vector3)));
        gl.BindVertexArray(0);


    }

    public void Render(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        GloblaBuffer.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);

        BasePass(DeltaTime);

        LightingPass(DeltaTime);
    }

    private void BasePass(double DeltaTime)
    {
        GloblaBuffer.Render(() =>
        {
            gl.Enable(EnableCap.DepthTest);
            gl.ClearColor(Color.Black);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            Shader.GlobalShader = BaseShader;
            if (CurrentCameraComponent != null)
            {
                BaseShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
                BaseShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
            }
            foreach (var component in World.CurrentLevel.PrimitiveComponents)
            {
                if (component.IsDestoryed == false)
                {
                    component.Render(DeltaTime);
                }
            }
        });

    }

    private void LightingPass(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        CurrentCameraComponent.RenderTarget.RenderTo(() =>
        {
            // 清除背景颜色
            gl.ClearColor(Color.Black);
            gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
            gl.Disable(EnableCap.DepthTest);
            gl.BlendEquation(GLEnum.FuncAdd);
            gl.BlendFunc(GLEnum.One, GLEnum.One);
            gl.Enable(EnableCap.Blend);

            // 环境光
            AmbientLightingPass();
            // 定向光
            DirectionalLight();


            gl.Disable(EnableCap.Blend);

        });
    }

    private unsafe void AmbientLightingPass()
    {
        if (CurrentCameraComponent == null)
            return;
        AmbientLightingShader.Use();
        AmbientLightingShader.SetVector2("TexCoordScale",
            new Vector2
            {
                X = GloblaBuffer.Width / (float)GloblaBuffer.BufferWidth,
                Y = GloblaBuffer.Height / (float)GloblaBuffer.BufferHeight
            });
        AmbientLightingShader.SetFloat("Brightness", 0.1f);
        AmbientLightingShader.SetInt("ColorTexture", 0);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.ColorId);


        gl.BindVertexArray(PostProcessVAO);
        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
        gl.ActiveTexture(GLEnum.Texture0);
        AmbientLightingShader.UnUse();
    }

    private unsafe void DirectionalLight()
    {
        if (CurrentCameraComponent == null)
            return;
        foreach (var DirectionalLight in World.CurrentLevel.DirectionLightComponents)
        {
            Matrix4x4.Invert((CurrentCameraComponent.View * CurrentCameraComponent.Projection), out var VPInvert);
            DirectionalLightingShader.SetMatrix("VPInvert", VPInvert);


            AmbientLightingShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = GloblaBuffer.Width / (float)GloblaBuffer.BufferWidth,
                    Y = GloblaBuffer.Height / (float)GloblaBuffer.BufferHeight
                });


            DirectionalLightingShader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.ColorId);


            DirectionalLightingShader.SetInt("NormalTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.NormalId);


            DirectionalLightingShader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.DepthId);

            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);
            DirectionalLightingShader.UnUse();



        }
    }

}

struct DeferredVertex
{
    public Vector3 Location;
    public Vector2 TexCoord;
}
