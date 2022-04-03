{GLVERSION}
layout (location = 0) in vec3 Location;
layout (location = 1) in vec3 Normal;
layout (location = 2) in vec3 Color;
layout (location = 3) in vec2 TexCoord;

out vec3 fsColor;
out vec2 fsTexCoord;

uniform mat4 Model;

layout (std140) uniform Matrices
{
    mat4 Projection;
    mat4 View;
};

void main()
{
    gl_Position =  Projection * View * Model *  vec4(Location, 1.0) ;
    fsColor = Color;
    fsTexCoord = TexCoord;
}