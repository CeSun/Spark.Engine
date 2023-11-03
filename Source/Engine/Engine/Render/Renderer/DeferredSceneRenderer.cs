using Spark.Engine.Components;
using System.Drawing;
using Silk.NET.OpenGLES;
using Shader = Spark.Engine.Render.Shader;
using static Spark.Engine.Components.CameraComponent;
using System.Numerics;
using Spark.Util;
using Texture = Spark.Engine.Assets.Texture;
using Spark.Engine.Properties;
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
    Shader IrradianceShader;
    Shader PrefilterShader;
    Shader IndirectLightShader;


    RenderTarget PostProcessBuffer1;
    RenderTarget? PostProcessBuffer2;
    RenderTarget? PostProcessBuffer3;
    World World { get; set; }

    Texture BrdfTexture;

    Texture? NoiseTexture;
    List<Vector3> HalfSpherical = new List<Vector3>();

    uint PostProcessVAO = 0;
    uint PostProcessVBO = 0;
    uint PostProcessEBO = 0;
    bool IsMobile = false;
    bool IsMicroGBuffer = false;

    public RenderTarget CreateRenderTarget(int width, int height, uint GbufferNums)
    {
        return new RenderTarget(width, height, GbufferNums, World.Engine);
    }

    public RenderTarget CreateRenderTarget(int width, int height)
    {
        return new RenderTarget(width, height, World.Engine);
    }

    public Shader CreateShader(string Path, List<string> Macros)
    {
        var frag = Resources.ResourceManager.GetString(Path + ".frag");
        var vert = Resources.ResourceManager.GetString(Path + ".vert");
        return new Shader(vert, frag, Macros, gl);
    }

    public GL gl => World.Engine.Gl;
    public DeferredSceneRenderer(World world)
    {

        World = world;
        var s = gl.GetStringS(GLEnum.Version);
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
        StaticMeshBaseShader = CreateShader("/Shader/Deferred/Base/Base", Macros);
        SkeletalMeshBaseShader = CreateShader("/Shader/Deferred/Base/BaseSkeletalMesh", Macros);
        HISMShader = CreateShader("/Shader/Deferred/Base/BaseInstance", Macros);
        DirectionalLightingShader = CreateShader("/Shader/Deferred/Light/DirectionalLighting", Macros);
        SpotLightingShader = CreateShader("/Shader/Deferred/Light/SpotLighting", Macros);
        PointLightingShader = CreateShader("/Shader/Deferred/Light/PointLighting", Macros);
        DLShadowMapShader = CreateShader("/Shader/ShadowMap/DirectionLightShadow", Macros);
        SpotShadowMapShader = CreateShader("/Shader/ShadowMap/SpotLightShadow", Macros);
        PointLightShadowShader = CreateShader("/Shader/ShadowMap/PointLightShadow", Macros);
        BloomPreShader = CreateShader("/Shader/Deferred/BloomPre", Macros);
        BloomShader = CreateShader("/Shader/Deferred/Bloom", Macros);
        SkyboxShader = CreateShader("/Shader/Skybox", Macros);
        RenderToCamera = CreateShader("/Shader/Deferred/RenderToCamera", Macros);
        ScreenSpaceReflectionShader = CreateShader("/Shader/Deferred/ssr", Macros);
        BackFaceDepthShader = CreateShader("/Shader/Deferred/BackFaceDepth", Macros);
        DecalShader = CreateShader("/Shader/Deferred/Decal", Macros);
        DecalPostShader = CreateShader("/Shader/Deferred/DecalPost", Macros);
        SSAOShader = CreateShader("/Shader/Deferred/SSAO", Macros);

        InstancePointLightingShader = CreateShader("/Shader/ShadowMap/Instance/PointLightShadow", Macros);
        InstanceDLShadowMapShader = CreateShader("/Shader/ShadowMap/Instance/DirectionLightShadow", Macros);
        InstanceSpotShadowMapShader = CreateShader("/Shader/ShadowMap/Instance/SpotLightShadow", Macros);
        SkeletakMeshDLShadowMapShader = CreateShader("/Shader/ShadowMap/SkeletalMesh/DirectionLightShadow", Macros);
        SkeletakMeshSpotShadowMapShader = CreateShader("/Shader/ShadowMap/SkeletalMesh/SpotLightShadow", Macros);
        SkeletakMeshPointLightingShader = CreateShader("/Shader/ShadowMap/SkeletalMesh/PointLightShadow", Macros);

        IrradianceShader = CreateShader("/Shader/Irradiance", Macros);
        PrefilterShader = CreateShader("/Shader/Prefilter", Macros);
        IndirectLightShader = CreateShader("/Shader/Deferred/Light/IndirectLight", Macros);

        if (IsMicroGBuffer == true)
        {
            GlobalBuffer = CreateRenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 1);
        }
        else
        {
            GlobalBuffer = CreateRenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 2);
        }
        PostProcessBuffer1 = new RenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 1, World.Engine, new List<(GLEnum, GLEnum)>
        {
            (GLEnum.Rgba32f, GLEnum.Float),
            (GLEnum.DepthComponent32f, GLEnum.DepthComponent)
        });
        if (IsMobile == false)
        {
            PostProcessBuffer2 = CreateRenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 1);
            PostProcessBuffer3 = CreateRenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 1);
            NoiseTexture = Texture.CreateNoiseTexture(4, 4);
            NoiseTexture.InitRender(gl);

        }
        // SceneBackFaceDepthBuffer = new RenderTarget(World.Engine.WindowSize.X, World.Engine.WindowSize.Y, 0);

        BrdfTexture = Texture.LoadFromMemory(Resources.brdf);
        BrdfTexture.InitRender(gl);
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
        if (PostProcessBuffer2 == null)
            return;
        if (IsMobile == true)
            return;
        gl.PushGroup("Decal Pass");
        gl.PushGroup("Decal PrePass");
        using (PostProcessBuffer2.Begin())
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

           // DecalShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
            DecalShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = PostProcessBuffer2.Width / (float)PostProcessBuffer2.BufferWidth,
                    Y = PostProcessBuffer2.Height / (float)PostProcessBuffer2.BufferHeight
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
                // DecalComponent.Material.Use();


                for (int i = 0; i < DecalComponent.Material.Textures.Count(); i++)
                {
                    var texture = DecalComponent.Material.Textures[i];
                    if (texture != null)
                    {
                        gl.ActiveTexture(GLEnum.Texture0 + i);
                        gl.BindTexture(GLEnum.Texture2D, texture.TextureId);
                    }
                    
                }
                gl.BindVertexArray(PostProcessVAO);
                gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);

            }
        }
        gl.PopGroup();

        gl.PushGroup("Decal PostPass");
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
            gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer2.GBufferIds[0]);
            DecalPostShader.SetInt("DecalDepthTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer2.DepthId);

            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);

            gl.DepthMask(true);
        }
        gl.PopGroup();
        gl.PopGroup();
    }
    public void Render(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        if (IsMobile == false)
            UpdateIBL();
        
        gl.PushGroup("Init Buffers");
        GlobalBuffer.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        PostProcessBuffer1.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        if (IsMobile == false)
        {
            PostProcessBuffer2?.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
            PostProcessBuffer3?.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        }
        //SceneBackFaceDepthBuffer?.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        gl.PopGroup();


        gl.PushGroup("Init Status");
        gl.Enable(GLEnum.CullFace);
        gl.CullFace(GLEnum.Back);
        gl.Enable(GLEnum.DepthTest);
        gl.PopGroup();

        // 生成ShadowMap
        DepthPass(DeltaTime);
        BasePass(DeltaTime);
        DecalPass(DeltaTime);
        if (IsMobile == false)
        {
            AOPass(DeltaTime);
        }
        using (PostProcessBuffer1.Begin())
        {
            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));
            gl.PushGroup("Lighting Pass");
            // 延迟光照
            LightingPass(DeltaTime);
            gl.PopGroup();
            gl.PushGroup("Skybox Pass");
            // 天空盒
            SkyboxPass(DeltaTime);
            gl.PopGroup();
            LastPostProcessBuffer = PostProcessBuffer1;
        }
        gl.PushGroup("PostProcess Pass");
        // 后处理
        PostProcessPass(DeltaTime);
        gl.PopGroup();

        // 渲染到摄像机的RenderTarget上
        RenderToCameraRenderTarget(DeltaTime);
    }

    private uint captureFBO = 0;
    private uint captureRBO = 0;
    private uint irradianceMap = 0;
    private uint prefilterMap = 0;
    private void UpdateIBL()
    {
        if (World.CurrentLevel.CurrentSkybox == null)
            return;
        if (World.CurrentLevel.CurrentSkybox.NeedUpdateIBL == false)
            return;
        if (World.CurrentLevel.CurrentSkybox.SkyboxCube == null)
            return;
        Matrix4x4 captureProjection = Matrix4x4.CreatePerspective(90.0f.DegreeToRadians(), 1.0f, 0.1f, 10.0f);
        Matrix4x4[] captureViews =
        {
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(-1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f,  1.0f,  0.0f), new Vector3(0.0f,  0.0f,  1.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f,  0.0f), new Vector3(0.0f,  0.0f, -1.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f,  0.0f,  1.0f),new Vector3(0.0f, -1.0f,  0.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f,  0.0f, -1.0f), new Vector3(0.0f, -1.0f,  0.0f))
        };
        if (captureFBO == 0 && captureRBO == 0)
        {
            captureFBO = gl.GenFramebuffer();
            captureRBO = gl.GenRenderbuffer();

            gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
            gl.BindRenderbuffer(GLEnum.Renderbuffer, captureRBO);
            gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent24, 512, 512);
            gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, captureRBO);
        }
        if (irradianceMap == 0)
        {
            irradianceMap = gl.GenTexture();
            gl.BindTexture(GLEnum.TextureCubeMap, irradianceMap);
            for (int i = 0; i < 6; ++i)
            {
                unsafe
                {
                    gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, (int)GLEnum.Rgb16f, 32, 32, 0, GLEnum.Rgb, GLEnum.Float, (void*)0);
                }
            }
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);

            gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
            gl.BindRenderbuffer(GLEnum.Renderbuffer, captureRBO);
            gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent24, 32, 32);
        }

        gl.PushGroup("IrradianceShader PreProcess");

        IrradianceShader.Use();
        IrradianceShader.SetInt("environmentMap", 0);
        IrradianceShader.SetMatrix("projection", captureProjection);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.TextureCubeMap, World.CurrentLevel.CurrentSkybox.SkyboxCube.TextureId);
        gl.Viewport(new Rectangle { X = 0, Y  = 0, Height = 32, Width = 32});
        gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);

        for (int i = 0; i < 6; ++i)
        {
            IrradianceShader.SetMatrix("view", captureViews[i]);
            gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.TextureCubeMapPositiveX + i, irradianceMap, 0);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            RenderCube();
        }

        if (prefilterMap == 0)
        {
            prefilterMap = gl.GenTexture();
            gl.BindTexture(GLEnum.TextureCubeMap, prefilterMap);
            for (int i = 0; i < 6; ++i)
            {
                unsafe
                {
                    gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, (int)GLEnum.Rgb16f, 128, 128, 0, GLEnum.Rgb, GLEnum.Float, (void*)0);
                }
            }
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            // generate mipmaps for the cubemap so OpenGL automatically allocates the required memory.
            gl.GenerateMipmap(GLEnum.TextureCubeMap);
        }
        gl.PopGroup();

        gl.PushGroup("Prefilter PreProcess");
        PrefilterShader.Use();
        PrefilterShader.SetInt("environmentMap", 0);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.TextureCubeMap, World.CurrentLevel.CurrentSkybox.SkyboxCube.TextureId);
        PrefilterShader.SetMatrix("projection", captureProjection);
        gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
        int maxMipLevels = 5;
        for (int mip = 0; mip < maxMipLevels; ++mip)
        {
            // reisze framebuffer according to mip-level size.
            uint mipWidth = (uint)(128 * Math.Pow(0.5, mip));
            uint mipHeight = (uint)(128 * Math.Pow(0.5, mip));
            gl.BindRenderbuffer(GLEnum.Renderbuffer, captureRBO);
            gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent24, mipWidth, mipHeight);
            gl.Viewport(0, 0, mipWidth, mipHeight);

            float roughness = (float)mip / (float)(maxMipLevels - 1);
            PrefilterShader.SetFloat("roughness", roughness);
            for (int i = 0; i < 6; ++i)
            {
                PrefilterShader.SetMatrix("view", captureViews[i]);
                gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.TextureCubeMapPositiveX + i, prefilterMap, mip);
                
                gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                RenderCube();
            }
        }
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        gl.PopGroup();

        World.CurrentLevel.CurrentSkybox.NeedUpdateIBL = false;
    }

    private uint cubeVAO = 0;
    private uint cubeVBO = 0;

    private void RenderCube()
    {
        if (cubeVAO == 0)
        {
            float[] vertices = {
                // back face
                -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 0.0f, 0.0f, // bottom-left
                 1.0f,  1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 1.0f, 1.0f, // top-right
                 1.0f, -1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 1.0f, 0.0f, // bottom-right         
                 1.0f,  1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 1.0f, 1.0f, // top-right
                -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 0.0f, 0.0f, // bottom-left
                -1.0f,  1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 0.0f, 1.0f, // top-left
                // front face
                -1.0f, -1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f, 0.0f, // bottom-left
                 1.0f, -1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f, 0.0f, // bottom-right
                 1.0f,  1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f, 1.0f, // top-right
                 1.0f,  1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f, 1.0f, // top-right
                -1.0f,  1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f, 1.0f, // top-left
                -1.0f, -1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f, 0.0f, // bottom-left
                // left face
                -1.0f,  1.0f,  1.0f, -1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-right
                -1.0f,  1.0f, -1.0f, -1.0f,  0.0f,  0.0f, 1.0f, 1.0f, // top-left
                -1.0f, -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-left
                -1.0f, -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-left
                -1.0f, -1.0f,  1.0f, -1.0f,  0.0f,  0.0f, 0.0f, 0.0f, // bottom-right
                -1.0f,  1.0f,  1.0f, -1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-right
                // right face
                 1.0f,  1.0f,  1.0f,  1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-left
                 1.0f, -1.0f, -1.0f,  1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-right
                 1.0f,  1.0f, -1.0f,  1.0f,  0.0f,  0.0f, 1.0f, 1.0f, // top-right         
                 1.0f, -1.0f, -1.0f,  1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-right
                 1.0f,  1.0f,  1.0f,  1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-left
                 1.0f, -1.0f,  1.0f,  1.0f,  0.0f,  0.0f, 0.0f, 0.0f, // bottom-left     
                // bottom face
                -1.0f, -1.0f, -1.0f,  0.0f, -1.0f,  0.0f, 0.0f, 1.0f, // top-right
                 1.0f, -1.0f, -1.0f,  0.0f, -1.0f,  0.0f, 1.0f, 1.0f, // top-left
                 1.0f, -1.0f,  1.0f,  0.0f, -1.0f,  0.0f, 1.0f, 0.0f, // bottom-left
                 1.0f, -1.0f,  1.0f,  0.0f, -1.0f,  0.0f, 1.0f, 0.0f, // bottom-left
                -1.0f, -1.0f,  1.0f,  0.0f, -1.0f,  0.0f, 0.0f, 0.0f, // bottom-right
                -1.0f, -1.0f, -1.0f,  0.0f, -1.0f,  0.0f, 0.0f, 1.0f, // top-right
                // top face
                -1.0f,  1.0f, -1.0f,  0.0f,  1.0f,  0.0f, 0.0f, 1.0f, // top-left
                 1.0f,  1.0f , 1.0f,  0.0f,  1.0f,  0.0f, 1.0f, 0.0f, // bottom-right
                 1.0f,  1.0f, -1.0f,  0.0f,  1.0f,  0.0f, 1.0f, 1.0f, // top-right     
                 1.0f,  1.0f,  1.0f,  0.0f,  1.0f,  0.0f, 1.0f, 0.0f, // bottom-right
                -1.0f,  1.0f, -1.0f,  0.0f,  1.0f,  0.0f, 0.0f, 1.0f, // top-left
                -1.0f,  1.0f,  1.0f,  0.0f,  1.0f,  0.0f, 0.0f, 0.0f  // bottom-left        
            };
            cubeVAO = gl.GenVertexArray();
            cubeVBO = gl.GenBuffer();
            // fill buffer
            gl.BindBuffer(GLEnum.ArrayBuffer, cubeVBO);
            unsafe
            {
                fixed(void* p = vertices)
                {
                    gl.BufferData(GLEnum.ArrayBuffer, (nuint)vertices.Length * sizeof(float), p, GLEnum.StaticDraw);
                }
                // link vertex attributes
                gl.BindVertexArray(cubeVAO);
                gl.EnableVertexAttribArray(0);
                gl.VertexAttribPointer(0, 3, GLEnum.Float, false, 8 * sizeof(float), (void*)0);
                gl.EnableVertexAttribArray(1);
                gl.VertexAttribPointer(1, 3, GLEnum.Float, false, 8 * sizeof(float), (void*)(3 * sizeof(float)));
                gl.EnableVertexAttribArray(2);
                gl.VertexAttribPointer(2, 2, GLEnum.Float, false, 8 * sizeof(float), (void*)(6 * sizeof(float)));
                gl.BindBuffer(GLEnum.ArrayBuffer, 0);
                gl.BindVertexArray(0);
            }
        }
        // render Cube
        gl.BindVertexArray(cubeVAO);
        gl.DrawArrays(GLEnum.Triangles, 0, 36);
        gl.BindVertexArray(0);
    }

   
    private void PostProcessPass(double DeltaTime)
    {
        if (IsMobile == true)
            return;
        gl.PushGroup("Bloom Effect");
        BloomPass(DeltaTime);
        gl.PopGroup();
        gl.PushGroup("ScreenSpaceReflection");
        // ScreenSpaceReflection(DeltaTime);
        gl.PopGroup();
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
        gl.PushGroup("SSAO Pass");
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
        gl.PopGroup();
    }

    public unsafe void IndirectLight()
    {
        if (World.CurrentLevel.CurrentSkybox == null)
            return;
        if (World.CurrentLevel.CurrentSkybox.SkyboxCube == null)
            return;
        if (CurrentCameraComponent == null)
            return;
        IndirectLightShader.SetInt("ColorTexture", 0);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[0]);


        if (IsMicroGBuffer == false)
        {

            IndirectLightShader.SetInt("CustomBuffer", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.GBufferIds[1]);
        }

        IndirectLightShader.SetInt("DepthTexture", 2);
        gl.ActiveTexture(GLEnum.Texture2);
        gl.BindTexture(GLEnum.Texture2D, GlobalBuffer.DepthId);


        IndirectLightShader.SetInt("irradianceMap", 3);
        gl.ActiveTexture(GLEnum.Texture3);
        gl.BindTexture(GLEnum.TextureCubeMap, irradianceMap);


        IndirectLightShader.SetInt("prefilterMap", 4);
        gl.ActiveTexture(GLEnum.Texture4);
        gl.BindTexture(GLEnum.TextureCubeMap, prefilterMap);


        IndirectLightShader.SetInt("brdfLUT", 5);
        gl.ActiveTexture(GLEnum.Texture5);
        gl.BindTexture(GLEnum.Texture2D, BrdfTexture.TextureId);


        if (IsMobile == false && PostProcessBuffer2 != null)
        {
            IndirectLightShader.SetInt("SSAOTexture", 6);
            gl.ActiveTexture(GLEnum.Texture6);
            gl.BindTexture(GLEnum.Texture2D, PostProcessBuffer2.GBufferIds[0]);
        }

        IndirectLightShader.SetVector2("TexCoordScale",
            new Vector2
            {
                X = GlobalBuffer.Width / (float)GlobalBuffer.BufferWidth,
                Y = GlobalBuffer.Height / (float)GlobalBuffer.BufferHeight
            });

        Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var VPInvert);
        IndirectLightShader.SetMatrix("VPInvert", VPInvert);

        IndirectLightShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);


        gl.BindVertexArray(PostProcessVAO);
        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
        gl.ActiveTexture(GLEnum.Texture0);

    }
    private void DepthPass(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;

        gl.PushGroup("ShadowMap Pass");
        gl.Enable(GLEnum.DepthTest);
        gl.Enable(GLEnum.CullFace);
        gl.CullFace(GLEnum.Front);

        SpotShadowMap(DeltaTime);
        PointLightShadowMap(DeltaTime);
        DirectionLightShadowMap(DeltaTime);


        gl.CullFace(GLEnum.Back);
        gl.PopGroup();
    }

    private void SpotShadowMap(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;

        gl.PushGroup("SpotLight");
        SpotShadowMapShader.Use();
        foreach (var SpotLightComponent in World.CurrentLevel.SpotLightComponents)
        {
            var View = Matrix4x4.CreateLookAt(SpotLightComponent.WorldLocation, SpotLightComponent.WorldLocation + SpotLightComponent.ForwardVector, SpotLightComponent.UpVector);
            var Projection = Matrix4x4.CreatePerspectiveFieldOfView(SpotLightComponent.OuterAngle.DegreeToRadians(), 1, 1F, 100);
            gl.Viewport(new Rectangle(0, 0, SpotLightComponent.ShadowMapSize.X, SpotLightComponent.ShadowMapSize.Y));
            gl.BindFramebuffer(GLEnum.Framebuffer, SpotLightComponent.ShadowMapFrameBufferID);
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
                    component.Render(DeltaTime);
                }
            }
            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));

        }

        SpotLightingShader.UnUse();
        gl.PopGroup();
    }
    private void PointLightShadowMap(double DeltaTime)
    {

        if (CurrentCameraComponent == null)
            return;
        gl.PushGroup("PointLight");
        PointLightShadowShader.Use();
        foreach (var PointLightComponent in World.CurrentLevel.PointLightComponents)
        {

            var Projection = Matrix4x4.CreatePerspectiveFieldOfView(95f.DegreeToRadians(), 1, 1, 1000);
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
                gl.PushGroup("face:" + i);
                gl.BindFramebuffer(GLEnum.Framebuffer, PointLightComponent.ShadowMapFrameBufferIDs[i]);
                gl.Clear(ClearBufferMask.DepthBufferBit);
                PointLightShadowShader.SetMatrix("ViewTransform", Views[i]);
                PointLightShadowShader.SetMatrix("ProjectionTransform", Projection);
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
                foreach (var hism in World.CurrentLevel.ISMComponents)
                {
                    if (hism == null)
                        continue;
                    hism.RenderISM(CurrentCameraComponent, DeltaTime);
                }


                SkeletakMeshPointLightingShader.SetMatrix("ViewTransform", Views[i]);
                SkeletakMeshPointLightingShader.SetMatrix("ProjectionTransform", Projection);
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
                        component.Render(DeltaTime);
                    }
                }
                gl.PopGroup();
            }

            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));
        }
        PointLightShadowShader.UnUse();
        gl.PopGroup();
    }
    private void DirectionLightShadowMap(double DeltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        gl.PushGroup("DirectionLight");
        DLShadowMapShader.Use();
        foreach (var DirectionalLight in World.CurrentLevel.DirectionLightComponents)
        {
            var LightLocation = CurrentCameraComponent.WorldLocation - DirectionalLight.ForwardVector * 20;
            var View = Matrix4x4.CreateLookAt(LightLocation, LightLocation + DirectionalLight.ForwardVector, DirectionalLight.UpVector);
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
                    component.Render(DeltaTime);
                }
            }
            gl.Viewport(new Rectangle(0, 0, CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height));
        }
        DLShadowMapShader.UnUse();
        gl.PopGroup();
    }
    private void BasePass(double DeltaTime)
    {
        // 生成GBuffer
        gl.PushGroup("Base Pass");
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
                StaticMeshBaseShader.SetInt("ARMTexture", 2);
                StaticMeshBaseShader.SetInt("ParallaxTexture", 3);
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
                SkeletalMeshBaseShader.SetInt("ARMTexture", 2);
                SkeletalMeshBaseShader.SetInt("ParallaxTexture", 3);
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
                gl.PushGroup("InstancedStaticMesh Render");
                HISMShader.SetInt("BaseColorTexture", 0);
                HISMShader.SetInt("NormalTexture", 1);
                HISMShader.SetInt("ARMTexture", 2);
                HISMShader.SetInt("ParallaxTexture", 3);
                HISMShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
                HISMShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
                HISMShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
                foreach (var ism in World.CurrentLevel.ISMComponents)
                {
                    if (ism is HierarchicalInstancedStaticMeshComponent hism)
                        hism.CameraCulling(CurrentCameraComponent.GetPlanes());
                    ism.RenderISM(CurrentCameraComponent, DeltaTime);
                }
                gl.PopGroup();
            }
        }
        gl.PopGroup();
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

        IndirectLight();
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
        gl.PushGroup("DirectionalLight Pass");
        DirectionalLightingShader.Use();
        foreach (var DirectionalLight in World.CurrentLevel.DirectionLightComponents)
        {
            var LightInfo = DirectionalLight.LightInfo;
            Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var VPInvert);
            DirectionalLightingShader.SetMatrix("VPInvert", VPInvert);

            var LightLocation = CurrentCameraComponent.WorldLocation - DirectionalLight.ForwardVector * 20;
            var View = Matrix4x4.CreateLookAt(LightLocation, LightLocation + DirectionalLight.ForwardVector, DirectionalLight.UpVector);
            var Projection = Matrix4x4.CreateOrthographic(100, 100, 1.0f, 100f);

            var WorldToLight = View * Projection;
            DirectionalLightingShader.SetMatrix("WorldToLight", WorldToLight);
            DirectionalLightingShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = GlobalBuffer.Width / (float)GlobalBuffer.BufferWidth,
                    Y = GlobalBuffer.Height / (float)GlobalBuffer.BufferHeight
                });


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

            DirectionalLightingShader.SetVector3("LightDirection", LightInfo.Direction);
            DirectionalLightingShader.SetVector3("LightColor", LightInfo.Color);

            DirectionalLightingShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);


        }
        DirectionalLightingShader.UnUse();
        gl.PopGroup();
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
        gl.PushGroup("PointLight Pass");
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

          

            for (int  i = 0; i < 6; i ++)
            {

                var Projection = Matrix4x4.CreatePerspectiveFieldOfView(95F.DegreeToRadians(), 1, 1, 1000);
                PointLightingShader.SetMatrix($"WorldToLights[{i}]", Views[i] * Projection);
                PointLightingShader.SetInt($"ShadowMapTextures{i}", 5 + i);
                gl.ActiveTexture(GLEnum.Texture5 + i);
                gl.BindTexture(GLEnum.Texture2D, PointLightComponent.ShadowMapTextureIDs[i]);
            }
            
            
            PointLightingShader.SetVector3("LightLocation", PointLightComponent.WorldLocation);
            PointLightingShader.SetVector3("LightColor", PointLightComponent._Color);

            PointLightingShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
            gl.BindVertexArray(PostProcessVAO);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);


        }
        PointLightingShader.UnUse();
        gl.PopGroup();
    }


    public unsafe void SpotLight()
    {

        gl.PushGroup("SpotLight Pass");
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
            var Projection = Matrix4x4.CreatePerspectiveFieldOfView(SpotLightComponent.OuterAngle.DegreeToRadians(), 1, 1F, 100);
            var WorldToLight = View * Projection;
            SpotLightingShader.SetMatrix("WorldToLight", WorldToLight);

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
        gl.PopGroup();
    }



    
}

public struct DeferredVertex
{
    public Vector3 Location;
    public Vector2 TexCoord;
}

