﻿using Spark.Engine.Assets;
using Spark.Engine.Components;
using System.Drawing;
using Silk.NET.OpenGL;
using static Spark.Engine.StaticEngine;
using Shader = Spark.Engine.Assets.Shader;
using static Spark.Engine.Components.CameraComponent;
using System.Numerics;
using Spark.Util;
using Spark.Engine.Render.Buffer;
using SharpGLTF.Schema2;
using Texture = Spark.Engine.Assets.Texture;

namespace Spark.Engine.Render.Renderer;

public class DeferredSceneRenderer : IRenderer
{
    RenderBuffer GlobalBuffer;
    Shader BaseShader;
    Shader DirectionalLightingShader;
    Shader SpotLightingShader;
    Shader PointLightingShader;

    Shader DLShadowMapShader;
    Shader SpotShadowMapShader;
    Shader SkyboxShader;
    Shader PontLightShadowShader;
    Shader BloomPreShader;
    Shader BloomShader;
    Shader RenderToCamera;
    Shader ScreenSpaceReflectionShader;
    Shader BackFaceDepthShader;
    Shader HISMShader;
    Shader DecalShader;
    Shader DecalPostShader;
    Shader SSAOShader;
    RenderBuffer PostProcessBuffer1;
    RenderBuffer PostProcessBuffer2;
    RenderBuffer PostProcessBuffer3;
    RenderBuffer SceneBackFaceDepthBuffer;
    World World { get; set; }

    Texture NoiseTexture;
    List<Vector3> HalfSpherical = new List<Vector3>();

    uint PostProcessVAO = 0;
    uint PostProcessVBO = 0;
    uint PostProcessEBO = 0;

    public DeferredSceneRenderer(World world)
    {
        World = world;
        BaseShader = new Shader("/Shader/Deferred/Base");
        DirectionalLightingShader = new Shader("/Shader/Deferred/DirectionalLighting");
        SpotLightingShader = new Shader("/Shader/Deferred/SpotLighting");
        PointLightingShader = new Shader("/Shader/Deferred/PointLighting");
        DLShadowMapShader = new Shader("/Shader/ShadowMap/DirectionLightShadow");
        SpotShadowMapShader = new Shader("/Shader/ShadowMap/SpotLightShadow");
        PontLightShadowShader = new Shader("/Shader/ShadowMap/PointLightShadow");
        BloomPreShader = new Shader("/Shader/Deferred/BloomPre");
        BloomShader = new Shader("/Shader/Deferred/Bloom");
        SkyboxShader = new Shader("/Shader/Skybox");
        RenderToCamera = new Shader("/Shader/Deferred/RenderToCamera");
        ScreenSpaceReflectionShader = new Shader("/Shader/Deferred/ssr");
        BackFaceDepthShader = new Shader("/Shader/Deferred/BackFaceDepth");
        HISMShader = new Shader("/Shader/Deferred/Dynamicbatching");
        DecalShader = new Shader("/Shader/Deferred/Decal");
        DecalPostShader = new Shader("/Shader/Deferred/DecalPost");
        SSAOShader = new Shader("/Shader/Deferred/SSAO");
        GlobalBuffer = new RenderBuffer(Engine.Instance.WindowSize.X, Engine.Instance.WindowSize.Y, 3);
        PostProcessBuffer1 = new RenderBuffer(Engine.Instance.WindowSize.X, Engine.Instance.WindowSize.Y, 1);
        PostProcessBuffer2 = new RenderBuffer(Engine.Instance.WindowSize.X, Engine.Instance.WindowSize.Y, 1);
        PostProcessBuffer3 = new RenderBuffer(Engine.Instance.WindowSize.X, Engine.Instance.WindowSize.Y, 1);
        SceneBackFaceDepthBuffer = new RenderBuffer(Engine.Instance.WindowSize.X, Engine.Instance.WindowSize.Y, 0);

        NoiseTexture = Texture.CreateNoiseTexture(4, 4);
        InitRender();
        InitSSAORender();
    }

