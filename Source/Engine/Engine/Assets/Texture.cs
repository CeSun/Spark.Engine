using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public abstract class Texture : AssetBase
{
    public uint _width;
    public uint _height;

    public TexChannel _channel;
    public TexFilter _filter = TexFilter.Liner;

    public uint Width 
    { 
        get => _width; 
        set
        {
            _width = value;
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureProxy>(this);
                if (proxy != null)
                {
                    proxy.Width = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }
    public uint Height 
    { 
        get => _height;
        set
        {
            _height = value;
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureProxy>(this);
                if (proxy != null)
                {
                    proxy.Height = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }

    public TexChannel Channel 
    { 
        get => _channel;
        set 
        {
            _channel = value;
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureProxy>(this);
                if (proxy != null)
                {
                    proxy.Channel = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }
    public TexFilter Filter 
    { 
        get => _filter; 
        set
        {
            _filter = value;
            AssetModify(render =>
            {
                var proxy = render.GetProxy<TextureProxy>(this);
                if (proxy != null)
                {
                    proxy.Filter = value;
                    render.AddNeedRebuildRenderResourceProxy(proxy);
                }
            });
        }
    }
}


public abstract class TextureProxy : RenderProxy
{
    public uint TextureId { get; protected set; }
    public uint Width { get; set; }
    public uint Height { get; set; }

    public TexChannel Channel;
    public TexFilter Filter { get; set; } = TexFilter.Liner;

}