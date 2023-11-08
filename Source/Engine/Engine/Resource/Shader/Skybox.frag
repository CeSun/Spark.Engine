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

    vec3 Color = texture(skybox, TexCoords).rgb;
    // HDR tonemap and gamma correct
    Color = Color / (Color + vec3(1.0));
    Color = pow(Color, vec3(1.0/2.2)); 

    FragColor = vec4(Color, 1.0f);
}