using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Core.Assets;

public class Shader : Asset
{
    public uint ProgramId;
    public Shader(string vspath, string fspath)
    {

    }

    protected override async Task AsyncLoad()
    {

    }

    public void SetInt(string name, int value) 
    {
        gl.UseProgram(ProgramId);
        var location = gl.GetUniformLocation(ProgramId, name);
        gl.Uniform1(location, value);
        gl.UseProgram(0);
    }

    public static Shader? GlobalShader;

}
