using Silk.NET.OpenGLES;
using Spark.Engine.Platform;
using System.Runtime.InteropServices;

namespace Spark.Engine.Assets;

public class TextureHdr : Texture
{
    public IReadOnlyList<float> _pixels = [];
    public IReadOnlyList<float> Pixels 
    {
        get => _pixels;
        set
        {
            _pixels = value;
            var list = value.ToList();
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureHdrProxy>(this);
                if (proxy != null)
                {
                    proxy.Pixels = list;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });

        }
    
    }


  
}


public class TextureHdrProxy : TextureProxy
{
    public List<float> Pixels { get; set; } = new List<float>();
    public override unsafe void RebuildGpuResource(GL gl)
    {
        if (TextureId != 0)
        {
            gl.DeleteTexture(TextureId);
        }
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, TextureId);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)Filter.ToGlFilter());
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)Filter.ToGlFilter());
        fixed (void* p = CollectionsMarshal.AsSpan(Pixels))
        {
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgb16f, Width, Height, 0, Channel.ToGlEnum(), GLEnum.Float, p);
        }
        gl.BindTexture(GLEnum.Texture2D, 0);
    }
}