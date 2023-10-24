using Spark.Engine.Assets;
using Spark.Engine.Components;
using System.Drawing;
using Silk.NET.OpenGLES;
using static Spark.Engine.StaticEngine;
using Shader = Spark.Engine.Assets.Shader;
using static Spark.Engine.Components.CameraComponent;
using System.Numerics;
using Spark.Util;
using SharpGLTF.Schema2;
using Texture = Spark.Engine.Assets.Texture;
namespace Spark.Engine.Render.Renderer;

public class DeferredSceneRenderer : IRenderer
{
    RenderTarget GlobalBuffer;
    Shader StaticMeshBaseShader;
    Shader DirectionalLightingShader;
    Shader SpotLightingShader;
    Shader PointLightingShader;

    Shader DLShadowMapShader;
    Shader InstanceDLShadowMapShader;
    Shader SpotShadowMapShader;
    Shader InstanceSpotShadowMapShader;
    Shader InstancePointLightingShader;
    Shader SkyboxShader;
    Shader PointLightShadowShader;
    Shader BloomPreShader;
    Shader BloomShader;
    Shader RenderToCamera;
    Shader ScreenSpaceReflectionShader;
    Shader BackFaceDepthShader;
    Shader HISMShader;
    Shader DecalShader;
    Shader DecalPostShader;
    Shader SSAOShader;

    Shader SkeletakMeshDLShadowMapShader;
    Shader SkeletakMeshSpotShadowMapShader;
    Shader SkeletakMeshPointLightingShader;

    Shader SkeletalMeshBaseShader;


    Shader DebugShader;
    RenderTarget? PostProcessBuffer1;
    RenderTarget? PostProcessBuffer2;
    RenderTarget? PostProcessBuffer3;
    World World { get; set; }

    Texture NoiseTexture;
    List<Vector3> HalfSpherical = new List<Vector3>();

