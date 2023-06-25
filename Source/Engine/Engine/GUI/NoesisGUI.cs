using Silk.NET.Input;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;
namespace Spark.Engine.GUI;

public class NoesisGUI
{
    static Noesis.View _view = default;
    public void Init()
    {
        Noesis.Log.SetLogCallback((level, channel, message) =>
        {
            if (channel == "")
            {
                // [TRACE] [DEBUG] [INFO] [WARNING] [ERROR]
                string[] prefixes = new string[] { "T", "D", "I", "W", "E" };
                string prefix = (int)level < prefixes.Length ? prefixes[(int)level] : " ";
                Console.WriteLine("[NOESIS/" + prefix + "] " + message);
            }
        });

        // Noesis initialization. This must be the first step before using any NoesisGUI functionality
        Noesis.GUI.SetLicense("Ce Sun", "JpZpg03aaYMzQP6TCJturoZUtpPys56lIS3O3DKAJlhYkJ/l");
        Noesis.GUI.Init();

        // Setup theme
        NoesisApp.Application.SetThemeProviders();
        Noesis.GUI.LoadApplicationResources("/Noesis.GUI.Extensions;component/Theme/NoesisTheme.DarkBlue.xaml");
        MainMouse.MouseMove += (m, v) => OnMouseMove((int)v.X, (int)v.Y);
        MainMouse.MouseDown += (m, button) => OnMouseButtonDown(button, (int)m.Position.X, (int)m.Position.Y);
        MainMouse.MouseUp += (m, button) => OnMouseButtonUp(button, (int)m.Position.X, (int)m.Position.Y);
        Noesis.Grid xaml = (Noesis.Grid)Noesis.GUI.ParseXaml(@"
                <Grid xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
                    
                    <Viewbox>
                        <StackPanel Margin=""50"">
                            <Button Content=""Hello World!"" Margin=""0,30,0,0""/>
                            <Rectangle Height=""5"" Margin=""-10,20,-10,0"">
                                <Rectangle.Fill>
                                    <RadialGradientBrush>
                                        <GradientStop Offset=""0"" Color=""#40000000""/>
                                        <GradientStop Offset=""1"" Color=""#00000000""/>
                                    </RadialGradientBrush>
                                </Rectangle.Fill>
                            </Rectangle>
                        </StackPanel>
                    </Viewbox>
                </Grid>");

        _view = Noesis.GUI.CreateView(xaml);
        _view.SetFlags(Noesis.RenderFlags.PPAA | Noesis.RenderFlags.LCD);
        
        // Renderer initialization with an OpenGL device
        _view.Renderer.Init(new Noesis.RenderDeviceGL());
    }

    public void Render(double DeltaTime)
    {
        gl.PushAttribute();
        _view.Update(DeltaTime);
        _view.Renderer.UpdateRenderTree();
        _view.Renderer.RenderOffscreen();
        _view.Renderer.Render();
        gl.PopAttribute();
    }

    public void Fini()
    {

    }

    public void OnResize(int width, int height)
    {

        _view.SetSize(width, height);
    }

    public void OnMouseMove(int x, int y)
    {
        _view.MouseMove(x, y);
    }

    public void OnMouseButtonDown(MouseButton button,int x, int y)
    {
        if (button == MouseButton.Left)
            _view.MouseButtonDown(x, y, Noesis.MouseButton.Left);
    }
    public void OnMouseButtonUp(MouseButton button, int x, int y)
    {
        if (button == MouseButton.Left)
            _view.MouseButtonUp(x, y, Noesis.MouseButton.Left);
    }
    int Blend;
    int BlendDstAlpha;
    int BlendDstRGB;
    int BlendEquationAlpha;
    int BlendEquationRgb;
    int BlendSrcAlpha;
    int BlendSrcRGB;

    int DepthTest;
    int DepthFunc;
    int DepthWriteMask;
    int CullFace;
    public void StateSave()
    {
        Blend = gl.GetInteger(GLEnum.Blend);
        BlendDstAlpha = gl.GetInteger(GLEnum.BlendDstAlpha);
        BlendDstRGB = gl.GetInteger(GLEnum.BlendDstRgb);
        BlendEquationAlpha = gl.GetInteger(GLEnum.BlendEquationAlpha);
        BlendEquationRgb = gl.GetInteger(GLEnum.BlendEquationRgb);
        BlendSrcAlpha = gl.GetInteger(GLEnum.BlendSrcAlpha);  
        BlendSrcRGB = gl.GetInteger(GLEnum.BlendSrcRgb);

        DepthTest = gl.GetInteger(GLEnum.DepthTest);
        DepthFunc = gl.GetInteger(GLEnum.DepthFunc);
        DepthWriteMask = gl.GetInteger(GLEnum.DepthWritemask);
        CullFace = gl.GetInteger(GLEnum.CullFace);
    }


    public void StateRestore()
    {
        gl.BlendEquationSeparate((GLEnum)BlendEquationRgb, (GLEnum)BlendEquationAlpha);
        gl.BlendFuncSeparate((GLEnum)BlendSrcRGB, (GLEnum)BlendDstRGB, (GLEnum)BlendSrcAlpha, (GLEnum)BlendDstAlpha);
        gl.Enable(GLEnum.Blend, (uint)Blend);
        gl.Enable(GLEnum.DepthTest, (uint)DepthTest);
        gl.DepthFunc((GLEnum)DepthFunc);
        gl.DepthMask(DepthWriteMask == 1);
        gl.Enable(GLEnum.CullFace, (uint)CullFace);
    }

}
