#version 300 es

precision highp float;
layout (location = 0) in vec3 Position;

out vec3 TexCoords;

uniform mat4 Projection;
uniform mat4 View;

void main()
{
    TexCoords = Position;
    vec4 pos = Projection * View * vec4(Position, 1.0);
    gl_Position = pos.xyww;
}