    uint PostProcessVAO = 0;
    uint PostProcessVBO = 0;
    uint PostProcessEBO = 0;
    bool IsMobile = false;
    bool IsMicroGBuffer = false;
    public DeferredSceneRenderer(World world)
    {
        World = world;
        List<string> Macros = new List<string>();
        if (World.Engine.IsMobile)
        {
            IsMobile = true;
            IsMicroGBuffer = true;
        }
        if (IsMobile == true)
        {
            Macros.Add("_MOBILE_");
        }
        if (IsMicroGBuffer == true)
        {
           Macros.Add("_MICRO_GBUFFER_");
        }
        // Base Pass
        StaticMeshBaseShader = new Shader("/Shader/Deferred/Base/Base", Macros);
        SkeletalMeshBaseShader = new Shader("/Shader/Deferred/Base/BaseSkeletalMesh", Macros);
        HISMShader = new Shader("/Shader/Deferred/Base/BaseInstance", Macros);
        DirectionalLightingShader = new Shader("/Shader/Deferred/Light/DirectionalLighting", Macros);
        SpotLightingShader = new Shader("/Shader/Deferred/Light/SpotLighting", Macros);
        PointLightingShader = new Shader("/Shader/Deferred/Light/PointLighting", Macros);
        DLShadowMapShader = new Shader("/Shader/ShadowMap/DirectionLightShadow", Macros);
        SpotShadowMapShader = new Shader("/Shader/ShadowMap/SpotLightShadow", Macros);
        PointLightShadowShader = new Shader("/Shader/ShadowMap/PointLightShadow", Macros);
        BloomPreShader = new Shader("/Shader/Deferred/BloomPre", Macros);
        BloomShader = new Shader("/Shader/Deferred/Bloom", Macros);
        SkyboxShader = new Shader("/Shader/Skybox", Macros);
        RenderToCamera = new Shader("/Shader/Deferred/RenderToCamera", Macros);
        ScreenSpaceReflectionShader = new Shader("/Shader/Deferred/ssr", Macros);
        BackFaceDepthShader = new Shader("/Shader/Deferred/BackFaceDepth", Macros);
        DecalShader = new Shader("/Shader/Deferred/Decal", Macros);
        DecalPostShader = new Shader("/Shader/Deferred/DecalPost", Macros);
        SSAOShader = new Shader("/Shader/Deferred/SSAO", Macros);

        InstancePointLightingShader = new Shader("/Shader/ShadowMap/Instance/PointLightShadow", Macros);
        InstanceDLShadowMapShader = new Shader("/Shader/ShadowMap/Instance/DirectionLightShadow", Macros);
        InstanceSpotShadowMapShader = new Shader("/Shader/ShadowMap/Instance/SpotLightShadow", Macros);
        SkeletakMeshDLShadowMapShader = new Shader("/Shader/ShadowMap/SkeletalMesh/DirectionLightShadow", Macros);
        SkeletakMeshSpotShadowMapShader = new Shader("/Shader/ShadowMap/SkeletalMesh/SpotLightShadow", Macros);
        SkeletakMeshPointLightingShader = new Shader("/Shader/ShadowMap/SkeletalMesh/PointLightShadow", Macros);

        DebugShader = new Shader("/Shader/debugLine");
        if (IsMicroGBuffer == true)
        {
            GlobalBuffer = new RenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 1);
        }
        else
        {
            GlobalBuffer = new RenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 2);
        }
        if (IsMobile == false)
        {
            PostProcessBuffer1 = new RenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 1);
            PostProcessBuffer2 = new RenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 1);
            PostProcessBuffer3 = new RenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 1);

        }
        // SceneBackFaceDepthBuffer = new RenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 0);
        
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
        if (PostProcessBuffer1 == null)
            return;
        if (IsMobile == true)
            return;
        gl.PushDebugGroup("Decal Pass");
        gl.PushDebugGroup("Decal PrePass");
        using (PostProcessBuffer1.Begin())
        {
            gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);

            DecalShader.SetInt("BaseColorTexture", 0);
            DecalShader.SetInt("NormalTexture", 1);
            DecalShader.SetInt("CustomTexture", 2);
            DecalShader.SetInt("DepthTexture", 3);
            DecalShader.SetInt("GBuffer1", 4);
            if (IsMicroGBuffer == false)
            {
                DecalShader.SetInt("GBuffer2", 5);
            }
            Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var VPInvert);
            DecalShader.SetMatrix("VPInvert", VPInvert);

            DecalShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
            DecalShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = PostProcessBuffer1.Width / (float)PostProcessBuffer1.BufferWidth,
                    Y = PostProcessBuffer1.Height / (float)PostProcessBuffer1.BufferHeight
                });
            gl.ActiveTexture(GLEnum.Texture3);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);
            gl.ActiveTexture(GLEnum.Texture4);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);
            if (IsMicroGBuffer == false)
            {
                gl.ActiveTexture(GLEnum.Texture5);
                gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[1]);
            }

            foreach (var DecalComponent in World.CurrentLevel.DecalComponents)
            {
                if (DecalComponent.Material == null)
                    continue;
                DecalShader.SetMatrix("ModelTransform", DecalComponent.WorldTransform);
                DecalComponent.Material.Use();
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
        if (IsMobile == false)
        {
            PostProcessBuffer1?.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
            PostProcessBuffer2?.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
            PostProcessBuffer3?.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        }
        //SceneBackFaceDepthBuffer?.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
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
        if (IsMobile == false)
        {
            AOPass(DeltaTime);
        }
        RenderTarget? LightRT = null;
        if (PostProcessBuffer1 == null)
            LightRT = CurrentCameraComponent.RenderTarget;
        else
            LightRT = PostProcessBuffer1;
        using (LightRT.Begin())
        {
            gl.PushDebugGroup("Lighting Pass");
            // 延迟光照
            LightingPass(DeltaTime);
            gl.PopDebugGroup();
            gl.PushDebugGroup("Skybox Pass");
            // 天空盒
            SkyboxPass(DeltaTime);
            gl.PopDebugGroup();
            LastPostProcessBuffer = PostProcessBuffer1;
        }
        gl.PushDebugGroup("PostProcess Pass");
        // 后处理
        PostProcessPass(DeltaTime);
        gl.PopDebugGroup();

        if (IsMobile == false)
        {
            // 渲染到摄像机的RenderTarget上
            RenderToCameraRenderTarget(DeltaTime);
        }
    }

   
    private void PostProcessPass(double DeltaTime)
    {
        if (IsMobile == true)
            return;
        gl.PushDebugGroup("Bloom Effect");
        BloomPass(DeltaTime);
        gl.PopDebugGroup();
        gl.PushDebugGroup("ScreenSpaceReflection");
        // ScreenSpaceReflection(DeltaTime);
        gl.PopDebugGroup();
    }
    private void SkyboxPass(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        SkyboxShader.Use();
        Matrix4x4 View = Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.Zero + CurrentCameraComponent.ForwardVector, CurrentCameraComponent.UpVector);
        Matrix4x4 Projection = Matrix4x4.CreatePerspectiveFieldOfView(CurrentCameraComponent.FieldOfView.DegreeToRadians(), World.Engine.WindowSize.X / (float)World.Engine.WindowSize.Y, 0.1f, 100f);
        SkyboxShader.SetMatrix("view", View);
        SkyboxShader.SetMatrix("projection", Projection);

        SkyboxShader.SetInt("DepthTexture", 1);
        gl.ActiveTexture(GLEnum.Texture1);
        gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);

        SkyboxShader.SetVector2("BufferSize", new Vector2(GlobalBuffer.BufferWidth, GlobalBuffer.BufferHeight));
        SkyboxShader.SetVector2("ScreenSize", new Vector2(GlobalBuffer.Width, GlobalBuffer.Height));
        SkyboxShader.SetInt("skybox", 0);
        World.CurrentLevel.CurrentSkybox?.RenderSkybox(DeltaTime);
        SkyboxShader.UnUse();

    }
    private Plane[] Planes = new Plane[6];
    private unsafe void AOPass(double deltaTime)
    {
        if (PostProcessBuffer2 == null)
            return;
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

            if (IsMicroGBuffer == false)
            {
                SSAOShader.SetInt("CustomBuffer", 1);
                gl.ActiveTexture(GLEnum.Texture1);
                gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[1]);
            }
            else
            {
                SSAOShader.SetInt("CustomBuffer", 1);
                gl.ActiveTexture(GLEnum.Texture1);
                gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);
            }

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

        gl.PushDebugGroup("ShadowMap Pass");
        gl.Enable(GLEnum.DepthTest);
        gl.Enable(GLEnum.CullFace);
        gl.CullFace(GLEnum.Front);

        SpotShadowMap(DeltaTime);
        PointLightShadowMap(DeltaTime);
        DirectionLightShadowMap(DeltaTime);


        gl.CullFace(GLEnum.Back);
        gl.PopDebugGroup();
    }

    private void SpotShadowMap(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;

        gl.PushDebugGroup("SpotLight");
        SpotShadowMapShader.Use();
        foreach (var SpotLight in World.CurrentLevel.SpotLightComponents)
        {
            var View = Matrix4x4.CreateLookAt(SpotLight.WorldLocation, SpotLight.WorldLocation + SpotLight.ForwardVector, SpotLight.UpVector);
            var Projection = Matrix4x4.CreatePerspectiveFieldOfView(170F.DegreeToRadians(), 1, 1F, 100);
            gl.Viewport(new Rectangle(0, 0, SpotLight.ShadowMapSize.X, SpotLight.ShadowMapSize.Y));
            gl.BindFramebuffer(GLEnum.Framebuffer, SpotLight.ShadowMapFrameBufferID);
            gl.Clear(ClearBufferMask.DepthBufferBit);

            SpotShadowMapShader.SetMatrix("ViewTransform", View);
            SpotShadowMapShader.SetMatrix("ProjectionTransform", Projection);

            foreach (var component in World.CurrentLevel.StaticMeshComponents)
            {
                if (component == null)
                    continue;
                if (component.IsDestoryed == false)
                {
                    SpotShadowMapShader.SetMatrix("ModelTransform", component.WorldTransform);
                    SpotShadowMapShader.SetMatrix("NormalTransform", component.NormalTransform);
                    component.Render(DeltaTime);
                }
            }

            InstanceSpotShadowMapShader.SetMatrix("ViewTransform", View);
            InstanceSpotShadowMapShader.SetMatrix("ProjectionTransform", Projection);
            foreach (var ism in World.CurrentLevel.ISMComponents)
            {
                GetPlanes(View * Projection, ref Planes);
                if (ism is HierarchicalInstancedStaticMeshComponent hism)
                    hism.CameraCulling(Planes);
                ism.RenderISM(CurrentCameraComponent, DeltaTime);
            }

            SkeletakMeshSpotShadowMapShader.SetMatrix("ViewTransform", View);
            SkeletakMeshSpotShadowMapShader.SetMatrix("ProjectionTransform", Projection);
            foreach (var component in World.CurrentLevel.SkeletalMeshComponents)
            {
                if (component.IsDestoryed == false)
                {
                    if (component.AnimSampler != null && component.SkeletalMesh != null && component.SkeletalMesh.Skeleton != null)
                    {
                        for (int i = 0; i < component.SkeletalMesh.Skeleton.BoneList.Count; i++)
                        {
                            SkeletakMeshSpotShadowMapShader.SetMatrix($"AnimTransform[{i}]", component.AnimBuffer[i]);
                        }
                    }
                    SkeletakMeshSpotShadowMapShader.SetMatrix("ModelTransform", component.WorldTransform);
                    SkeletakMeshSpotShadowMapShader.SetMatrix("NormalTransform", component.NormalTransform);
                    component.Render(DeltaTime);
                }
            }
            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));

        }

        SpotLightingShader.UnUse();
        gl.PopDebugGroup();
    }
    private void PointLightShadowMap(double DeltaTime)
    {

        if (CurrentCameraComponent == null)
            return;
        gl.PushDebugGroup("PointLight");
        PointLightShadowShader.Use();
        foreach (var PointLightComponent in World.CurrentLevel.PointLightComponents)
        {

            var Projection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), 1, 1, 1000);
            Matrix4x4[] Views = new Matrix4x4[6];
            Views[0] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(0, 0, -1), new Vector3(0, 1, 0));
            Views[1] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(0, 0, 1), new Vector3(0, 1, 0));
            Views[2] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(-1, 0, 0), new Vector3(0, 1, 0));
            Views[3] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(1, 0, 0), new Vector3(0, 1, 0));
            Views[4] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            Views[5] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(0, -1, 0), new Vector3(0, 0, -1));



            gl.Viewport(new Rectangle(0, 0, PointLightComponent.ShadowMapSize.X, PointLightComponent.ShadowMapSize.Y));
            for (int i = 0; i < 6; i++)
            {
                gl.PushDebugGroup("face:" + i);
                gl.BindFramebuffer(GLEnum.Framebuffer, PointLightComponent.ShadowMapFrameBufferIDs[i]);
                gl.Clear(ClearBufferMask.DepthBufferBit);
                SpotShadowMapShader.SetMatrix("ViewTransform", Views[i]);
                SpotShadowMapShader.SetMatrix("ProjectionTransform", Projection);
                foreach (var component in World.CurrentLevel.StaticMeshComponents)
                {
                    if (component == null)
                        continue;
                    if (component.IsDestoryed == false)
                    {
                        PointLightShadowShader.SetMatrix("ModelTransform", component.WorldTransform);
                        component.Render(DeltaTime);
                    }
                }

                InstancePointLightingShader.SetMatrix("ViewTransform", Views[i]);
                InstancePointLightingShader.SetMatrix("ProjectionTransform", Projection);
                InstancePointLightingShader.SetInt("layer", i);
                foreach (var hism in World.CurrentLevel.ISMComponents)
                {
                    if (hism == null)
                        continue;
                    hism.RenderISM(CurrentCameraComponent, DeltaTime);
                }


                SkeletakMeshPointLightingShader.SetMatrix("ViewTransform", Views[i]);
                SkeletakMeshPointLightingShader.SetMatrix("ProjectionTransform", Projection);
                SkeletakMeshPointLightingShader.SetInt("layer", i);
                SkeletakMeshPointLightingShader.Use();
                foreach (var component in World.CurrentLevel.SkeletalMeshComponents)
                {
                    if (component.IsDestoryed == false)
                    {
                        if (component.AnimSampler != null && component.SkeletalMesh != null && component.SkeletalMesh.Skeleton != null)
                        {
                            for (int j = 0; j < component.SkeletalMesh.Skeleton.BoneList.Count; j++)
                            {
                                SkeletakMeshPointLightingShader.SetMatrix($"AnimTransform[{j}]", component.AnimBuffer[j]);
                            }
                        }
                        SkeletakMeshPointLightingShader.SetMatrix("ModelTransform", component.WorldTransform);
                        SkeletakMeshPointLightingShader.SetMatrix("NormalTransform", component.NormalTransform);
                        component.Render(DeltaTime);
                    }
                }
                gl.PopDebugGroup();
            }

            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));
        }
        PointLightShadowShader.UnUse();
        gl.PopDebugGroup();
    }
    private void DirectionLightShadowMap(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        gl.PushDebugGroup("DirectionLight");
        DLShadowMapShader.Use();
        foreach (var DirectionalLight in World.CurrentLevel.DirectionLightComponents)
        {
            var LightLocation = CurrentCameraComponent.RelativeLocation - DirectionalLight.ForwardVector * 20;
            var View = Matrix4x4.CreateLookAt(LightLocation, CurrentCameraComponent.WorldLocation + DirectionalLight.ForwardVector, DirectionalLight.UpVector);
            var Projection = Matrix4x4.CreateOrthographic(100, 100, 1.0f, 100f);
            gl.Viewport(new Rectangle(0, 0, DirectionalLight.ShadowMapSize.X, DirectionalLight.ShadowMapSize.Y));
            gl.BindFramebuffer(GLEnum.Framebuffer, DirectionalLight.ShadowMapFrameBufferID);
            gl.Clear(ClearBufferMask.DepthBufferBit);
            DLShadowMapShader.SetMatrix("ViewTransform", View);
            DLShadowMapShader.SetMatrix("ProjectionTransform", Projection);
            foreach (var component in World.CurrentLevel.StaticMeshComponents)
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

            InstanceDLShadowMapShader.SetMatrix("ViewTransform", View);
            InstanceDLShadowMapShader.SetMatrix("ProjectionTransform", Projection);
            foreach (var ism in World.CurrentLevel.ISMComponents)
            {
                GetPlanes(View * Projection, ref Planes);
                if (ism is HierarchicalInstancedStaticMeshComponent hism)
                    hism.CameraCulling(Planes);
                ism.RenderISM(CurrentCameraComponent, DeltaTime);
            }


            SkeletakMeshDLShadowMapShader.SetMatrix("ViewTransform", View);
            SkeletakMeshDLShadowMapShader.SetMatrix("ProjectionTransform", Projection);
            foreach (var component in World.CurrentLevel.SkeletalMeshComponents)
            {
                if (component.IsDestoryed == false)
                {
                    if (component.AnimSampler != null && component.SkeletalMesh != null && component.SkeletalMesh.Skeleton != null)
                    {
                        for (int i = 0; i < component.SkeletalMesh.Skeleton.BoneList.Count; i++)
                        {
                            SkeletakMeshDLShadowMapShader.SetMatrix($"AnimTransform[{i}]", component.AnimBuffer[i]);
                        }
                    }
                    SkeletakMeshDLShadowMapShader.SetMatrix("ModelTransform", component.WorldTransform);
                    SkeletakMeshDLShadowMapShader.SetMatrix("NormalTransform", component.NormalTransform);
                    component.Render(DeltaTime);
                }
            }
            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));
        }
        DLShadowMapShader.UnUse();
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
                StaticMeshBaseShader.SetInt("BaseColorTexture", 0);
                StaticMeshBaseShader.SetInt("NormalTexture", 1);
                StaticMeshBaseShader.SetInt("CustomTexture", 2);
                StaticMeshBaseShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
                StaticMeshBaseShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
                StaticMeshBaseShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
                foreach (var component in World.CurrentLevel.StaticMeshComponents)
                {
                    if (component.IsDestoryed == false)
                    {
                        StaticMeshBaseShader.SetMatrix("ModelTransform", component.WorldTransform);
                        StaticMeshBaseShader.SetMatrix("NormalTransform", component.NormalTransform);
                        component.Render(DeltaTime);
                    }
                }

                SkeletalMeshBaseShader.SetInt("BaseColorTexture", 0);
                SkeletalMeshBaseShader.SetInt("NormalTexture", 1);
                SkeletalMeshBaseShader.SetInt("CustomTexture", 2);
                SkeletalMeshBaseShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
                SkeletalMeshBaseShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
                SkeletalMeshBaseShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
                foreach (var component in World.CurrentLevel.SkeletalMeshComponents)
                {
                    if (component.IsDestoryed == false)
                    {
                        if (component.AnimSampler != null && component.SkeletalMesh != null && component.SkeletalMesh.Skeleton != null)
                        {
                            for (int i = 0; i < component.SkeletalMesh.Skeleton.BoneList.Count; i++)
                            {
                                SkeletalMeshBaseShader.SetMatrix($"AnimTransform[{i}]", component.AnimBuffer[i]);
                            }
                        }
                        SkeletalMeshBaseShader.SetMatrix("ModelTransform", component.WorldTransform);
                        SkeletalMeshBaseShader.SetMatrix("NormalTransform", component.NormalTransform);
                        component.Render(DeltaTime);
                    }
                }

                gl.Disable(GLEnum.CullFace);
                gl.PushDebugGroup("InstancedStaticMesh Render");
                HISMShader.SetInt("BaseColorTexture", 0);
                HISMShader.SetInt("NormalTexture", 1);
                HISMShader.SetInt("CustomTexture", 2);
                HISMShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
                HISMShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
                HISMShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
                foreach (var ism in World.CurrentLevel.ISMComponents)
                {
                    if (ism is HierarchicalInstancedStaticMeshComponent hism)
                        hism.CameraCulling(CurrentCameraComponent.GetPlanes());
                    ism.RenderISM(CurrentCameraComponent, DeltaTime);
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
        using (CurrentCameraComponent.RenderTarget.Begin())
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

        };
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
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);

            if (IsMicroGBuffer == false)
            {
                DirectionalLightingShader.SetInt("CustomBuffer", 1);
                gl.ActiveTexture(GLEnum.Texture1);
                gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[1]);
            }


            DirectionalLightingShader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);

            DirectionalLightingShader.SetInt("ShadowMapTexture", 3);
            gl.ActiveTexture(GLEnum.Texture3);
            gl.BindTexture(GLEnum.Texture2D, DirectionalLight.ShadowMapTextureID);

            if (IsMobile == false && PostProcessBuffer2 != null)
            {
                DirectionalLightingShader.SetInt("SSAOTexture", 4);
                gl.ActiveTexture(GLEnum.Texture4);
                gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer2.GBufferIds[0]);

            }
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
        if (PostProcessBuffer1 == null || PostProcessBuffer2 == null || PostProcessBuffer3 == null)
            return;
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

        RenderTarget[] buffer = new RenderTarget[2] {
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

    RenderTarget? LastPostProcessBuffer = null;
    public unsafe void PointLight()
    {
        if (CurrentCameraComponent == null)
            return;
        gl.PushDebugGroup("PointLight Pass");
        PointLightingShader.Use();
        foreach (var PointLightComponent in World.CurrentLevel.PointLightComponents)
        {
            Matrix4x4[] Views = new Matrix4x4[6];
            Views[0] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(0, 0, -1), new Vector3(0, 1, 0));
            Views[1] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(0, 0, 1), new Vector3(0, 1, 0));
            Views[2] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(-1, 0, 0), new Vector3(0, 1, 0));
            Views[3] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(1, 0, 0), new Vector3(0, 1, 0));
            Views[4] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            Views[5] = Matrix4x4.CreateLookAt(PointLightComponent.WorldLocation, PointLightComponent.WorldLocation + new Vector3(0, -1, 0), new Vector3(0, 0, -1));


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
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);


            if (IsMicroGBuffer == false)
            {

                PointLightingShader.SetInt("CustomBuffer", 1);
                gl.ActiveTexture(GLEnum.Texture1);
                gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[1]);
            }

            PointLightingShader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);

            if (IsMobile == false && PostProcessBuffer2 != null)
            {
                PointLightingShader.SetInt("SSAOTexture", 4);
                gl.ActiveTexture(GLEnum.Texture4);
                gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer2.GBufferIds[0]);
            }


            for (int  i = 0; i < 6; i ++)
            {

                var Projection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), 1, 1, 1000);
                PointLightingShader.SetMatrix($"WorldToLights[{i}]", Views[i] * Projection);
                PointLightingShader.SetInt($"ShadowMapTextures{i}", 5 + i);
                gl.ActiveTexture(GLEnum.Texture5 + i);
                gl.BindTexture(GLEnum.Texture2D, PointLightComponent.ShadowMapTextureIDs[i]);
            }
            
            
            
            PointLightingShader.SetFloat("FarPlan", 1000);

            PointLightingShader.SetVector3("LightLocation", PointLightComponent.WorldLocation);
            PointLightingShader.SetVector3("LightColor", PointLightComponent._Color);

            PointLightingShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);


        }
        PointLightingShader.UnUse();
        gl.PopDebugGroup();
    }


    public unsafe void SpotLight()
    {

        gl.PushDebugGroup("SpotLight Pass");
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
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);

            if (IsMicroGBuffer == false)
            {
                SpotLightingShader.SetInt("CustomBuffer", 1);
                gl.ActiveTexture(GLEnum.Texture1);
                gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[1]);
            }


            SpotLightingShader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);
            SpotLightingShader.SetInt("ShadowMapTexture", 3);

            if (IsMobile == false && PostProcessBuffer2 != null)
            {
                SpotLightingShader.SetInt("SSAOTexture", 4);
                gl.ActiveTexture(GLEnum.Texture4);
                gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer2.GBufferIds[0]);
            }

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
        gl.PopDebugGroup();
    }
}

public struct DeferredVertex
{
    public Vector3 Location;
    public Vector2 TexCoord;
}

