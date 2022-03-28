using LiteEngine.Core.Render;
using Silk.NET.OpenGL;


namespace LiteEngine.Core.Components;

public class StaticMeshComponent : RenderableComponent
{
    bool IsLoaded = false;
    List<Vertex> vertices = new ();
    List<uint> indices = new();
    uint Vao;
    uint Vbo;
    uint Ebo;

    
    private static readonly string VertexShaderSource = @"
        #version 330 core //Using version GLSL version 3.3
        layout (location = 0) in vec4 vPos;
        
        void main()
        {
            gl_Position = vec4(vPos.x, vPos.y, vPos.z, 1.0);
        }
        ";

    //Fragment shaders are run on each fragment/pixel of the geometry.
    private static readonly string FragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;
        void main()
        {
            FragColor = vec4(1.0f, 0.5f, 0.2f, 1.0f);
        }
        ";
    uint Shader;

    private void ShaderGen()
    {
        var Gl = gl;
        uint vertexShader = Gl.CreateShader(ShaderType.VertexShader);
        Gl.ShaderSource(vertexShader, VertexShaderSource);
        Gl.CompileShader(vertexShader);

        //Checking the shader for compilation errors.
        string infoLog = Gl.GetShaderInfoLog(vertexShader);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            Console.WriteLine($"Error compiling vertex shader {infoLog}");
        }

        //Creating a fragment shader.
        uint fragmentShader = Gl.CreateShader(ShaderType.FragmentShader);
        Gl.ShaderSource(fragmentShader, FragmentShaderSource);
        Gl.CompileShader(fragmentShader);

        //Checking the shader for compilation errors.
        infoLog = Gl.GetShaderInfoLog(fragmentShader);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            Console.WriteLine($"Error compiling fragment shader {infoLog}");
        }

        //Combining the shaders under one shader program.
        Shader = Gl.CreateProgram();
        Gl.AttachShader(Shader, vertexShader);
        Gl.AttachShader(Shader, fragmentShader);
        Gl.LinkProgram(Shader);

        //Checking the linking for errors.
        Gl.GetProgram(Shader, GLEnum.LinkStatus, out var status);
        if (status == 0)
        {
            Console.WriteLine($"Error linking shader {Gl.GetProgramInfoLog(Shader)}");
        }

        //Delete the no longer useful individual shaders;
        Gl.DetachShader(Shader, vertexShader);
        Gl.DetachShader(Shader, fragmentShader);
        Gl.DeleteShader(vertexShader);
        Gl.DeleteShader(fragmentShader);
    }

    public StaticMeshComponent(Component parent, string name) : base(parent, name)
    {
        for(var i = 0; i < 4; i++)
        {
            var vertex = new Vertex();
            switch (i)
            {
                case 0:
                    vertex.Location = new (-0.5f,0.5f,0);
                    vertex.TexCoord = new(0, 1);
                    break;
                case 1:
                    vertex.Location = new(0.5f, 0.5f, 0);
                    vertex.TexCoord = new(1, 1);
                    break;
                case 2:
                    vertex.Location = new(0.5f, -0.5f, 0);
                    vertex.TexCoord = new(1, 0);
                    break;
                case 3:
                    vertex.Location = new(-0.5f, -0.5f, 0);
                    vertex.TexCoord = new(-1, -1);
                    break;
            }
            vertex.Normal = new(0, 0, 1);
            vertex.Color = new(1, 0, 0);
            vertices.Add(vertex);
        }
        indices.AddRange(new uint[]{0,3,2,2,1,0});
        (Vao, Vbo, Ebo) = GLUtil.GenBuffer(vertices, indices);
        ShaderGen();
        IsLoaded = true;
    }

    public override unsafe void Render()
    {
        base.Render();
        if (!IsLoaded)
            return;
        gl.UseProgram(Shader);
        gl.BindVertexArray(Vao);
        gl.DrawElements(PrimitiveType.Triangles, (uint)indices.Count, GLEnum.UnsignedInt, null);
        gl.BindVertexArray(0);
    }
}
