using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGLES;
using static Spark.Engine.StaticEngine;



namespace Spark.Engine.Render;

public class RenderTarget
{
    public RenderTarget(int width, int height, bool isRenderToViewPort)
    {
        IsRenderToViewPort = isRenderToViewPort;
        Width = width;
        Height = height;
        FboId = 0;
    }
    private uint FboId;

    private bool _IsRenderToViewPort;

    public bool IsRenderToViewPort
    {
        set
        {
            if (value != _IsRenderToViewPort)
            {
                _IsRenderToViewPort = value;
            }
        }

        get => _IsRenderToViewPort;
    }
    public int Width { get; set; }
    public int Height { get; set; }


    internal void RenderTo(Action action)
    {
        gl.BindFramebuffer(GLEnum.Framebuffer, FboId);
        action?.Invoke();
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);

    }
}
