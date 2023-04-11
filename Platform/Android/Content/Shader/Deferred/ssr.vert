#version 300 es
layout (location = 0) in vec3 Location;
layout (location = 1) in vec2 TexCoord;

uniform vec2 TexCoordScale;
out vec2 OutTexCoord;
out vec2 TexCoordScale2;

void main()
{
    
    OutTexCoord = TexCoord * TexCoordScale;
    TexCoordScale2 = TexCoordScale;
    gl_Position = vec4(Location, 1.0);
    
}