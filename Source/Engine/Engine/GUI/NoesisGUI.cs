using Noesis;
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
        //Noesis.GUI.SetLicense("Ce Sun", "JpZpg03aaYMzQP6TCJturoZUtpPys56lIS3O3DKAJlhYkJ/l");
        Noesis.GUI.Init();
        MainKeyBoard.KeyChar += (kb, word) =>
        {
            OnKeyChar(word);
        };
        MainKeyBoard.KeyDown +=  (kb, key, num) =>
        {
            OnKeyDown(key);
        };

        MainKeyBoard.KeyUp += (kb, key, num) =>
        {
            OnKeyUp(key);
        };
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
                            <TextBox></TextBox>
                            <Button Content=""Hello World!"" Margin=""0,30,0,0""/>
                         
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

    public void OnMouseButtonDown(Silk.NET.Input.MouseButton button,int x, int y)
    {
        if (button == Silk.NET.Input.MouseButton.Left)
            _view.MouseButtonDown(x, y, Noesis.MouseButton.Left);
    }
    public void OnMouseButtonUp(Silk.NET.Input.MouseButton button, int x, int y)
    {
        if (button == Silk.NET.Input.MouseButton.Left)
            _view.MouseButtonUp(x, y, Noesis.MouseButton.Left);
    }

    void OnKeyDown(Silk.NET.Input.Key key)
    {
        _view.KeyDown(GetUIKey(key));
    }
    void OnKeyUp(Silk.NET.Input.Key key)
    {
        _view.KeyUp(GetUIKey(key));
    }
    Noesis.Key GetUIKey(Silk.NET.Input.Key key)
    {
        return key switch
        {
            Silk.NET.Input.Key.Space => Noesis.Key.Space,
            Silk.NET.Input.Key.Comma => Noesis.Key.OemComma,
            Silk.NET.Input.Key.Backspace => Noesis.Key.Back,
            Silk.NET.Input.Key.Up => Noesis.Key.Up,
            Silk.NET.Input.Key.Left => Noesis.Key.Left,
            Silk.NET.Input.Key.Right => Noesis.Key.Right,
            Silk.NET.Input.Key.Down => Noesis.Key.Down,
            Silk.NET.Input.Key.Enter => Noesis.Key.Enter,
            Silk.NET.Input.Key.Escape => Noesis.Key.Escape,
            Silk.NET.Input.Key.ControlLeft => Noesis.Key.LeftCtrl,
            Silk.NET.Input.Key.ControlRight => Noesis.Key.RightCtrl,
            Silk.NET.Input.Key.AltLeft => Noesis.Key.LeftAlt,
            Silk.NET.Input.Key.AltRight => Noesis.Key.RightAlt,
            Silk.NET.Input.Key.ShiftLeft => Noesis.Key.LeftShift,
            Silk.NET.Input.Key.ShiftRight => Noesis.Key.RightShift,
            Silk.NET.Input.Key.Q => Noesis.Key.Q,
            Silk.NET.Input.Key.W => Noesis.Key.W,
            Silk.NET.Input.Key.E => Noesis.Key.E,


            Silk.NET.Input.Key.R => Noesis.Key.R,
            Silk.NET.Input.Key.T => Noesis.Key.T,
            Silk.NET.Input.Key.Y => Noesis.Key.Y,


            Silk.NET.Input.Key.U => Noesis.Key.U,
            Silk.NET.Input.Key.I => Noesis.Key.I,
            Silk.NET.Input.Key.O => Noesis.Key.O,
            Silk.NET.Input.Key.P => Noesis.Key.P,



            Silk.NET.Input.Key.A => Noesis.Key.A,
            Silk.NET.Input.Key.S => Noesis.Key.S,
            Silk.NET.Input.Key.D => Noesis.Key.D,


            Silk.NET.Input.Key.F => Noesis.Key.F,
            Silk.NET.Input.Key.G => Noesis.Key.G,
            Silk.NET.Input.Key.H => Noesis.Key.H,


            Silk.NET.Input.Key.J => Noesis.Key.J,
            Silk.NET.Input.Key.K => Noesis.Key.K,
            Silk.NET.Input.Key.L => Noesis.Key.L,




            _ => Noesis.Key.None
        };

    }
    public void OnKeyChar(char word)
    {
        _view.Char(word);
    }


}
