#version 300 es
layout (location = 0) out vec3 BufferDepth;

void main()
{
    BufferDepth = vec3(gl_FragCoord.z, 0, 0);
}

