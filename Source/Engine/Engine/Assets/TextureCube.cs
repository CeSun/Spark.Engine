using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;

namespace Spark.Engine.Assets;

public class TextureCube : AssetBase
{

    public uint TextureId;

    public readonly Texture?[] Textures = new Texture?[6];

    public Texture? RightFace { get => Textures[0]; set => Textures[0] = value; }
    public Texture? LeftFace { get => Textures[1]; set => Textures[1] = value; }
    public Texture? UpFace { get => Textures[2]; set => Textures[2] = value; }
    public Texture? DownFace { get => Textures[3]; set => Textures[3] = value; }
    public Texture? FrontFace { get => Textures[4]; set => Textures[4] = value; }
    public Texture? BackFace { get => Textures[5]; set => Textures[5] = value; }

  
}


public class TextureCubeProxy : RenderProxy
{

    public uint TextureId;

    private static readonly GLEnum[] TexTargets =
    [
        GLEnum.TextureCubeMapPositiveX,
        GLEnum.TextureCubeMapNegativeX,

        GLEnum.TextureCubeMapPositiveY,
        GLEnum.TextureCubeMapNegativeY,

        GLEnum.TextureCubeMapPositiveZ,
        GLEnum.TextureCubeMapNegativeZ
    ];

    private static readonly string[] Attributes =
    [
        "Right",
        "Left",
        "Up",
        "Down",
        "Front",
        "Back"
    ];
    /*
    public unsafe override void RebuildGpuResource(GL gl)
    {
        if (TextureId > 0)
            return;
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, TextureId);

        for (int i = 0; i < 6; i++)
        {
            var tex = Textures[i];
            if (tex == null)
                continue;
            if (tex is TextureHdr textureHdr)
            {
                fixed (void* data = CollectionsMarshal.AsSpan(textureHdr.Pixels))
                {
                    gl.TexImage2D(TexTargets[i], 0, (int)tex.Channel.ToGlHdrEnum(), tex.Width, tex.Height, 0, tex.Channel.ToGlEnum(), GLEnum.Float, data);
                }

            }
            else if (tex is TextureLdr textureLdr)
            {
                fixed (void* data = CollectionsMarshal.AsSpan(textureLdr.Pixels))
                {
                    gl.TexImage2D(TexTargets[i], 0, (int)tex.Channel.ToGlEnum(), tex.Width, tex.Height, 0, tex.Channel.ToGlEnum(), GLEnum.UnsignedByte, data);
                }
            }
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        }
    }
    */
}