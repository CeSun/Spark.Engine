#version 300 es

precision highp float;
layout (location = 0) out vec3 BufferDepth;

void main()
{
    BufferDepth = vec3(gl_FragCoord.z, 0, 0);
}

