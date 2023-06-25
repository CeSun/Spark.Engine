#version 330 core
out vec4 glColor;

in vec2 OutTexCoord;
uniform sampler2D ColorTexture;
uniform float Brightness;

void main()
{
    glColor = vec4(texture(ColorTexture, OutTexCoord).rgb * Brightness, 1.0f);
}