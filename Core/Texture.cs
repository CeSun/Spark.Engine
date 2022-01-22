using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StbSharp;
using OpenTK.Graphics.OpenGL4;

namespace LiteEngine.Core
{
    public class Texture
    {
        private Texture() { }

        private int _Id;
        public int Id { get => _Id; private set => _Id = value; }
        public static Texture Load(string path)
        {
            var texture = TexturePool.GetValueOrDefault(path);
            if (texture == null)
            {
                texture = new Texture();
                var sr = new StreamReader(path);
                var br = new BinaryReader(sr.BaseStream);
                long length = sr.BaseStream.Length;
                byte[] bytes = new byte[length];
                br.Read(bytes, 0, bytes.Length);
                var image = StbImage.LoadFromMemory(bytes);
                var id = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, id);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
                texture.Id = id;
            }
            return texture;
        }

        static Dictionary<string, Texture> TexturePool = new Dictionary<string, Texture>();


    }

}
