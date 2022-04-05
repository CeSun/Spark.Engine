using Silk.NET.OpenGL;
using Shader = LiteEngine.Core.Resources.Shader;

namespace LiteEngine.Core.Resources;

public class Material
{
    public List<Texture> Textures { get; set; }
    public Shader Shader { get; set; }

    static GL gl { get => Engine.Instance.Gl; }
    public Material(List<Texture>? textures, Shader shader) : this()
    {
        if (textures != null)
            Textures.AddRange(textures);
        Shader = shader;
    }
    public Material()
    {
        Textures = new List<Texture>();
        Shader = Shader.LoadShader("default");
    }

    public void Use()
    {
        Shader.Use();
        if (Textures == null)
            return;
        for(int i = 0; i < Textures.Count; i++)
        {
            if (i > 31)
                throw new("单个材质纹理数量不能大于31");
            gl.ActiveTexture(GLEnum.Texture0 + i);
            Textures[i].Use();
        }
    }

}
