#version 300 es

precision highp float;
out vec4 glColor;

in vec2 OutTexCoord;
uniform sampler2D ColorTexture;

void main()
{
    glColor = texture(ColorTexture, OutTexCoord);
}
