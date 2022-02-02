using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace LiteEngine.Core
{
    public class Camera : GameObject
    {
        public static Camera Current { get => _camera; set => _camera = value; }
        private static Camera _camera = new Camera();

        public RenderTarget Target { get; private set; }

        public Camera(RenderTarget target = RenderTarget.Screen) : base("Camera")
        {
            Nearest = 0.01f;
            Furthest = 1000.0f;
            Fov = (float)((Math.PI / 180) * 60f);
            Parent = Scene.Current.Root;

            Layers = uint.MaxValue;
            Index = 0;
            ClearFlag = ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit;
            ClearColor = Color4.WhiteSmoke;
            Target = target;
            fbo = 0;
            if (target == RenderTarget.Texture)
            {
                var texId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, texId);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, Game.Instance.Size.X, Game.Instance.Size.Y, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)0);

                GL.BindTexture(TextureTarget.Texture2D, 0);

                var rbo = GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, rbo);
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent, Game.Instance.Size.X, Game.Instance.Size.Y);
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

                fbo = GL.GenFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texId, 0);
                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, rbo);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                OutputTeture = Texture.Create(texId);
            }
        }

        private int fbo;
        public Texture? OutputTeture { get; private set; }
        public uint Layers { get; set; }

        public void DrawScene(double deltaTime)
        {
            CurrentDrawCamera = this;
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.ClearColor(ClearColor);
            GL.Clear(ClearFlag);
            Draw_(Scene.Current.Root,deltaTime);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
        static internal Camera CurrentDrawCamera;
        public ClearBufferMask ClearFlag { get; set; }
        
        public Color4 ClearColor { get; set; }

        private void Draw_(GameObject obj, double delta)
        {
            if (obj == null)
                return;
            if (((uint)obj.Layer & Layers) != 0)
            {
                obj.Draw(delta);
            }
            obj.Foreach(o => Draw_(o, delta));
        }
        public int Index { get; set; }
        public Matrix4 ViewMat
        {
            get  {
                var transform = Transform;
                var Up = new Vector4 { X = 0, Y = 1, Z = 0, W = 1 } * transform;
                var Target = new Vector4 { X = 0, Y = 0, Z = 1, W = 1 } * transform;
                var Eye = new Vector4 { X = 0, Y = 0, Z = 0, W = 1 } * transform;
                Up = Up - Eye;
                return Matrix4.LookAt(new Vector3 { X = Eye.X, Y = Eye.Y, Z = Eye.Z }, new Vector3 { X = Target.X, Y = Target.Y, Z = Target.Z }, new Vector3 { X = Up.X, Y = Up.Y, Z = Up.Z });
            }
       
        }

        public Matrix4 PerspectiveMat
        {
            get
            {
                return Matrix4.CreatePerspectiveFieldOfView(Fov, Game.Instance.Size.X/(float)Game.Instance.Size.Y, Nearest, Furthest);
            }
        }

        public float Nearest { get; set; }
        public float Furthest { get; set; }
        public float Fov { get; set; }
    }

    [Flags]
    public enum RenderLayer
    {
        Layer1 = (1 << 0),
        Layer2 = (1 << 1),
        Layer3 = (1 << 2),
        Layer4 = (1 << 3),
        Layer5 = (1 << 4),
        Layer6 = (1 << 5),
        Layer7 = (1 << 6),
        Layer8 = (1 << 7),
        Layer9 = (1 << 8),
        Layer10 = (1 << 9),
        Layer11 = (1 << 10),
        Layer12 = (1 << 11),
        Layer13 = (1 << 12),

    }

    public enum RenderTarget
    {
        Screen,     // 在屏幕渲染
        Texture,    // 在纹理上渲染
    }
}
