#version 300 es
layout (location = 0) in vec3 Location;

uniform mat4 ModelTransform;
uniform mat4 ViewTransform;
uniform mat4 ProjectionTransform;


void main()
{
    gl_Position = ProjectionTransform * ViewTransform * ModelTransform * vec4(Location, 1.0);
    
}