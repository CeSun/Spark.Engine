#version 300 es

precision highp float;
layout (location = 0) in vec3 Location;
layout (location = 5) in vec2 TexCoord;

uniform mat4 ModelTransform;
uniform mat4 ViewTransform;
uniform mat4 ProjectionTransform;
out vec2 OutTexCoord;


void main()
{
    OutTexCoord = TexCoord;
    gl_Position = ProjectionTransform * ViewTransform * ModelTransform * vec4(Location, 1.0);
    
}