using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using StbSharp;

namespace LiteEngine.Core.Resources;

public class Texture
{

    static GL gl { get => Engine.Instance.Gl; }

    static Dictionary<string, Texture> TexturePool = new Dictionary<string, Texture>();
    public static unsafe Texture LoadTexture(string path, bool isGenMipmap = false)
    {
        Texture? texture = null;
        if (TexturePool.TryGetValue(path, value: out texture))
        {
            return texture;
        }
        var data = Engine.Instance.FileSystem.LoadFile(path);
        var image = StbImage.LoadFromMemory(data);
        texture = new Texture();
        var id = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, id);
        gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapS, (int)TextureWrapMode.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapT, (int)TextureWrapMode.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest);
        var internalFormat = image.Comp switch
        {
            3 => InternalFormat.Rgb,
            4 => InternalFormat.Rgba,
            _ => throw new NotSupportedException("image channel error")
        } ;
        var format = image.Comp switch
        {
            3 => PixelFormat.Rgb,
            4 => PixelFormat.Rgba,
            _ => throw new NotSupportedException("image channel error")
        };

        fixed (void* d = image.Data)
        {
            gl.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, (uint)image.Width, (uint)image.Height, 0, format, PixelType.UnsignedByte, d);
        }
        if (isGenMipmap)
        {
            gl.GenerateMipmap(TextureTarget.Texture2D);
        }
        texture.Id = id;
        texture.Width = (uint)image.Width;
        texture.Height = (uint)image.Height;
        texture.Channel = (uint)image.Comp;

        TexturePool.Add(path, texture);
        return texture;
    }

    public void Use()
    {
        gl.BindTexture(TextureTarget.Texture2D, Id);
    }

    public void Clear()
    {
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }
    public uint Id { get; private set; }
    public uint Height { get; private set; }
    public uint Width { get; private set; }
    public uint Channel { get; private set; }

}