    void InitSSAORender()
    {
        for(int i = 0; i < 64; i ++)
        {
            HalfSpherical.Add(new Vector3
            {
                X = (float)Random.Shared.NextDouble() * 2 - 1,
                Y = (float)Random.Shared.NextDouble() * 2 - 1,
                Z = (float)Random.Shared.NextDouble() * 2 - 1
            });
        }

        for (int i = 0; i < 64; i++)
        {
            SSAOShader.SetVector3($"samples[{i}]", HalfSpherical[i]);
        }
    }

    ~DeferredSceneRenderer()
    {
        if (PostProcessVAO != 0)
            gl.DeleteVertexArray(PostProcessVAO);
        if (PostProcessVBO != 0)
            gl.DeleteBuffer(PostProcessVBO);
        if (PostProcessEBO != 0)
            gl.DeleteBuffer(PostProcessEBO);

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
        gl.VertexAttribPointer(1, 2, GLEnum.Float, false, (uint)sizeof(DeferredVertex), (void*)sizeof(Vector3));
        gl.BindVertexArray(0);


    }

    public unsafe void DecalPass(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        gl.PushDebugGroup("Decal Pass");
        gl.PushDebugGroup("Decal PrePass");
        using (PostProcessBuffer1.Begin())
        {
            gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);

            Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var VPInvert);
            DecalShader.SetMatrix("VPInvert", VPInvert);

            DecalShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
            DecalShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = PostProcessBuffer1.Width / (float)PostProcessBuffer1.BufferWidth,
                    Y = PostProcessBuffer1.Height / (float)PostProcessBuffer1.BufferHeight
                });
            DecalShader.SetInt("DepthTexture", 3);
            gl.ActiveTexture(GLEnum.Texture3);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);

            foreach (var DecalComponent in World.CurrentLevel.DecalComponents)
            {
                if (DecalComponent.Material == null)
                    continue;
                DecalShader.SetMatrix("ModelTransform", DecalComponent.WorldTransform);
                DecalComponent.Material.Diffuse.Use(0);
                gl.BindVertexArray(PostProcessVAO);
                gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);

            }
        }
        gl.PopDebugGroup();

        gl.PushDebugGroup("Decal PostPass");
        using (GlobalBuffer.Begin())
        {
            gl.DepthMask(false);
            DecalPostShader.Use();
            DecalPostShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = GlobalBuffer.Width / (float)GlobalBuffer.BufferWidth,
                    Y = GlobalBuffer.Height / (float)GlobalBuffer.BufferHeight
                });

            DecalPostShader.SetInt("DecalTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer1.GBufferIds[0]);
            DecalPostShader.SetInt("DecalDepthTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer1.DepthId);

            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);

            gl.DepthMask(true);
        }
        gl.PopDebugGroup();
        gl.PopDebugGroup();
    }
    public void Render(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;

        gl.PushDebugGroup("Init Buffers");
        GlobalBuffer.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        PostProcessBuffer1.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        PostProcessBuffer2.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        PostProcessBuffer3.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        SceneBackFaceDepthBuffer.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        gl.PopDebugGroup();


        gl.PushDebugGroup("Init Status");
        gl.Enable(GLEnum.CullFace);
        gl.CullFace(GLEnum.Back);
        gl.Enable(GLEnum.DepthTest);
        gl.PopDebugGroup();

        // 生成ShadowMap
        DepthPass(DeltaTime);
        BasePass(DeltaTime);
        DecalPass(DeltaTime);
        AOPass(DeltaTime);
        using (PostProcessBuffer1.Begin())
        {
            gl.PushDebugGroup("Lighting Pass");
            // 延迟光照
            LightingPass(DeltaTime);
            gl.PopDebugGroup();
            gl.PushDebugGroup("Skybox Pass");
            // 天空盒
            SkyboxPass(DeltaTime);
            gl.PopDebugGroup();
        }
        gl.PushDebugGroup("PostProcess Pass");
        // 后处理
        PostProcessPass(DeltaTime);
        gl.PopDebugGroup();
        // 渲染到摄像机的RenderTarget上
        RenderToCameraRenderTarget(DeltaTime);
    }

    private void BackFaceDepthPass(double DeltaTime)
    {
        using(SceneBackFaceDepthBuffer.Begin())
        {
            BackFaceDepthShader.Use();
            gl.Enable(EnableCap.DepthTest);
            gl.ClearColor(Color.Black);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            gl.Enable(GLEnum.CullFace);
            gl.CullFace(GLEnum.Front);

            gl.Disable(EnableCap.Blend);
            if (CurrentCameraComponent != null)
            {
                BackFaceDepthShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
                BackFaceDepthShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
            }
            foreach (var component in World.CurrentLevel.PrimitiveComponents)
            {
                if (component.IsDestoryed == false)
                {
                    BackFaceDepthShader.SetMatrix("ModelTransform", component.WorldTransform);
                    component.Render(DeltaTime);
                }
            }

            BackFaceDepthShader.UnUse();
            gl.CullFace(GLEnum.Back);
        }
    }
    private void PostProcessPass(double DeltaTime)
    {
        gl.PushDebugGroup("Bloom Effect");
        BloomPass(DeltaTime);
        gl.PopDebugGroup();
        gl.PushDebugGroup("ScreenSpaceReflection");
        ScreenSpaceReflection(DeltaTime);
        gl.PopDebugGroup();
    }
    private void SkyboxPass(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        SkyboxShader.Use();
        Matrix4x4 View = Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.Zero + CurrentCameraComponent.ForwardVector, CurrentCameraComponent.UpVector);
        Matrix4x4 Projection = Matrix4x4.CreatePerspectiveFieldOfView(CurrentCameraComponent.FieldOfView.DegreeToRadians(), Engine.Instance.WindowSize.X / (float)Engine.Instance.WindowSize.Y, 0.1f, 100f);
        SkyboxShader.SetMatrix("view", View);
        SkyboxShader.SetMatrix("projection", Projection);

        SkyboxShader.SetInt("NormalTexture", 1);
        gl.ActiveTexture(GLEnum.Texture1);
        gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);

        SkyboxShader.SetVector2("BufferSize", new Vector2(GlobalBuffer.BufferWidth, GlobalBuffer.BufferHeight));
        SkyboxShader.SetVector2("ScreenSize", new Vector2(GlobalBuffer.Width, GlobalBuffer.Height));
        SkyboxShader.SetInt("skybox", 0);
        World.CurrentLevel.CurrentSkybox?.RenderSkybox(DeltaTime);
        SkyboxShader.UnUse();

    }

    private unsafe void AOPass(double deltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        gl.PushDebugGroup("SSAO Pass");
        using (PostProcessBuffer2.Begin())
        {
            gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
           
            SSAOShader.SetVector2("TexCoordScale",
            new Vector2
            {
                X = PostProcessBuffer2.Width / (float)PostProcessBuffer2.BufferWidth,
                Y = PostProcessBuffer2.Height / (float)PostProcessBuffer2.BufferHeight
            });

            SSAOShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
            SSAOShader.SetMatrix("InvertProjectionTransform", CurrentCameraComponent.Projection.Inverse());


            SSAOShader.SetInt("NormalTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);

            SSAOShader.SetInt("DepthTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);

            SSAOShader.SetInt("NoiseTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, NoiseTexture.TextureId);




            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);


        }
        gl.PopDebugGroup();
    }
    private void DepthPass(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;

        gl.PushDebugGroup("Shadow Depth Pass");
        gl.Enable(GLEnum.DepthTest);
        gl.Enable(GLEnum.CullFace);
        gl.CullFace(GLEnum.Front);


        DLShadowMapShader.Use();
        foreach (var DirectionalLight in World.CurrentLevel.DirectionLightComponents)
        {
            var LightLocation = CurrentCameraComponent.RelativeLocation - DirectionalLight.ForwardVector * 20;
            var View = Matrix4x4.CreateLookAt(LightLocation, LightLocation + DirectionalLight.ForwardVector, DirectionalLight.UpVector);
            var Projection = Matrix4x4.CreateOrthographic(100, 100, 1.0f, 100f);
            gl.Viewport(new Rectangle(0, 0, DirectionalLight.ShadowMapSize.X, DirectionalLight.ShadowMapSize.Y));
            gl.BindFramebuffer(GLEnum.Framebuffer, DirectionalLight.ShadowMapFrameBufferID);
            gl.Clear(ClearBufferMask.DepthBufferBit);
            DLShadowMapShader.SetMatrix("ViewTransform", View);
            DLShadowMapShader.SetMatrix("ProjectionTransform", Projection);
            foreach (var component in World.CurrentLevel.PrimitiveComponents)
            {
                if (component == null)
                    continue;
                if (component.IsDestoryed == false)
                {
                    DLShadowMapShader.SetMatrix("ModelTransform", component.WorldTransform);
                    DLShadowMapShader.SetMatrix("NormalTransform", component.NormalTransform);
                    component.Render(DeltaTime);
                }
            }

            gl.BindFramebuffer(GLEnum.Framebuffer, 0);
            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));
        }
        DLShadowMapShader.UnUse();

        SpotLightingShader.Use();
        foreach (var SpotLight in World.CurrentLevel.SpotLightComponents)
        {
            var View = Matrix4x4.CreateLookAt(SpotLight.WorldLocation, SpotLight.WorldLocation + SpotLight.ForwardVector, SpotLight.UpVector);
            var Projection = Matrix4x4.CreatePerspectiveFieldOfView(170F.DegreeToRadians(), 1, 1F, 100);
            gl.Viewport(new Rectangle(0, 0, SpotLight.ShadowMapSize.X, SpotLight.ShadowMapSize.Y));
            gl.BindFramebuffer(GLEnum.Framebuffer, SpotLight.ShadowMapFrameBufferID);
            gl.Clear(ClearBufferMask.DepthBufferBit);

            SpotLightingShader.SetMatrix("ViewTransform", View);
            SpotLightingShader.SetMatrix("ProjectionTransform", Projection);

            foreach (var component in World.CurrentLevel.PrimitiveComponents)
            {
                if (component == null)
                    continue;
                if (component.IsDestoryed == false)
                {
                    SpotLightingShader.SetMatrix("ModelTransform", component.WorldTransform);
                    SpotLightingShader.SetMatrix("NormalTransform", component.NormalTransform);
                    component.Render(DeltaTime);
                }
            }

            gl.BindFramebuffer(GLEnum.Framebuffer, 0);
            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));

        }


        SpotLightingShader.UnUse();


        PontLightShadowShader.Use();
        foreach (var PointLight in World.CurrentLevel.PointLightComponents)
        {
            gl.Viewport(new Rectangle(0, 0, PointLight.ShadowMapSize.X, PointLight.ShadowMapSize.Y));
            gl.BindFramebuffer(GLEnum.Framebuffer, PointLight.ShadowMapFrameBufferID);
            gl.Clear(ClearBufferMask.DepthBufferBit);
            var Projection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), 1, 1, 1000);
            Matrix4x4[] ShadowMatrices = new Matrix4x4[6];
            ShadowMatrices[0] = Matrix4x4.CreateLookAt(PointLight.WorldLocation, PointLight.WorldLocation + PointLight.RightVector, -PointLight.UpVector) * Projection;
            ShadowMatrices[1] = Matrix4x4.CreateLookAt(PointLight.WorldLocation, PointLight.WorldLocation - PointLight.RightVector, -PointLight.UpVector) * Projection;
            ShadowMatrices[2] = Matrix4x4.CreateLookAt(PointLight.WorldLocation, PointLight.WorldLocation + PointLight.UpVector, -PointLight.ForwardVector) * Projection;
            ShadowMatrices[3] = Matrix4x4.CreateLookAt(PointLight.WorldLocation, PointLight.WorldLocation - PointLight.UpVector, PointLight.ForwardVector) * Projection;
            ShadowMatrices[4] = Matrix4x4.CreateLookAt(PointLight.WorldLocation, PointLight.WorldLocation - PointLight.ForwardVector, -PointLight.UpVector) * Projection;
            ShadowMatrices[5] = Matrix4x4.CreateLookAt(PointLight.WorldLocation, PointLight.WorldLocation + PointLight.ForwardVector, -PointLight.UpVector) * Projection;
            for (var i = 0; i < 6; i++)
            {
                PontLightShadowShader.SetMatrix("shadowMatrices[" + i + "]", ShadowMatrices[i]);
            }


            PontLightShadowShader.SetVector3("LightLocation", PointLight.WorldLocation);
            PontLightShadowShader.SetFloat("FarPlan", 1000);
            foreach (var component in World.CurrentLevel.PrimitiveComponents)
            {
                if (component == null)
                    continue;
                if (component.IsDestoryed == false)
                {
                    PontLightShadowShader.SetMatrix("ModelTransform", component.WorldTransform);
                    component.Render(DeltaTime);
                }
            }

            gl.BindFramebuffer(GLEnum.Framebuffer, 0);
            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));
        }
        PontLightShadowShader.UnUse();




        gl.CullFace(GLEnum.Back);
        gl.PopDebugGroup();
    }
    private void BasePass(double DeltaTime)
    {
        // 生成GBuffer
        gl.PushDebugGroup("Base Pass");
        using (GlobalBuffer.Begin())
        {
            gl.Enable(EnableCap.DepthTest);
            gl.ClearColor(Color.Black);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            gl.Enable(GLEnum.CullFace);
            gl.CullFace(GLEnum.Back);
            gl.Disable(EnableCap.Blend);
            if (CurrentCameraComponent != null)
            {
                BaseShader.SetInt("Diffuse", 0);
                BaseShader.SetInt("Normal", 1);
                BaseShader.SetInt("Parallax", 2);
                BaseShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
                BaseShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
                BaseShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
                foreach (var component in World.CurrentLevel.PrimitiveComponents)
                {
                    if (component.IsDestoryed == false)
                    {
                        BaseShader.SetMatrix("ModelTransform", component.WorldTransform);
                        BaseShader.SetMatrix("NormalTransform", component.NormalTransform);
                        component.Render(DeltaTime);
                    }
                }

                gl.Disable(GLEnum.CullFace);
                gl.PushDebugGroup("InstancedStaticMesh Render");
                HISMShader.SetInt("Diffuse", 0);
                HISMShader.SetInt("Normal", 1);
                HISMShader.SetInt("Parallax", 2);
                HISMShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
                HISMShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
                HISMShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
                foreach (var hism in World.CurrentLevel.ISMComponents)
                {
                    hism.RenderHISM(CurrentCameraComponent, DeltaTime);
                }
                gl.PopDebugGroup();
            }
        }
        gl.PopDebugGroup();
    }

    private unsafe void RenderToCameraRenderTarget(double DeltaTime)
    {
        if (LastPostProcessBuffer == null)
            return;
        if (CurrentCameraComponent == null)
            return;
        CurrentCameraComponent.RenderTarget.RenderTo(() =>
        {
            gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
            RenderToCamera.Use();
            RenderToCamera.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = GlobalBuffer.Width / (float)GlobalBuffer.BufferWidth,
                    Y = GlobalBuffer.Height / (float)GlobalBuffer.BufferHeight
                });
            RenderToCamera.SetFloat("Brightness", 0.0f);
            RenderToCamera.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, LastPostProcessBuffer.GBufferIds[0]);


            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);
            RenderToCamera.UnUse();

        });

    }

    private unsafe void ScreenSpaceReflection(double DeltaTime)
    {
        if (CurrentCameraComponent == null) return;
        BackFaceDepthPass(DeltaTime);
        using(PostProcessBuffer2.Begin())
        {
            gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
            ScreenSpaceReflectionShader.Use();
            ScreenSpaceReflectionShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = PostProcessBuffer1.Width / (float)PostProcessBuffer1.BufferWidth,
                    Y = PostProcessBuffer1.Height / (float)PostProcessBuffer1.BufferHeight
                });

            Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var VPInvert);
            ScreenSpaceReflectionShader.SetMatrix("VPInvert", VPInvert);
            ScreenSpaceReflectionShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
            ScreenSpaceReflectionShader.SetMatrix("View", CurrentCameraComponent.View);
            ScreenSpaceReflectionShader.SetMatrix("Projection", CurrentCameraComponent.Projection);

            ScreenSpaceReflectionShader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer1.GBufferIds[0]);
            ScreenSpaceReflectionShader.SetInt("NormalTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);
            ScreenSpaceReflectionShader.SetInt("ReflectionTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[2]);
            ScreenSpaceReflectionShader.SetInt("DepthTexture", 3);
            gl.ActiveTexture(GLEnum.Texture3);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);
            if (World.CurrentLevel.CurrentSkybox != null)
            {
                if (World.CurrentLevel.CurrentSkybox.SkyboxCube != null)
                {
                    ScreenSpaceReflectionShader.SetInt("SkyboxTexture", 4);
                    World.CurrentLevel.CurrentSkybox.SkyboxCube.Use(4);
                }
            }
            ScreenSpaceReflectionShader.SetInt("BackDepthTexture", 5);
            gl.ActiveTexture(GLEnum.Texture5);
            gl.BindTexture(GLEnum.Texture2D, SceneBackFaceDepthBuffer.DepthId);

            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);
            ScreenSpaceReflectionShader.UnUse();
        }

        LastPostProcessBuffer = PostProcessBuffer2;

    }
    private void LightingPass(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;

        // 清除背景颜色
        gl.ClearColor(Color.Black);
        gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
        gl.Disable(EnableCap.DepthTest);
        gl.BlendEquation(GLEnum.FuncAdd);
        gl.BlendFunc(GLEnum.One, GLEnum.One);
        gl.Enable(EnableCap.Blend);

        // 定向光
        DirectionalLight();
        // 点光源
        PointLight();
        // 聚光
        SpotLight();
        gl.Disable(EnableCap.Blend);

    }


    private unsafe void DirectionalLight()
    {
        if (CurrentCameraComponent == null)
            return;
        gl.PushDebugGroup("DirectionalLight Pass");
        DirectionalLightingShader.Use();
        foreach (var DirectionalLight in World.CurrentLevel.DirectionLightComponents)
        {
            var LightInfo = DirectionalLight.LightInfo;
            Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var VPInvert);
            DirectionalLightingShader.SetMatrix("VPInvert", VPInvert);

            var LightLocation = CurrentCameraComponent.RelativeLocation - DirectionalLight.ForwardVector * 20;
            var View = Matrix4x4.CreateLookAt(LightLocation, CurrentCameraComponent.WorldLocation + DirectionalLight.ForwardVector * -1, DirectionalLight.UpVector);
            var Projection = Matrix4x4.CreateOrthographic(100, 100, 1.0f, 100f);
            
            var WorldToLight = View * Projection;
            DirectionalLightingShader.SetMatrix("WorldToLight", WorldToLight);
            DirectionalLightingShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = GlobalBuffer.Width / (float)GlobalBuffer.BufferWidth,
                    Y = GlobalBuffer.Height / (float)GlobalBuffer.BufferHeight
                });


            DirectionalLightingShader.SetFloat("AmbientStrength", DirectionalLight.AmbientStrength);
            DirectionalLightingShader.SetFloat("LightStrength", DirectionalLight.LightStrength);
            DirectionalLightingShader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[1]);


            DirectionalLightingShader.SetInt("NormalTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);


            DirectionalLightingShader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);

            DirectionalLightingShader.SetInt("ShadowMapTexture", 3);
            gl.ActiveTexture(GLEnum.Texture3);
            gl.BindTexture(GLEnum.Texture2D, DirectionalLight.ShadowMapTextureID);


            DirectionalLightingShader.SetInt("SSAOTexture", 4);
            gl.ActiveTexture(GLEnum.Texture4);
            gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer2.GBufferIds[0]);


            DirectionalLightingShader.SetVector3("LightDirection", LightInfo.Direction);
            DirectionalLightingShader.SetVector3("LightColor", LightInfo.Color);

            DirectionalLightingShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);


        }
        DirectionalLightingShader.UnUse();
        gl.PopDebugGroup();
    }

    private unsafe void BloomPass(double DeltaTime)
    {
        if (CurrentCameraComponent == null) return;
        gl.Disable(EnableCap.DepthTest);
        using(PostProcessBuffer2.Begin())
        {
            gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
            BloomPreShader.Use();
            BloomPreShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = GlobalBuffer.Width / (float)GlobalBuffer.BufferWidth,
                    Y = GlobalBuffer.Height / (float)GlobalBuffer.BufferHeight
                });
            BloomPreShader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer1.GBufferIds[0]);

            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);
            BloomPreShader.UnUse();
        }

        RenderBuffer[] buffer = new RenderBuffer[2] {
            PostProcessBuffer3,
            PostProcessBuffer2,
        };
        for (int i = 0; i < 2; i++)
        {
            int next = i == 1 ? 0 : 1;
            using(buffer[i].Begin())
            {
                gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
                BloomShader.Use();
                BloomShader.SetVector2("TexCoordScale",
                    new Vector2
                    {
                        X = PostProcessBuffer1.Width / (float)PostProcessBuffer1.BufferWidth,
                        Y = PostProcessBuffer1.Height / (float)PostProcessBuffer1.BufferHeight
                    });
                BloomShader.SetInt("horizontal", i);
                BloomShader.SetInt("ColorTexture", 0);
                gl.ActiveTexture(GLEnum.Texture0);
                gl.BindTexture(GLEnum.Texture2D, buffer[next].GBufferIds[0]);

                gl.BindVertexArray(PostProcessVAO);
                gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
                gl.ActiveTexture(GLEnum.Texture0);
                BloomShader.UnUse();
            }
        }
        using (PostProcessBuffer1.Begin()) 
        {
            gl.Enable(EnableCap.Blend);
            gl.BlendEquation(GLEnum.FuncAdd);
            RenderToCamera.Use();
            RenderToCamera.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = PostProcessBuffer1.Width / (float)PostProcessBuffer1.BufferWidth,
                    Y = PostProcessBuffer1.Height / (float)PostProcessBuffer1.BufferHeight
                });
            RenderToCamera.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer2.GBufferIds[0]);

            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);
            RenderToCamera.UnUse();

        }
        LastPostProcessBuffer = PostProcessBuffer1;

    }

    RenderBuffer? LastPostProcessBuffer = null;
    public unsafe void PointLight()
    {
        if (CurrentCameraComponent == null)
            return;
        PointLightingShader.Use();
        foreach (var PointLightComponent in World.CurrentLevel.PointLightComponents)
        {
            Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var VPInvert);
            PointLightingShader.SetMatrix("VPInvert", VPInvert);

            PointLightingShader.SetFloat("LightStrength", PointLightComponent.LightStrength);
            PointLightingShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = GlobalBuffer.Width / (float)GlobalBuffer.BufferWidth,
                    Y = GlobalBuffer.Height / (float)GlobalBuffer.BufferHeight
                });

            PointLightingShader.SetFloat("Constant", PointLightComponent.Constant);
            PointLightingShader.SetFloat("Linear", PointLightComponent.Linear);
            PointLightingShader.SetFloat("Quadratic", PointLightComponent.Quadratic);

            PointLightingShader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[1]);


            PointLightingShader.SetInt("NormalTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);


            PointLightingShader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);

            PointLightingShader.SetInt("ShadowMapTextue", 3);
            gl.ActiveTexture(GLEnum.Texture3);
            gl.BindTexture(GLEnum.TextureCubeMap, PointLightComponent.ShadowMapTextureID);

            PointLightingShader.SetInt("SSAOTexture", 4);
            gl.ActiveTexture(GLEnum.Texture4);
            gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer2.GBufferIds[0]);

            PointLightingShader.SetFloat("FarPlan", 1000);

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
            SpotLightingShader.SetFloat("LightStrength", SpotLightComponent.LightStrength);
            Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var VPInvert);
            SpotLightingShader.SetMatrix("VPInvert", VPInvert);


            SpotLightingShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = GlobalBuffer.Width / (float)GlobalBuffer.BufferWidth,
                    Y = GlobalBuffer.Height / (float)GlobalBuffer.BufferHeight
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
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[1]);


            SpotLightingShader.SetInt("NormalTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);


            SpotLightingShader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);
            SpotLightingShader.SetInt("ShadowMapTexture", 3);

            SpotLightingShader.SetInt("SSAOTexture", 4);
            gl.ActiveTexture(GLEnum.Texture4);
            gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer2.GBufferIds[0]);


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

public struct DeferredVertex
{
    public Vector3 Location;
    public Vector2 TexCoord;
}
