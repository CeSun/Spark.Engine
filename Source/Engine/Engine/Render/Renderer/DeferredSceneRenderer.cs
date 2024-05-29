using Spark.Engine.Components;
using System.Drawing;
using Silk.NET.OpenGLES;
using static Spark.Engine.Components.CameraComponent;
using System.Numerics;
using Spark.Util;
using Texture = Spark.Engine.Assets.Texture;
using Spark.Engine.Properties;

namespace Spark.Engine.Render.Renderer;

public class DeferredSceneRenderer : IRenderer
{
    private readonly RenderTarget _globalBuffer;
    private readonly Shader _staticMeshBaseShader;
    private readonly Shader _directionalLightingShader;
    private readonly Shader _spotLightingShader;
    private readonly Shader _pointLightingShader;

    private readonly Shader _directionalLightingShaderNoShadow;
    private readonly Shader _spotLightingShaderNoShadow;
    private readonly Shader _pointLightingShaderNoShadow;

    private readonly Shader _dlShadowMapShader;
    private readonly Shader _instanceDlShadowMapShader;
    private readonly Shader _spotShadowMapShader;
    private readonly Shader _instanceSpotShadowMapShader;
    private readonly Shader _instancePointLightingShader;
    private readonly Shader _skyboxShader;
    private readonly Shader _pointLightShadowShader;
    private readonly Shader _bloomPreShader;
    private readonly Shader _bloomShader;
    private readonly Shader _renderToCamera;
    private readonly Shader _hismShader;
    private readonly Shader _decalShader;
    private readonly Shader _decalPostShader;
    private readonly Shader _ssaoShader;

    private readonly Shader _skeletalMeshDlShadowMapShader;
    private readonly Shader _skeletalMeshSpotShadowMapShader;
    private readonly Shader _skeletalMeshPointLightingShader;

    private readonly Shader _skeletalMeshBaseShader;
    private readonly Shader _bloomPostShader;
    public Shader IrradianceShader;
    public Shader PrefilterShader;
    public Shader IndirectLightShader;
    public Shader Hdri2CubeMapShader;


    private readonly RenderTarget _postProcessBuffer1;
    private readonly RenderTarget? _postProcessBuffer2;
    private readonly RenderTarget? _postProcessBuffer3;
    private World World { get; set; }

    private readonly Texture _brdfTexture;

    private readonly Texture? _noiseTexture;
    private readonly List<Vector3> _halfSpherical = [];

    private uint _postProcessVao;
    private uint _postProcessVbo;
    private uint _postProcessEbo;
    private readonly bool _isMobile;
    private readonly bool _isMicroGBuffer;

    public RenderTarget CreateRenderTarget(int width, int height, uint gbufferNums)
    {
        return new RenderTarget(width, height, gbufferNums, World.Engine);
    }

    public RenderTarget CreateRenderTarget(int width, int height)
    {
        return new RenderTarget(width, height, World.Engine);
    }

    public Shader CreateShader(string path, List<string> macros)
    {
        var frag = Resources.ResourceManager.GetString(path + ".frag");
        var vert = Resources.ResourceManager.GetString(path + ".vert");
        return new Shader(vert!, frag!, macros, gl);
    }

    public GL gl => World.Engine.GraphicsApi!;
    public DeferredSceneRenderer(World world)
    {

        World = world;
        List<string> macros = [];
        if (World.Engine.IsMobile)
        {
            _isMobile = true;
            _isMicroGBuffer = true;
        }
        if (_isMobile)
        {
            macros.Add("_MOBILE_");
        }
        if (_isMicroGBuffer)
        {
           macros.Add("_MICRO_GBUFFER_");
        }
        // Base Pass
        _staticMeshBaseShader = CreateShader("/Shader/Deferred/Base/Base", macros);
        _skeletalMeshBaseShader = CreateShader("/Shader/Deferred/Base/BaseSkeletalMesh", macros);
        _hismShader = CreateShader("/Shader/Deferred/Base/BaseInstance", macros);

        List<string> macrosLightWithShadow = [..macros, "_ENABLE_SHADOWMAP_"];
        _directionalLightingShader = CreateShader("/Shader/Deferred/Light/DirectionalLighting", macrosLightWithShadow);
        _spotLightingShader = CreateShader("/Shader/Deferred/Light/SpotLighting", macrosLightWithShadow);
        _pointLightingShader = CreateShader("/Shader/Deferred/Light/PointLighting", macrosLightWithShadow);


        _directionalLightingShaderNoShadow = CreateShader("/Shader/Deferred/Light/DirectionalLighting", macros);
        _spotLightingShaderNoShadow = CreateShader("/Shader/Deferred/Light/SpotLighting", macros);
        _pointLightingShaderNoShadow = CreateShader("/Shader/Deferred/Light/PointLighting", macros);


        _dlShadowMapShader = CreateShader("/Shader/ShadowMap/DirectionLightShadow", macros);
        _spotShadowMapShader = CreateShader("/Shader/ShadowMap/SpotLightShadow", macros);
        _pointLightShadowShader = CreateShader("/Shader/ShadowMap/PointLightShadow", macros);
        _bloomPreShader = CreateShader("/Shader/Deferred/BloomPre", macros);
        _bloomShader = CreateShader("/Shader/Deferred/Bloom", macros);
        _bloomPostShader = CreateShader("/Shader/Deferred/BloomPost", macros);
        _skyboxShader = CreateShader("/Shader/Skybox", macros);
        _renderToCamera = CreateShader("/Shader/Deferred/RenderToCamera", macros);
        CreateShader("/Shader/Deferred/ssr", macros);
        CreateShader("/Shader/Deferred/BackFaceDepth", macros);
        _decalShader = CreateShader("/Shader/Deferred/Decal", macros);
        _decalPostShader = CreateShader("/Shader/Deferred/DecalPost", macros);
        _ssaoShader = CreateShader("/Shader/Deferred/SSAO", macros);

        _instancePointLightingShader = CreateShader("/Shader/ShadowMap/Instance/PointLightShadow", macros);
        _instanceDlShadowMapShader = CreateShader("/Shader/ShadowMap/Instance/DirectionLightShadow", macros);
        _instanceSpotShadowMapShader = CreateShader("/Shader/ShadowMap/Instance/SpotLightShadow", macros);
        _skeletalMeshDlShadowMapShader = CreateShader("/Shader/ShadowMap/SkeletalMesh/DirectionLightShadow", macros);
        _skeletalMeshSpotShadowMapShader = CreateShader("/Shader/ShadowMap/SkeletalMesh/SpotLightShadow", macros);
        _skeletalMeshPointLightingShader = CreateShader("/Shader/ShadowMap/SkeletalMesh/PointLightShadow", macros);

        IrradianceShader = CreateShader("/Shader/Irradiance", macros);
        PrefilterShader = CreateShader("/Shader/Prefilter", macros);
        IndirectLightShader = CreateShader("/Shader/Deferred/Light/IndirectLight", macros);


        Hdri2CubeMapShader = CreateShader("/Shader/Deferred/RenderHDRI2CubeMap", macros);

        if (_isMicroGBuffer)
        {
            _globalBuffer = CreateRenderTarget(1, 1, 1);
        }
        else
        {
            _globalBuffer = CreateRenderTarget(1, 1, 2);
        }
        _postProcessBuffer1 = new RenderTarget(1, 1, 1, World.Engine, new List<(GLEnum, GLEnum)>
        {
            (GLEnum.Rgba32f, GLEnum.Float),
            (GLEnum.DepthComponent32f, GLEnum.DepthComponent)
        });
        if (_isMobile == false)
        {
            _postProcessBuffer2 = CreateRenderTarget(1, 1, 1);
            _postProcessBuffer3 = CreateRenderTarget(1, 1, 1);
            _noiseTexture = Texture.CreateNoiseTexture(64, 64);
            _noiseTexture.InitRender(gl);

        }

        _postProcessEbo = 0;
        _isMobile = false;
        _isMicroGBuffer = false;
        _cubeVbo = 0;
        _brdfTexture = Texture.LoadFromMemory(Resources.brdf, true, true);
        _brdfTexture.InitRender(gl);
        InitRender();
        InitSsaoRender();
    }

