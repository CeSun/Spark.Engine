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
using Spark.Util;

namespace Spark.Engine.Core.Render;

public class SceneRenderer
{
    RenderBuffer GloblaBuffer;
    Shader BaseShader;
    Shader BrightnessLightingShader;
    Shader DirectionalLightingShader;
    Shader SpotLightingShader;
    Shader PointLightingShader;

    Shader DLShadowMapShader;
    Shader SpotShadowMapShader;

    World World { get; set; }

    uint PostProcessVAO = 0;
    uint PostProcessVBO = 0;
    uint PostProcessEBO = 0;
    public SceneRenderer(World world)
    {
        World = world;
        BaseShader = new Shader("/Shader/Deferred/Base");
        BrightnessLightingShader = new Shader("/Shader/Deferred/BrightnessLighting");
        DirectionalLightingShader = new Shader("/Shader/Deferred/DirectionalLighting");
        SpotLightingShader = new Shader("/Shader/Deferred/SpotLighting");
        PointLightingShader = new Shader("/Shader/Deferred/PointLighting");
        DLShadowMapShader = new Shader("/Shader/ShadowMap/DirectionLightShadow");
        SpotShadowMapShader = new Shader("/Shader/ShadowMap/SpotLightShadow");
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

        gl.Enable(GLEnum.CullFace);
        gl.CullFace(GLEnum.Back);

        DepthPass(DeltaTime);
        BasePass(DeltaTime); 
        LightingPass(DeltaTime);
    }
    private void DepthPass(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        gl.Enable(GLEnum.DepthTest);
        gl.Enable(GLEnum.CullFace);
        gl.CullFace(GLEnum.Front);


        DLShadowMapShader.Use();
        foreach (var DirectionalLight in World.CurrentLevel.DirectionLightComponents)
        {
            var View = Matrix4x4.CreateLookAt(DirectionalLight.WorldLocation, DirectionalLight.WorldLocation + DirectionalLight.ForwardVector, DirectionalLight.UpVector);
            var Projection = Matrix4x4.CreateOrthographic(100, 100, 1.0f, 100f);
            gl.Viewport(new Rectangle(0, 0, DirectionalLight.ShadowMapSize.X, DirectionalLight.ShadowMapSize.Y));
            gl.BindFramebuffer(GLEnum.Framebuffer, DirectionalLight.ShadowMapFrameBufferID);
            gl.Clear(ClearBufferMask.DepthBufferBit);
            BaseShader.SetMatrix("ViewTransform", View);
            BaseShader.SetMatrix("ProjectionTransform", Projection);
            foreach (var component in World.CurrentLevel.PrimitiveComponents)
            {
                if (component.IsDestoryed == false)
                {
                    component.Render(DeltaTime);
                }
            }

            gl.BindFramebuffer(GLEnum.Framebuffer, 0);
            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));
        }
        DLShadowMapShader.UnUse();

        SpotLightingShader.Use();
        foreach(var SpotLight in World.CurrentLevel.SpotLightComponents)
        {
            var View = Matrix4x4.CreateLookAt(SpotLight.WorldLocation, SpotLight.WorldLocation + SpotLight.ForwardVector, SpotLight.UpVector);
            var Projection = Matrix4x4.CreatePerspectiveFieldOfView(170F.DegreeToRadians(), 1, 1F, 100);
            gl.Viewport(new Rectangle(0, 0, SpotLight.ShadowMapSize.X, SpotLight.ShadowMapSize.Y));
            gl.BindFramebuffer(GLEnum.Framebuffer, SpotLight.ShadowMapFrameBufferID);
            gl.Clear(ClearBufferMask.DepthBufferBit);

            BaseShader.SetMatrix("ViewTransform", View);
            BaseShader.SetMatrix("ProjectionTransform", Projection);

            foreach (var component in World.CurrentLevel.PrimitiveComponents)
            {
                if (component.IsDestoryed == false)
                {
                    component.Render(DeltaTime);
                }
            }

            gl.BindFramebuffer(GLEnum.Framebuffer, 0);
            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));

        }


        SpotLightingShader.UnUse();







        gl.CullFace(GLEnum.Back);
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

            // 间接光
            AmbientLightingPass();
            // 定向光
            DirectionalLight();
            // 点光源
            PointLight();
            // 
            SpotLight();
            gl.Disable(EnableCap.Blend);

        });
    }

    private unsafe void AmbientLightingPass()
    {
        if (CurrentCameraComponent == null)
            return;
        BrightnessLightingShader.Use();
        BrightnessLightingShader.SetVector2("TexCoordScale",
            new Vector2
            {
                X = GloblaBuffer.Width / (float)GloblaBuffer.BufferWidth,
                Y = GloblaBuffer.Height / (float)GloblaBuffer.BufferHeight
            });
        BrightnessLightingShader.SetFloat("Brightness",0.0f);
        BrightnessLightingShader.SetInt("ColorTexture", 0);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.ColorId);


        gl.BindVertexArray(PostProcessVAO);
        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
        gl.ActiveTexture(GLEnum.Texture0);
        BrightnessLightingShader.UnUse();
    }

    private unsafe void DirectionalLight()
    {
        if (CurrentCameraComponent == null)
            return;
        DirectionalLightingShader.Use();
        foreach (var DirectionalLight in World.CurrentLevel.DirectionLightComponents)
        {
            var LightInfo = DirectionalLight.LightInfo;
            Matrix4x4.Invert((CurrentCameraComponent.View * CurrentCameraComponent.Projection), out var VPInvert);
            DirectionalLightingShader.SetMatrix("VPInvert", VPInvert);


            var View = Matrix4x4.CreateLookAt(DirectionalLight.WorldLocation, DirectionalLight.WorldLocation + DirectionalLight.ForwardVector, DirectionalLight.UpVector);
            var Projection = Matrix4x4.CreateOrthographic(100, 100, 1.0f, 100f);

            var WorldToLight = View * Projection;
            DirectionalLightingShader.SetMatrix("WorldToLight", WorldToLight);
            DirectionalLightingShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = GloblaBuffer.Width / (float)GloblaBuffer.BufferWidth,
                    Y = GloblaBuffer.Height / (float)GloblaBuffer.BufferHeight
                });


            DirectionalLightingShader.SetFloat("AmbientStrength", DirectionalLight.AmbientStrength);
            DirectionalLightingShader.SetFloat("LightStrength", DirectionalLight.LightStrength);
            DirectionalLightingShader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.ColorId);


            DirectionalLightingShader.SetInt("NormalTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.NormalId);


            DirectionalLightingShader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.DepthId);

            DirectionalLightingShader.SetInt("ShadowMapTexture", 3);
            gl.ActiveTexture(GLEnum.Texture3);
            gl.BindTexture(GLEnum.Texture2D, DirectionalLight.ShadowMapTextureID);

            DirectionalLightingShader.SetVector3("LightDirection", LightInfo.Direction);
            DirectionalLightingShader.SetVector3("LightColor", LightInfo.Color);

            DirectionalLightingShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);


        }
        DirectionalLightingShader.UnUse();

    }

    public unsafe void PointLight()
    {
        if (CurrentCameraComponent == null)
            return;
        PointLightingShader.Use();
        foreach (var PointLightComponent in World.CurrentLevel.PointLightComponents)
        {
            Matrix4x4.Invert((CurrentCameraComponent.View * CurrentCameraComponent.Projection), out var VPInvert);
            PointLightingShader.SetMatrix("VPInvert", VPInvert);


            PointLightingShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = GloblaBuffer.Width / (float)GloblaBuffer.BufferWidth,
                    Y = GloblaBuffer.Height / (float)GloblaBuffer.BufferHeight
                });

            PointLightingShader.SetFloat("Constant", PointLightComponent.Constant);
            PointLightingShader.SetFloat("Linear", PointLightComponent.Linear);
            PointLightingShader.SetFloat("Quadratic", PointLightComponent.Quadratic);

            PointLightingShader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.ColorId);


            PointLightingShader.SetInt("NormalTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.NormalId);


            PointLightingShader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.DepthId);



            PointLightingShader.SetVector3("LightLocation", PointLightComponent.WorldLocation);
            PointLightingShader.SetVector3("LightColor", PointLightComponent._Color);

            PointLightingShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);


        }
        PointLightingShader.UnUse();
    }


    public unsafe void SpotLight()
    {

        if (CurrentCameraComponent == null)
            return;
        SpotLightingShader.Use();
        foreach (var SpotLightComponent in World.CurrentLevel.SpotLightComponents)
        {
            Matrix4x4.Invert((CurrentCameraComponent.View * CurrentCameraComponent.Projection), out var VPInvert);
            SpotLightingShader.SetMatrix("VPInvert", VPInvert);


            SpotLightingShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = GloblaBuffer.Width / (float)GloblaBuffer.BufferWidth,
                    Y = GloblaBuffer.Height / (float)GloblaBuffer.BufferHeight
                });



            var View = Matrix4x4.CreateLookAt(SpotLightComponent.WorldLocation, SpotLightComponent.WorldLocation + SpotLightComponent.ForwardVector, SpotLightComponent.UpVector);
            var Projection = Matrix4x4.CreatePerspectiveFieldOfView(170F.DegreeToRadians(), 1, 1F, 100);
            var WorldToLight = View * Projection;
            SpotLightingShader.SetMatrix("WorldToLight", WorldToLight);
            SpotLightingShader.SetFloat("Constant", SpotLightComponent.Constant);
            SpotLightingShader.SetFloat("Linear", SpotLightComponent.Linear);
            SpotLightingShader.SetFloat("Quadratic", SpotLightComponent.Quadratic);


            SpotLightingShader.SetFloat("InnerCosine", (float)Math.Cos(SpotLightComponent.InnerAngle.DegreeToRadians()));

            SpotLightingShader.SetFloat("OuterCosine", (float)Math.Cos(SpotLightComponent.OuterAngle.DegreeToRadians()));
            SpotLightingShader.SetVector3("ForwardVector", SpotLightComponent.ForwardVector);


            SpotLightingShader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.ColorId);


            SpotLightingShader.SetInt("NormalTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.NormalId);


            SpotLightingShader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, GloblaBuffer.DepthId);
            SpotLightingShader.SetInt("ShadowMapTexture", 3);

            gl.ActiveTexture(GLEnum.Texture3);
            gl.BindTexture(GLEnum.Texture2D, SpotLightComponent.ShadowMapTextureID);

            SpotLightingShader.SetVector3("LightLocation", SpotLightComponent.WorldLocation);
            SpotLightingShader.SetVector3("LightColor", SpotLightComponent._Color);

            SpotLightingShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);


        }
        SpotLightingShader.UnUse();
    }
}

struct DeferredVertex
{
    public Vector3 Location;
    public Vector2 TexCoord;
}
