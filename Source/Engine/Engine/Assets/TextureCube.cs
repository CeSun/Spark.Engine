using Silk.NET.OpenGLES;
using StbImageSharp;
using System.Text.Json.Nodes;
using Spark.Engine.Platform;
using System.Runtime.InteropServices;

namespace Spark.Engine.Assets;


public class SubTexture : AssetBase
{
    public uint Width { get;  set; }
    public uint Height { get;  set; }
    public List<byte> Pixels { get; set; } = new List<byte>();

    public TexChannel Channel;

    public GLEnum Target;

    public override void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteUInt32(Width);
        bw.WriteUInt32(Height);
        bw.WriteInt32((int)Channel);
        bw.WriteInt32((int)Target);
        bw.WriteInt32(Pixels.Count);
        bw.Write(Pixels.ToArray());
    }

    public override void Deserialize(BinaryReader br, Engine engine)
    {
        Width = br.ReadUInt32();
        Height = br.ReadUInt32();
        Channel = (TexChannel)br.ReadInt32();
        Target = (GLEnum)br.ReadInt32();
        var pixelsLen = br.ReadInt32();
        Pixels.AddRange(br.ReadBytes(pixelsLen));
    }
}
public class TextureCube : ISerializable
{
    static GLEnum[] TexTargets = new[]
    {
        GLEnum.TextureCubeMapPositiveX,
        GLEnum.TextureCubeMapNegativeX,

        GLEnum.TextureCubeMapPositiveY,
        GLEnum.TextureCubeMapNegativeY,

        GLEnum.TextureCubeMapPositiveZ,
        GLEnum.TextureCubeMapNegativeZ
    };
    static string[] Attributes = new[]{
            "Right",
            "Left",
            "Up",
            "Down",
            "Back",
            "Front",
        };

    public TextureCube()
    {

    }

    public uint TextureId;

    List<SubTexture> Textures = new List<SubTexture>();

    private static SubTexture LoadSubTexture(string Path)
    {
        using var StreamReader = FileSystem.Instance.GetStreamReader("Content" + Path);
        var imageResult = ImageResult.FromStream(StreamReader.BaseStream);
        if (imageResult != null)
        {
            SubTexture texture = new SubTexture();
            texture.Width = (uint)imageResult.Width;
            texture.Height = (uint)imageResult.Height;
            texture.Channel = imageResult.Comp.ToTexChannel();
            texture.Pixels.AddRange(imageResult.Data);
            return texture;
        }
        throw new Exception("Load Texture error");
    }
    public async static Task<TextureCube> LoadAsync(string Path)
    {
        return await Task.Run(() => Load(Path));
    }

    public unsafe static TextureCube Load(string Path)
    {
        TextureCube textureCube = new TextureCube();
        using var sr = FileSystem.Instance.GetStreamReader("Content" + Path + ".TextureCube");

        var jstext = sr.ReadToEnd();
        var Object = JsonNode.Parse(jstext);
        string jpgpath = "";
        var strs = Path.Split("/");
        if (strs.Length > 1)
            jpgpath = string.Join("/", strs.Take(strs.Length - 1));
        else
            jpgpath = "/";
        if (Object == null)
            throw new Exception("Object is null");
        for (int i = 0; i < Attributes.Length; i++)
        {
            var path = Object[Attributes[i]];
            if (path == null)
                throw new Exception($"{Attributes[i]} attribute is null");

            using var StreamReader = FileSystem.Instance.GetStreamReader("Content" + jpgpath + "/" + path.ToString());
            var imageResult = ImageResult.FromStream(StreamReader.BaseStream);
            if (imageResult == null)
                throw new Exception("Load Texture error");

            SubTexture texture = new SubTexture();
            texture.Width = (uint)imageResult.Width;
            texture.Height = (uint)imageResult.Height;
            texture.Channel = imageResult.Comp.ToTexChannel();
            texture.Pixels.AddRange(imageResult.Data);
            texture.Target = TexTargets[i];
            textureCube.Textures.Add(texture);


        }
        return textureCube;
    }
    

    public unsafe void InitRender(GL gl)
    {

        if (TextureId > 0)
            return;
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, TextureId);
        
        foreach(var tex in Textures)
        {

            fixed (void* data = CollectionsMarshal.AsSpan(tex.Pixels))
            {
                gl.TexImage2D(tex.Target, 0, (int)tex.Channel.ToGLEnum(), tex.Width, tex.Height, 0, tex.Channel.ToGLEnum(), GLEnum.UnsignedByte, data);
            }
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

        }
        ReleaseMemory();
    }

    public void ReleaseMemory()
    {
        Textures = null;
    }

    public void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(MagicCode.TextureCube);
        bw.WriteInt32(Textures.Count);
        foreach(var texture in Textures)
        {
            texture.Serialize(bw, engine);
        }
    }

    public void Deserialize(BinaryReader br, Engine engine)
    {
        var AssetMagicCode = br.ReadInt32();
        if (AssetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var TextureMagicCode = br.ReadInt32();
        if (TextureMagicCode != MagicCode.TextureCube)
            throw new Exception("");
        var count = br.ReadInt32();
        for(int i = 0; i < count; i++)
        {
            var texture = new SubTexture();
            texture.Deserialize(br, engine);
            Textures.Add(texture);
        }
    }
}
