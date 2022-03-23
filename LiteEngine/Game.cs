using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;


    
namespace LiteEngine;
public class Engine
{
    static Engine _Instance = new Engine();
    public static Engine Instance { get { return _Instance; } }

    public void Update(float deltaTime)
    {
    
    }
    public void Render()
    {
        
    }
    public void Init()
    {

    }

    public void Fini()
    {

    }
}