#version 320 es

precision highp float;
layout (location = 0) in vec3 position;

uniform mat4 ModelTransform;

void main()
{
    gl_Position = ModelTransform * vec4(position, 1.0);
}