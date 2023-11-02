#version 300 es

precision highp float;
out vec4 glColor;

in vec2 OutTexCoord;
uniform sampler2D ColorTexture;

void main()
{
    glColor = vec4(pow(texture(ColorTexture, OutTexCoord).rgb, vec3(1.0/ 2.2)), 1.0f);
}
