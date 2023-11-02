#version 300 es

precision highp float;
out vec4 FragColor;

uniform vec4 Color;

void main()
{   
    FragColor = Color;
}