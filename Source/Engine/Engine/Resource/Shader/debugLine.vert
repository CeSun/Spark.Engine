#version 300 es

precision highp float;
layout (location = 0) in vec3 Location;

uniform mat4 ViewTransform;
uniform mat4 ProjectionTransform;

void main()
{
    gl_Position = ProjectionTransform * ViewTransform  * vec4(Location, 1.0);
}