#version 300 es

precision highp float;
out vec4 glColor;

in vec2 OutTexCoord;
uniform sampler2D ColorTexture;

void main()
{
    vec3 color = texture(ColorTexture, OutTexCoord).rgb;
    color = color / (color + vec3(1.0));
    glColor = vec4(pow(color, vec3(1.0/ 2.2)), 1.0f);
}
