#version 320 es
precision mediump float;
layout (location = 0) out vec3 BufferNormal;
layout (location = 1) out vec3 BufferColor;
layout (location = 2) out vec4 BufferDepth;

in vec2 OutTexCoord;
in vec3 OutColor;
in vec3 OutNormal;
in vec3 OutPosition;

uniform sampler2D Diffuse;
uniform sampler2D Normal;

void main()
{
    BufferColor = texture(Diffuse, OutTexCoord).rgb;
    BufferNormal =  (OutNormal + 1.0f) / 2.0f;
    BufferDepth = vec4(BufferColor, gl_FragCoord.z);
}