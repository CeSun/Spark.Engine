#version 300 es
layout (location = 0) in vec3 Location;
layout (location = 1) in vec3 Normal;
layout (location = 2) in vec3 Tangent;
layout (location = 3) in vec3 BitTangent;
layout (location = 4) in vec3 Color;
layout (location = 5) in vec2 TexCoord;

uniform mat4 ModelTransform;
uniform mat4 ViewTransform;
uniform mat4 ProjectionTransform;


void main()
{
    gl_Position = ProjectionTransform * ViewTransform * ModelTransform * vec4(Location, 1.0);
}