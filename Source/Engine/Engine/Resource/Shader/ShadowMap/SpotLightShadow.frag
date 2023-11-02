#version 300 es

precision highp float;

uniform sampler2D Diffuse;
in vec2 OutTexCoord;

void main()
{             
    
    vec4 color = texture(Diffuse, OutTexCoord).rgba;
    if (color.a < 0.1f)
        discard;
}