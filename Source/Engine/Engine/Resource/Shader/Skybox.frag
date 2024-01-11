#version 300 es

precision highp float;
out vec4 FragColor;

in vec3 TexCoords;

uniform samplerCube skybox;
uniform sampler2D DepthTexture;
uniform vec2 BufferSize;
uniform vec2 ScreenSize;

float GetDepth(vec2 ScreenLocation)
{
    vec2 OutTexCoord = vec2(ScreenLocation) / BufferSize;
    vec2 OutTrueTexCoord = vec2(ScreenLocation) / ScreenSize;
    return texture(DepthTexture, OutTexCoord).x;
}


void main()
{   
    vec3 v = TexCoords;
    v.y = v.y * -1.0f;
    vec3 Color = texture(skybox, v).rgb;
    FragColor = vec4(Color, 1.0f);
}