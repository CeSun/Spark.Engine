#version 330 core
layout (location = 0) in vec3 Location;
layout (location = 1) in vec2 TexCoord;

uniform vec2 TexCoordScale;
uniform mat4 ModelTransform;
out vec2 OutTexCoord;
out vec2 OutTrueTexCoord;
out mat4 ModelInvertTransform;

void main()
{
    ModelInvertTransform = inverse(ModelTransform);
    OutTexCoord = TexCoord * TexCoordScale;
    OutTrueTexCoord = TexCoord;
    gl_Position = vec4(Location, 1.0);
    
}