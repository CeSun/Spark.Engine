#version 300 es

precision highp float;
out vec4 glColor;

in vec2 OutTexCoord;
uniform sampler2D ColorTexture;

void main()
{
    glColor = vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
}
