#version 330 core
out vec4 FragColor;

in vec2 OutTexCoord;
in vec3 OutColor;

uniform sampler2D Diffuse;
uniform sampler2D Normal;

void main()
{
    FragColor = vec4(texture(Diffuse, OutTexCoord).rgb, 1.0f);
}