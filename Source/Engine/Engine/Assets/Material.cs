namespace Spark.Engine.Assets;

public class Material : AssetBase
{
    public Texture?[] Textures = new Texture?[4];

    public Texture? BaseColor { get => Textures[0]; set => Textures[0] = value; }
    public Texture? Normal { get => Textures[1]; set => Textures[1] = value; }
    public Texture? Arm { get => Textures[2]; set => Textures[2] = value; }
    public Texture? Parallax { get => Textures[3]; set => Textures[3] = value; }
}
