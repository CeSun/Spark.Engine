using Silk.NET.OpenGLES;
using StbImageSharp;
using System.Text.Json.Nodes;
using Spark.Engine.Platform;
using System.Runtime.InteropServices;

namespace Spark.Engine.Assets;


public class SubTexture : ISerializable
{
    public uint Width { get;  set; }
    public uint Height { get;  set; }
    public List<byte> Pixels { get; set; } = new List<byte>();

    public TexChannel Channel;

    public GLEnum Target;

    public void Serialize(StreamWriter Writer)
    {
        var bw = new BinaryWriter(Writer.BaseStream);
        bw.Write(BitConverter.GetBytes(Width));
        bw.Write(BitConverter.GetBytes(Height));
        bw.Write(BitConverter.GetBytes((int)Channel));
        bw.Write(BitConverter.GetBytes((int)Target));
        bw.Write(BitConverter.GetBytes(Pixels.Count));
        bw.Write(Pixels.ToArray());
    }

    public void Deserialize(StreamReader Reader)
    {
        var br = new BinaryReader(Reader.BaseStream);
        Width = br.ReadUInt32();
        Height = br.ReadUInt32();
        Channel = (TexChannel)br.ReadInt32();
        Target = (GLEnum)br.ReadInt32();
        var pixelsLen = br.ReadInt32();
        Pixels.AddRange(br.ReadBytes(pixelsLen));
    }
}
public class TextureCube
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

}