    private void InitSsaoRender()
    {
        for(int i = 0; i < 64; i ++)
        {
            _halfSpherical.Add(new Vector3
            {
                X = (float)Random.Shared.NextDouble() * 2 - 1,
                Y = (float)Random.Shared.NextDouble() * 2 - 1,
                Z = (float)Random.Shared.NextDouble() * 2 - 1
            });
        }

        for (int i = 0; i < 64; i++)
        {
            _ssaoShader.SetVector3($"samples[{i}]", _halfSpherical[i]);
        }
    }

    public unsafe void InitRender()
    {
        DeferredVertex[] vertices = new DeferredVertex[4] {
            new () {Location = new Vector3(-1, 1, 0), TexCoord = new Vector2(0, 1) },
            new () {Location = new Vector3(-1, -1, 0), TexCoord = new Vector2(0, 0) },
            new () {Location = new Vector3(1, -1, 0), TexCoord = new Vector2(1, 0) },
            new () {Location = new Vector3(1, 1, 0), TexCoord = new Vector2(1, 1) },
        };

        uint[] indices =
        [
            0, 1, 2, 2, 3,0
        ];
        _postProcessVao = gl.GenVertexArray();
        _postProcessVbo = gl.GenBuffer();
        _postProcessEbo = gl.GenBuffer();
        gl.BindVertexArray(_postProcessVao);
        gl.BindBuffer(GLEnum.ArrayBuffer, _postProcessVbo);
        fixed (DeferredVertex* p = vertices)
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(vertices.Length * sizeof(DeferredVertex)), p, GLEnum.StaticDraw);
        }
        gl.BindBuffer(GLEnum.ElementArrayBuffer, _postProcessEbo);
        fixed (uint* p = indices)
        {
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), p, GLEnum.StaticDraw);
        }
        // Location
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(DeferredVertex), (void*)0);
        // TexCoord
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, GLEnum.Float, false, (uint)sizeof(DeferredVertex), (void*)sizeof(Vector3));
        gl.BindVertexArray(0);


    }

    public unsafe void DecalPass(RenderTarget tempRenderTarget)
    {
        if (CurrentCameraComponent == null)
            return;
        if (_decalComponents.Count == 0)
            return;
        gl.PushGroup("Decal Pass");
        gl.PushGroup("Decal PrePass");
        using (tempRenderTarget.Begin())
        {
            gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);

            _decalShader.SetInt("BaseColorTexture", 0);
            _decalShader.SetInt("NormalTexture", 1);
            _decalShader.SetInt("CustomTexture", 2);
            _decalShader.SetInt("DepthTexture", 3);
            _decalShader.SetInt("GBuffer1", 4);
            if (_isMicroGBuffer == false)
            {
                _decalShader.SetInt("GBuffer2", 5);
            }
            Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var vpInvert);
            _decalShader.SetMatrix("VPInvert", vpInvert);

           // DecalShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
            _decalShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = tempRenderTarget.Width / (float)tempRenderTarget.BufferWidth,
                    Y = tempRenderTarget.Height / (float)tempRenderTarget.BufferHeight
                });
            gl.ActiveTexture(GLEnum.Texture3);
            gl.BindTexture(GLEnum.Texture2D, _globalBuffer.DepthId);
            gl.ActiveTexture(GLEnum.Texture4);
            gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[0]);
            if (_isMicroGBuffer == false)
            {
                gl.ActiveTexture(GLEnum.Texture5);
                gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[1]);
            }

            foreach (var decalComponent in _decalComponents)
            {
                if (decalComponent.Material == null)
                    continue;
                _decalShader.SetMatrix("ModelTransform", decalComponent.WorldTransform);
                // DecalComponent.Material.Use();


                for (int i = 0; i < decalComponent.Material.Textures.Count(); i++)
                {
                    var texture = decalComponent.Material.Textures[i];
                    gl.ActiveTexture(GLEnum.Texture0 + i);
                    gl.BindTexture(GLEnum.Texture2D, texture.TextureId);

                }
                gl.BindVertexArray(_postProcessVao);
                gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);

            }
        }
        gl.PopGroup();

        gl.PushGroup("Decal PostPass");
        using (_globalBuffer.Begin())
        {
            gl.DepthMask(false);
            _decalPostShader.Use();
            _decalPostShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = tempRenderTarget.Width / (float)tempRenderTarget.BufferWidth,
                    Y = tempRenderTarget.Height / (float)tempRenderTarget.BufferHeight
                });

            _decalPostShader.SetInt("DecalTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, tempRenderTarget.GBufferIds[0]);
            _decalPostShader.SetInt("DecalDepthTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, tempRenderTarget.DepthId);

            gl.BindVertexArray(_postProcessVao);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);

            gl.DepthMask(true);
        }
        gl.PopGroup();
        gl.PopGroup();
    }
    public void Render(double deltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        
        gl.PushGroup("Init Buffers");
        _globalBuffer.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        _postProcessBuffer1.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        if (_isMobile == false)
        {
            _postProcessBuffer2?.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
            _postProcessBuffer3?.Resize(CurrentCameraComponent.RenderTarget.Width, CurrentCameraComponent.RenderTarget.Height);
        }
        
        CameraCulling();
        gl.PopGroup();
        gl.PushGroup("Init Status");
        gl.Enable(GLEnum.CullFace);
        gl.CullFace(GLEnum.Back);
        gl.Enable(GLEnum.DepthTest);
        gl.PopGroup();

        // 生成ShadowMap
        DepthPass(deltaTime);
        BasePass(deltaTime);
        if (_postProcessBuffer2 != null)
        {
            DecalPass(_postProcessBuffer2);
            AoPass(_postProcessBuffer2);
        }

        using (_postProcessBuffer1.Begin())
        {
            // 延迟光照
            LightingPass(_postProcessBuffer2);
        }

        // 泛光效果
        if (_postProcessBuffer2 != null && _postProcessBuffer3 != null)
            BloomPass(_postProcessBuffer2, _postProcessBuffer3, _postProcessBuffer1);

        // 后处理
        PostProcessPass();

        // 渲染到摄像机的RenderTarget上
        RenderToCameraRenderTarget(deltaTime, _postProcessBuffer1);
    }


    private uint _cubeVao;
    private uint _cubeVbo;

    public void RenderCube()
    {
        if (_cubeVao == 0)
        {
            float[] vertices =
            [
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
            ];
            _cubeVao = gl.GenVertexArray();
            _cubeVbo = gl.GenBuffer();
            // fill buffer
            gl.BindBuffer(GLEnum.ArrayBuffer, _cubeVbo);
            unsafe
            {
                fixed(void* p = vertices)
                {
                    gl.BufferData(GLEnum.ArrayBuffer, (nuint)vertices.Length * sizeof(float), p, GLEnum.StaticDraw);
                }
                // link vertex attributes
                gl.BindVertexArray(_cubeVao);
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
        gl.BindVertexArray(_cubeVao);
        gl.DrawArrays(GLEnum.Triangles, 0, 36);
        gl.BindVertexArray(0);
    }

   
    private void PostProcessPass()
    {
        gl.PushGroup("PostProcess Pass");
        gl.PopGroup();
    }
    private void SkyboxPass(double deltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        _skyboxShader.Use();
        var view = Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.Zero + CurrentCameraComponent.ForwardVector, CurrentCameraComponent.UpVector);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(CurrentCameraComponent.FieldOfView.DegreeToRadians(), World.WorldMainRenderTarget!.Width / (float)World.WorldMainRenderTarget.Height, 0.1f, 100f);
        _skyboxShader.SetMatrix("view", view);
        _skyboxShader.SetMatrix("projection", projection);

        _skyboxShader.SetInt("DepthTexture", 1);
        gl.ActiveTexture(GLEnum.Texture1);
        gl.BindTexture(GLEnum.Texture2D, _globalBuffer.DepthId);

        _skyboxShader.SetVector2("BufferSize", new Vector2(_globalBuffer.BufferWidth, _globalBuffer.BufferHeight)); 
        _skyboxShader.SetVector2("ScreenSize", new Vector2(_globalBuffer.Width, _globalBuffer.Height));

        _skyboxShader.SetInt("skybox", 0);
        World.CurrentLevel.CurrentSkybox?.RenderSkybox(deltaTime);
        _skyboxShader.UnUse();

    }
    private Plane[] _planes = new Plane[6];
    private unsafe void AoPass(RenderTarget renderTarget)
    {
        if (CurrentCameraComponent == null)
            return;
        gl.PushGroup("SSAO Pass");
        using (renderTarget.Begin())
        {
            gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
           
            _ssaoShader.SetVector2("TexCoordScale",
            new Vector2
            {
                X = renderTarget.Width / (float)renderTarget.BufferWidth,
                Y = renderTarget.Height / (float)renderTarget.BufferHeight
            });

            _ssaoShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
            _ssaoShader.SetMatrix("InvertProjectionTransform", CurrentCameraComponent.Projection.Inverse());

            if (_isMicroGBuffer == false)
            {
                _ssaoShader.SetInt("CustomBuffer", 1);
                gl.ActiveTexture(GLEnum.Texture1);
                gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[1]);
            }
            else
            {
                _ssaoShader.SetInt("CustomBuffer", 1);
                gl.ActiveTexture(GLEnum.Texture1);
                gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[0]);
            }

            _ssaoShader.SetInt("DepthTexture", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, _globalBuffer.DepthId);

            _ssaoShader.SetInt("NoiseTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, _noiseTexture!.TextureId);


            gl.BindVertexArray(_postProcessVao);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);


        }
        gl.PopGroup();
    }

    public unsafe void IndirectLight(RenderTarget? aoRenderTarget)
    {
        if (World.CurrentLevel.CurrentSkybox == null)
            return;
        if (World.CurrentLevel.CurrentSkybox.SkyboxCube == null)
            return;
        if (CurrentCameraComponent == null)
            return;
        IndirectLightShader.SetInt("ColorTexture", 0);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[0]);


        if (_isMicroGBuffer == false)
        {

            IndirectLightShader.SetInt("CustomBuffer", 1);
            gl.ActiveTexture(GLEnum.Texture1);
            gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[1]);
        }

        IndirectLightShader.SetInt("DepthTexture", 2);
        gl.ActiveTexture(GLEnum.Texture2);
        gl.BindTexture(GLEnum.Texture2D, _globalBuffer.DepthId);


        IndirectLightShader.SetInt("irradianceMap", 3);
        gl.ActiveTexture(GLEnum.Texture3);
        gl.BindTexture(GLEnum.TextureCubeMap, World.CurrentLevel.CurrentSkybox.IrradianceMapId);


        IndirectLightShader.SetInt("prefilterMap", 4);
        gl.ActiveTexture(GLEnum.Texture4);
        gl.BindTexture(GLEnum.TextureCubeMap, World.CurrentLevel.CurrentSkybox.PrefilterMapId);


        IndirectLightShader.SetInt("brdfLUT", 5);
        gl.ActiveTexture(GLEnum.Texture5);
        gl.BindTexture(GLEnum.Texture2D, _brdfTexture.TextureId);


        if (aoRenderTarget != null)
        {
            IndirectLightShader.SetInt("SSAOTexture", 6);
            gl.ActiveTexture(GLEnum.Texture6);
            gl.BindTexture(GLEnum.Texture2D, aoRenderTarget.GBufferIds[0]);
        }

        IndirectLightShader.SetVector2("TexCoordScale",
            new Vector2
            {
                X = _globalBuffer.Width / (float)_globalBuffer.BufferWidth,
                Y = _globalBuffer.Height / (float)_globalBuffer.BufferHeight
            });

        Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var vpInvert);
        IndirectLightShader.SetMatrix("VPInvert", vpInvert);

        IndirectLightShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);


        gl.BindVertexArray(_postProcessVao);
        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
        gl.ActiveTexture(GLEnum.Texture0);

    }
    private void DepthPass(double deltaTime)
    {
        if (CurrentCameraComponent == null)
            return;

        gl.PushGroup("ShadowMap Pass");
        gl.Enable(GLEnum.DepthTest);
        gl.Enable(GLEnum.CullFace);
        gl.CullFace(GLEnum.Front);

        SpotShadowMap(deltaTime);
        PointLightShadowMap(deltaTime);
        DirectionLightShadowMap(deltaTime);


        gl.CullFace(GLEnum.Back);
        gl.PopGroup();
    }

    private void SpotShadowMap(double deltaTime)
    {
        if (CurrentCameraComponent == null)
            return;

        gl.PushGroup("SpotLight");
        _spotShadowMapShader.Use();
        foreach (var spotLightComponent in World.CurrentLevel.SpotLightComponents)
        {
            if (spotLightComponent.IsCastShadowMap == false)
            {
                continue;
            }
            gl.BindFramebuffer(GLEnum.Framebuffer, spotLightComponent.ShadowMapFrameBufferID);
            gl.Clear(ClearBufferMask.DepthBufferBit);
            var view = Matrix4x4.CreateLookAt(spotLightComponent.WorldLocation, spotLightComponent.WorldLocation + spotLightComponent.ForwardVector, spotLightComponent.UpVector);
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(spotLightComponent.OuterAngle.DegreeToRadians(), 1, 1F, 100);
            gl.Viewport(new Rectangle(0, 0, spotLightComponent.ShadowMapSize.X, spotLightComponent.ShadowMapSize.Y));
            
            _spotShadowMapShader.SetMatrix("ViewTransform", view);
            _spotShadowMapShader.SetMatrix("ProjectionTransform", projection);

            foreach (var component in World.CurrentLevel.StaticMeshComponents)
            {
                if (component.IsDestoryed)
                    continue;
                if (component.IsCastShadowMap == false)
                    continue;
                _spotShadowMapShader.SetMatrix("ModelTransform", component.WorldTransform);
                component.Render(deltaTime);
            }

            _instanceSpotShadowMapShader.SetMatrix("ViewTransform", view);
            _instanceSpotShadowMapShader.SetMatrix("ProjectionTransform", projection);
            foreach (var ism in World.CurrentLevel.IsmComponents)
            {
                if (ism.IsDestoryed)
                    continue;
                if (ism.IsCastShadowMap == false)
                    continue;
                GetPlanes(view * projection, ref _planes);
                if (ism is HierarchicalInstancedStaticMeshComponent hism)
                    hism.CameraCulling(_planes);
                ism.RenderISM(CurrentCameraComponent, deltaTime);
            }

            _skeletalMeshSpotShadowMapShader.SetMatrix("ViewTransform", view);
            _skeletalMeshSpotShadowMapShader.SetMatrix("ProjectionTransform", projection);
            foreach (var component in World.CurrentLevel.SkeletalMeshComponents)
            {
                if (component.IsDestoryed)
                    continue;
                if (component.IsCastShadowMap == false)
                    continue;
                if (component.AnimSampler != null && component.SkeletalMesh != null && component.SkeletalMesh.Skeleton != null)
                {
                    for (int i = 0; i < component.SkeletalMesh.Skeleton.BoneList.Count; i++)
                    {
                        _skeletalMeshSpotShadowMapShader.SetMatrix($"AnimTransform[{i}]", component.SkeletalMesh.Skeleton.BoneList[i].WorldToLocalTransform * component.AnimBuffer[i]);
                    }
                }
                _skeletalMeshSpotShadowMapShader.SetMatrix("ModelTransform", component.WorldTransform);
                component.Render(deltaTime);
            }

        }

        _spotLightingShader.UnUse();
        gl.PopGroup();
    }

    private void PointLightShadowMap(double deltaTime)
    {

        if (CurrentCameraComponent == null)
            return;
        gl.PushGroup("PointLight");
        _pointLightShadowShader.Use();
        Span<Matrix4x4> views = stackalloc Matrix4x4[6];
        foreach (var pointLightComponent in _pointLightComponents)
        {
            _tmpCullingStaticMesh.Clear();
            World.CurrentLevel.RenderObjectOctree.SphereCulling(_tmpCullingStaticMesh, new Physics.Sphere() { Location = pointLightComponent.WorldLocation, Radius = pointLightComponent.AttenuationRadius * 4 });
          
            if (pointLightComponent.IsCastShadowMap == false)
            {
                continue;
            }
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(95f.DegreeToRadians(), 1, 1, 1000);

            views[0] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(0, 0, -1), new Vector3(0, 1, 0));
            views[1] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(0, 0, 1), new Vector3(0, 1, 0));
            views[2] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(-1, 0, 0), new Vector3(0, 1, 0));
            views[3] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(1, 0, 0), new Vector3(0, 1, 0));
            views[4] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            views[5] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(0, -1, 0), new Vector3(0, 0, -1));


            gl.Viewport(new Rectangle(0, 0, pointLightComponent.ShadowMapSize.X, pointLightComponent.ShadowMapSize.Y));
            for (int i = 0; i < 6; i++)
            {
                gl.PushGroup("face:" + i);
                gl.BindFramebuffer(GLEnum.Framebuffer, pointLightComponent.ShadowMapFrameBufferIDs[i]);
                gl.Clear(ClearBufferMask.DepthBufferBit);
                _pointLightShadowShader.SetMatrix("ViewTransform", views[i]);
                _pointLightShadowShader.SetMatrix("ProjectionTransform", projection);
                foreach (var component in _cullingResult)
                {
                    if (component is not StaticMeshComponent)
                        continue;
                    if (component.IsDestoryed)
                        continue;
                    if (component.IsCastShadowMap == false)
                        continue;
                    _pointLightShadowShader.SetMatrix("ModelTransform", component.WorldTransform);
                    component.Render(deltaTime);
                }

                _instancePointLightingShader.SetMatrix("ViewTransform", views[i]);
                _instancePointLightingShader.SetMatrix("ProjectionTransform", projection);
                foreach (var hism in World.CurrentLevel.IsmComponents)
                {
                    if (hism.IsDestoryed)
                        continue;
                    if (hism.IsCastShadowMap == false)
                        continue;
                    hism.RenderISM(CurrentCameraComponent, deltaTime);
                }


                _skeletalMeshPointLightingShader.SetMatrix("ViewTransform", views[i]);
                _skeletalMeshPointLightingShader.SetMatrix("ProjectionTransform", projection);
                _skeletalMeshPointLightingShader.Use();
                foreach (var component in World.CurrentLevel.SkeletalMeshComponents)
                {
                    if (component.IsDestoryed)
                        continue;
                    if (component.IsCastShadowMap == false)
                        continue;
                    if (component.AnimSampler != null && component.SkeletalMesh != null && component.SkeletalMesh.Skeleton != null)
                    {
                        for (int j = 0; j < component.SkeletalMesh.Skeleton.BoneList.Count; j++)
                        {
                            _skeletalMeshPointLightingShader.SetMatrix($"AnimTransform[{j}]", component.SkeletalMesh.Skeleton.BoneList[j].WorldToLocalTransform * component.AnimBuffer[j]);
                        }
                    }
                    _skeletalMeshPointLightingShader.SetMatrix("ModelTransform", component.WorldTransform);
                    component.Render(deltaTime);
                }
                gl.PopGroup();
            }

        }
        _pointLightShadowShader.UnUse();
        gl.PopGroup();
    }
    private void DirectionLightShadowMap(double deltaTime)
    {
        if (CurrentCameraComponent == null)
            return;
        gl.PushGroup("DirectionLight");
        _dlShadowMapShader.Use();
        foreach (var directionalLight in World.CurrentLevel.DirectionLightComponents)
        {
            if (directionalLight.IsCastShadowMap == false)
            {
                continue;
            }
            gl.Viewport(new Rectangle(0, 0, directionalLight.ShadowMapSize.X, directionalLight.ShadowMapSize.Y));
            gl.BindFramebuffer(GLEnum.Framebuffer, directionalLight.ShadowMapFrameBufferID);

            var lightLocation = CurrentCameraComponent.WorldLocation - directionalLight.ForwardVector * 20;
            var view = Matrix4x4.CreateLookAt(lightLocation, lightLocation + directionalLight.ForwardVector, directionalLight.UpVector);
            var projection = Matrix4x4.CreateOrthographic(100, 100, 1.0f, 100f);
            gl.Clear(ClearBufferMask.DepthBufferBit);
            _dlShadowMapShader.SetMatrix("ViewTransform", view);
            _dlShadowMapShader.SetMatrix("ProjectionTransform", projection);
            foreach (var component in World.CurrentLevel.StaticMeshComponents)
            {
                if (component.IsDestoryed)
                    continue;
                if (component.IsCastShadowMap == false)
                    continue;
                _dlShadowMapShader.SetMatrix("ModelTransform", component.WorldTransform);
                component.Render(deltaTime);
            }

            _instanceDlShadowMapShader.SetMatrix("ViewTransform", view);
            _instanceDlShadowMapShader.SetMatrix("ProjectionTransform", projection);
            foreach (var ism in World.CurrentLevel.IsmComponents)
            {
                if (ism.IsDestoryed)
                    continue;
                if (ism.IsCastShadowMap == false)
                    continue;
                GetPlanes(view * projection, ref _planes);
                if (ism is HierarchicalInstancedStaticMeshComponent hism)
                    hism.CameraCulling(_planes);
                ism.RenderISM(CurrentCameraComponent, deltaTime);
            }


            _skeletalMeshDlShadowMapShader.SetMatrix("ViewTransform", view);
            _skeletalMeshDlShadowMapShader.SetMatrix("ProjectionTransform", projection);
            foreach (var component in World.CurrentLevel.SkeletalMeshComponents)
            {
                if (component.IsDestoryed)
                    continue;
                if (component.IsCastShadowMap == false)
                    continue;
                if (component.AnimSampler != null && component.SkeletalMesh != null && component.SkeletalMesh.Skeleton != null)
                {
                    for (int i = 0; i < component.SkeletalMesh.Skeleton.BoneList.Count; i++)
                    {
                        _skeletalMeshDlShadowMapShader.SetMatrix($"AnimTransform[{i}]", component.SkeletalMesh.Skeleton.BoneList[i].WorldToLocalTransform * component.AnimBuffer[i]);
                    }
                }
                _skeletalMeshDlShadowMapShader.SetMatrix("ModelTransform", component.WorldTransform);
                component.Render(deltaTime);
            }
        }
        _dlShadowMapShader.UnUse();
        gl.PopGroup();
    }
    private void BasePass(double deltaTime)
    {
        // 生成GBuffer
        gl.PushGroup("Base Pass");
        using (_globalBuffer.Begin())
        {

            gl.Enable(EnableCap.DepthTest);
            gl.ClearColor(Color.FromArgb(0,0,0,0));
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            gl.Enable(GLEnum.CullFace);
            gl.CullFace(GLEnum.Back);
            gl.Disable(EnableCap.Blend);
            if (CurrentCameraComponent != null)
            {
                _staticMeshBaseShader.SetInt("BaseColorTexture", 0);
                _staticMeshBaseShader.SetInt("NormalTexture", 1);
                _staticMeshBaseShader.SetInt("ARMTexture", 2);
                _staticMeshBaseShader.SetInt("ParallaxTexture", 3);
                _staticMeshBaseShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
                _staticMeshBaseShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
                _staticMeshBaseShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);


                foreach (var component in _staticMeshComponents)
                {
                    if (component.IsDestoryed == false)
                    {
                        _staticMeshBaseShader.SetMatrix("ModelTransform", component.WorldTransform);
                        _staticMeshBaseShader.SetMatrix("NormalTransform", component.NormalTransform);
                        component.Render(deltaTime);
                    }
                }

                _skeletalMeshBaseShader.SetInt("BaseColorTexture", 0);
                _skeletalMeshBaseShader.SetInt("NormalTexture", 1);
                _skeletalMeshBaseShader.SetInt("ARMTexture", 2);
                _skeletalMeshBaseShader.SetInt("ParallaxTexture", 3);
                _skeletalMeshBaseShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
                _skeletalMeshBaseShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
                _skeletalMeshBaseShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);

                foreach (var component in World.CurrentLevel.SkeletalMeshComponents)
                {
                    if (component.IsDestoryed == false)
                    {
                        if (component.SkeletalMesh != null && component.SkeletalMesh.Skeleton != null)
                        {
                            if (component.AnimSampler != null)
                            {

                                for (int i = 0; i < component.SkeletalMesh.Skeleton.BoneList.Count; i++)
                                {
                                    _skeletalMeshBaseShader.SetMatrix($"AnimTransform[{i}]", component.SkeletalMesh.Skeleton.BoneList[i].WorldToLocalTransform * component.AnimBuffer[i]);
                                }
                            }
                            else
                            {
                                for (int i = 0; i < component.SkeletalMesh.Skeleton.BoneList.Count; i++)
                                {
                                    _skeletalMeshBaseShader.SetMatrix($"AnimTransform[{i}]", Matrix4x4.Identity);
                                }
                            }
                        }
                        _skeletalMeshBaseShader.SetMatrix("ModelTransform", component.WorldTransform);
                        _skeletalMeshBaseShader.SetMatrix("NormalTransform", component.NormalTransform);
                        component.Render(deltaTime);
                    }
                }

                gl.Disable(GLEnum.CullFace);
                gl.PushGroup("InstancedStaticMesh Render");
                _hismShader.SetInt("BaseColorTexture", 0);
                _hismShader.SetInt("NormalTexture", 1);
                _hismShader.SetInt("ARMTexture", 2);
                _hismShader.SetInt("ParallaxTexture", 3);
                _hismShader.SetMatrix("ViewTransform", CurrentCameraComponent.View);
                _hismShader.SetMatrix("ProjectionTransform", CurrentCameraComponent.Projection);
                _hismShader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
                foreach (var ism in World.CurrentLevel.IsmComponents)
                {
                    if (ism is HierarchicalInstancedStaticMeshComponent hism)
                        hism.CameraCulling(CurrentCameraComponent.GetPlanes());
                    ism.RenderISM(CurrentCameraComponent, deltaTime);
                }
                gl.PopGroup();
            }
        }
        gl.PopGroup();
    }

    private unsafe void RenderToCameraRenderTarget(double deltaTime, RenderTarget colorBuffer)
    {
        if (CurrentCameraComponent == null)
            return;
        using (CurrentCameraComponent.RenderTarget.Begin())
        {
            gl.PushGroup("Skybox Pass");

            gl.Enable(EnableCap.Blend);
            gl.BlendEquation(GLEnum.FuncAdd);
            gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

            // 天空盒
            SkyboxPass(deltaTime);
            gl.PopGroup();

            gl.PushGroup("RenderToCamera Pass");
            gl.Clear(ClearBufferMask.DepthBufferBit);
            _renderToCamera.Use();
            _renderToCamera.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = colorBuffer.Width / (float)colorBuffer.BufferWidth,
                    Y = colorBuffer.Height / (float)colorBuffer.BufferHeight
                });
            _renderToCamera.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, colorBuffer.GBufferIds[0]);


            gl.BindVertexArray(_postProcessVao);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);

            _renderToCamera.UnUse();
            gl.PopGroup();

        }
    }

    private void LightingPass(RenderTarget? aoRenderTarget)
    {
        if (CurrentCameraComponent == null)
            return;

        gl.PushGroup("Lighting Pass");
        // 清除背景颜色
        gl.ClearColor(Color.FromArgb(0, 0, 0, 0));
        gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
        gl.Disable(EnableCap.DepthTest);
        gl.BlendEquation(GLEnum.FuncAdd);
        gl.BlendFunc(GLEnum.One, GLEnum.One);
        gl.Enable(EnableCap.Blend);

        IndirectLight(aoRenderTarget);
        // 定向光
        DirectionalLight();
        // 点光源
        PointLight();
        // 聚光
        SpotLight();
        gl.Disable(EnableCap.Blend);

        gl.PopGroup();
    }


    private unsafe void DirectionalLight()
    {
        if (CurrentCameraComponent == null)
            return;
        gl.PushGroup("DirectionalLight Pass");

        foreach (var directionalLight in World.CurrentLevel.DirectionLightComponents)
        {
            Shader shader = directionalLight.IsCastShadowMap ? _directionalLightingShader : _directionalLightingShaderNoShadow;
            var lightInfo = directionalLight.LightInfo;
            Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var vpInvert);
            shader.SetMatrix("VPInvert", vpInvert);

            var lightLocation = CurrentCameraComponent.WorldLocation - directionalLight.ForwardVector * 20;
            var view = Matrix4x4.CreateLookAt(lightLocation, lightLocation + directionalLight.ForwardVector, directionalLight.UpVector);
            var projection = Matrix4x4.CreateOrthographic(100, 100, 1.0f, 100f);

            var worldToLight = view * projection;
            shader.SetMatrix("WorldToLight", worldToLight);
            shader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = _globalBuffer.Width / (float)_globalBuffer.BufferWidth,
                    Y = _globalBuffer.Height / (float)_globalBuffer.BufferHeight
                });


            shader.SetFloat("LightStrength", directionalLight.LightStrength);
            shader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[0]);

            if (_isMicroGBuffer == false)
            {
                shader.SetInt("CustomBuffer", 1);
                gl.ActiveTexture(GLEnum.Texture1);
                gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[1]);
            }


            shader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, _globalBuffer.DepthId);
            if (directionalLight.IsCastShadowMap)
            {
                shader.SetInt("ShadowMapTexture", 3);
                gl.ActiveTexture(GLEnum.Texture3);
                gl.BindTexture(GLEnum.Texture2D, directionalLight.ShadowMapTextureID);
            }

            shader.SetVector3("LightDirection", lightInfo.Direction);
            shader.SetVector3("LightColor", lightInfo.Color);

            shader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
            gl.BindVertexArray(_postProcessVao);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);
        }
        gl.PopGroup();
    }

    private unsafe void BloomPass(RenderTarget tmpBuffer1, RenderTarget tmpBuffer2, RenderTarget colorBuffer)
    {
        gl.PushGroup("Bloom Effect");
        if (CurrentCameraComponent == null) return;
        gl.Disable(EnableCap.DepthTest);

        gl.Enable(EnableCap.Blend);
        gl.BlendEquation(GLEnum.FuncAdd);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        using (tmpBuffer1.Begin())
        {
            gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
            _bloomPreShader.Use();
            _bloomPreShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = colorBuffer.Width / (float)colorBuffer.BufferWidth,
                    Y = colorBuffer.Height / (float)colorBuffer.BufferHeight
                });
            _bloomPreShader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, colorBuffer.GBufferIds[0]);

            gl.BindVertexArray(_postProcessVao);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);
            _bloomPreShader.UnUse();
        }

        Span<RenderTarget> buffer =
        [
            tmpBuffer2,
            tmpBuffer1
        ];
        for (int i = 0; i < 2; i++)
        {
            int next = i == 1 ? 0 : 1;
            using(buffer[i].Begin())
            {
                gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
                _bloomShader.Use();
                _bloomShader.SetVector2("TexCoordScale",
                    new Vector2
                    {
                        X = buffer[next].Width / (float)buffer[next].BufferWidth,
                        Y = buffer[next].Height / (float)buffer[next].BufferHeight
                    });
                _bloomShader.SetInt("horizontal", i);
                _bloomShader.SetInt("ColorTexture", 0);
                gl.ActiveTexture(GLEnum.Texture0);
                gl.BindTexture(GLEnum.Texture2D, buffer[next].GBufferIds[0]);

                gl.BindVertexArray(_postProcessVao);
                gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
                gl.ActiveTexture(GLEnum.Texture0);
                _bloomShader.UnUse();
            }
        }
        using (colorBuffer.Begin()) 
        {
            gl.Enable(EnableCap.Blend);
            gl.BlendEquation(GLEnum.FuncAdd);
            _bloomPostShader.Use();
            _bloomPostShader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = tmpBuffer1.Width / (float)tmpBuffer1.BufferWidth,
                    Y = tmpBuffer1.Height / (float)tmpBuffer1.BufferHeight
                });
            _bloomPostShader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, tmpBuffer2.GBufferIds[0]);

            gl.BindVertexArray(_postProcessVao);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);
            _bloomPostShader.UnUse();

        }
        gl.PopGroup();
    }

    public unsafe void PointLight()
    {
        if (CurrentCameraComponent == null)
            return;

        Span<Matrix4x4> views = stackalloc Matrix4x4[6];
        gl.PushGroup("PointLight Pass");
        _pointLightingShader.Use();
        foreach (var pointLightComponent in _pointLightComponents)
        {
            Shader shader = pointLightComponent.IsCastShadowMap ? _pointLightingShader : _pointLightingShaderNoShadow;

            views[0] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(0, 0, -1), new Vector3(0, 1, 0));
            views[1] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(0, 0, 1), new Vector3(0, 1, 0));
            views[2] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(-1, 0, 0), new Vector3(0, 1, 0));
            views[3] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(1, 0, 0), new Vector3(0, 1, 0));
            views[4] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            views[5] = Matrix4x4.CreateLookAt(pointLightComponent.WorldLocation, pointLightComponent.WorldLocation + new Vector3(0, -1, 0), new Vector3(0, 0, -1));


            Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var vpInvert);
            shader.SetMatrix("VPInvert", vpInvert);

            shader.SetFloat("LightStrength", pointLightComponent.LightStrength);
            shader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = _globalBuffer.Width / (float)_globalBuffer.BufferWidth,
                    Y = _globalBuffer.Height / (float)_globalBuffer.BufferHeight
                });

            shader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[0]);


            if (_isMicroGBuffer == false)
            {

                shader.SetInt("CustomBuffer", 1);
                gl.ActiveTexture(GLEnum.Texture1);
                gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[1]);
            }

            shader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, _globalBuffer.DepthId);


            if (pointLightComponent.IsCastShadowMap)
            {
                for (int i = 0; i < 6; i++)
                {
                    var projection = Matrix4x4.CreatePerspectiveFieldOfView(95F.DegreeToRadians(), 1, 1, 1000);
                    shader.SetMatrix($"WorldToLights[{i}]", views[i] * projection);
                    shader.SetInt($"ShadowMapTextures{i}", 5 + i);
                    gl.ActiveTexture(GLEnum.Texture5 + i);
                    gl.BindTexture(GLEnum.Texture2D, pointLightComponent.ShadowMapTextureIDs[i]);
                }
            }

            shader.SetVector3("LightLocation", pointLightComponent.WorldLocation);
            shader.SetVector3("LightColor", pointLightComponent._Color);
            
            shader.SetFloat("FalloffRadius", pointLightComponent.AttenuationRadius);
            shader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
            gl.BindVertexArray(_postProcessVao);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);


        }
        gl.PopGroup();
    }


    public unsafe void SpotLight()
    {

        gl.PushGroup("SpotLight Pass");
        if (CurrentCameraComponent == null)
            return;
        _spotLightingShader.Use();
        foreach (var spotLightComponent in World.CurrentLevel.SpotLightComponents)
        {
            Shader shader = spotLightComponent.IsCastShadowMap ? _spotLightingShader : _spotLightingShaderNoShadow;
            shader.SetFloat("LightStrength", spotLightComponent.LightStrength);
            Matrix4x4.Invert(CurrentCameraComponent.View * CurrentCameraComponent.Projection, out var vpInvert);
            shader.SetMatrix("VPInvert", vpInvert);


            shader.SetVector2("TexCoordScale",
                new Vector2
                {
                    X = _globalBuffer.Width / (float)_globalBuffer.BufferWidth,
                    Y = _globalBuffer.Height / (float)_globalBuffer.BufferHeight
                });



            var view = Matrix4x4.CreateLookAt(spotLightComponent.WorldLocation, spotLightComponent.WorldLocation + spotLightComponent.ForwardVector, spotLightComponent.UpVector);
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(spotLightComponent.OuterAngle.DegreeToRadians(), 1, 1F, 100);
            var worldToLight = view * projection;
            shader.SetMatrix("WorldToLight", worldToLight);

            shader.SetFloat("InnerCosine", (float)Math.Cos(spotLightComponent.InnerAngle.DegreeToRadians()));

            shader.SetFloat("OuterCosine", (float)Math.Cos(spotLightComponent.OuterAngle.DegreeToRadians()));
            shader.SetVector3("ForwardVector", spotLightComponent.ForwardVector);

            shader.SetInt("ColorTexture", 0);
            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[0]);

            if (_isMicroGBuffer == false)
            {
                shader.SetInt("CustomBuffer", 1);
                gl.ActiveTexture(GLEnum.Texture1);
                gl.BindTexture(GLEnum.Texture2D, _globalBuffer.GBufferIds[1]);
            }


            shader.SetInt("DepthTexture", 2);
            gl.ActiveTexture(GLEnum.Texture2);
            gl.BindTexture(GLEnum.Texture2D, _globalBuffer.DepthId); 

            if (spotLightComponent.IsCastShadowMap)
            {
                shader.SetInt("ShadowMapTexture", 3);
                gl.ActiveTexture(GLEnum.Texture3);
                gl.BindTexture(GLEnum.Texture2D, spotLightComponent.ShadowMapTextureID);
            }

            shader.SetVector3("LightLocation", spotLightComponent.WorldLocation);
            shader.SetVector3("LightColor", spotLightComponent._Color);

            shader.SetVector3("CameraLocation", CurrentCameraComponent.WorldLocation);
            gl.BindVertexArray(_postProcessVao);
            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
            gl.ActiveTexture(GLEnum.Texture0);


        }
        gl.PopGroup();
    }


    private void CameraCulling()
    {
        if (CurrentCameraComponent == null)
            return;
        _cullingResult.Clear();
        _pointLightComponents.Clear();
        _staticMeshComponents.Clear();
        _decalComponents.Clear();

        World.CurrentLevel.RenderObjectOctree.FrustumCulling(_cullingResult, CurrentCameraComponent.GetPlanes());

        foreach(var component in _cullingResult)
        {
            switch (component)
            {
                case StaticMeshComponent staticMeshComponent:
                    _staticMeshComponents.Add(staticMeshComponent);
                    break;
                case PointLightComponent pointLightComponent:
                    _pointLightComponents.Add(pointLightComponent);
                    break;
                case DecalComponent decalComponent:
                    _decalComponents.Add(decalComponent);
                    break;
            }
        }
    }

    private readonly List<PrimitiveComponent> _cullingResult = [];

    private readonly List<StaticMeshComponent> _tmpCullingStaticMesh = [];

    private readonly List<StaticMeshComponent> _staticMeshComponents = [];

    private readonly List<PointLightComponent> _pointLightComponents = [];
    private readonly List<DecalComponent> _decalComponents = [];

    public DeferredSceneRenderer(Shader instanceDlShadowMapShader, Shader renderToCamera, Shader hismShader, Shader decalPostShader, RenderTarget globalBuffer, Shader staticMeshBaseShader, Shader directionalLightingShader, Shader spotLightingShader, Shader pointLightingShader, Shader directionalLightingShaderNoShadow, Shader spotLightingShaderNoShadow, Shader pointLightingShaderNoShadow, Shader dlShadowMapShader, Shader spotShadowMapShader, Shader instanceSpotShadowMapShader, Shader instancePointLightingShader, Shader skyboxShader, Shader pointLightShadowShader, Shader bloomPreShader, Shader bloomShader, Shader decalShader, Shader ssaoShader, Shader skeletalMeshDlShadowMapShader, Shader skeletalMeshSpotShadowMapShader, Shader skeletalMeshPointLightingShader, Shader skeletalMeshBaseShader, Shader bloomPostShader, Shader irradianceShader, Shader prefilterShader, Shader indirectLightShader, Shader hdri2CubeMapShader, RenderTarget postProcessBuffer1, World world, Texture brdfTexture)
    {
        _instanceDlShadowMapShader = instanceDlShadowMapShader;
        _renderToCamera = renderToCamera;
        _hismShader = hismShader;
        _postProcessEbo = 0;
        _isMobile = false;
        _isMicroGBuffer = false;
        _cubeVbo = 0;
        _decalPostShader = decalPostShader;
        _globalBuffer = globalBuffer;
        _staticMeshBaseShader = staticMeshBaseShader;
        _directionalLightingShader = directionalLightingShader;
        _spotLightingShader = spotLightingShader;
        _pointLightingShader = pointLightingShader;
        _directionalLightingShaderNoShadow = directionalLightingShaderNoShadow;
        _spotLightingShaderNoShadow = spotLightingShaderNoShadow;
        _pointLightingShaderNoShadow = pointLightingShaderNoShadow;
        _dlShadowMapShader = dlShadowMapShader;
        _spotShadowMapShader = spotShadowMapShader;
        _instanceSpotShadowMapShader = instanceSpotShadowMapShader;
        _instancePointLightingShader = instancePointLightingShader;
        _skyboxShader = skyboxShader;
        _pointLightShadowShader = pointLightShadowShader;
        _bloomPreShader = bloomPreShader;
        _bloomShader = bloomShader;
        _decalShader = decalShader;
        _ssaoShader = ssaoShader;
        _skeletalMeshDlShadowMapShader = skeletalMeshDlShadowMapShader;
        _skeletalMeshSpotShadowMapShader = skeletalMeshSpotShadowMapShader;
        _skeletalMeshPointLightingShader = skeletalMeshPointLightingShader;
        _skeletalMeshBaseShader = skeletalMeshBaseShader;
        _bloomPostShader = bloomPostShader;
        IrradianceShader = irradianceShader;
        PrefilterShader = prefilterShader;
        IndirectLightShader = indirectLightShader;
        Hdri2CubeMapShader = hdri2CubeMapShader;
        _postProcessBuffer1 = postProcessBuffer1;
        World = world;
        _brdfTexture = brdfTexture;
    }
}

public struct DeferredVertex
{
    public Vector3 Location;
    public Vector2 TexCoord;
}

