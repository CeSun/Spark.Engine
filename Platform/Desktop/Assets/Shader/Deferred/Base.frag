#version 330 core
layout (location = 0) out vec3 BufferNormal;
layout (location = 1) out vec4 BufferColor;

in vec2 OutTexCoord;
in vec3 OutColor;
in vec3 OutNormal;
in vec3 OutPosition;

uniform sampler2D Diffuse;
uniform sampler2D Normal;

void main()
{
    BufferColor = vec4(texture(Diffuse, OutTexCoord).rgb, 1.0f);
    BufferNormal =  (OutNormal + 1) / 2;
}