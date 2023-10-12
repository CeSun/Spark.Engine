#version 300 es

precision highp float;
out vec4 FragColor;

in vec3 TexCoords;

uniform samplerCube skybox;
uniform sampler2D DepthTexture;
uniform vec2 BufferSize;
uniform vec2 ScreenSize;

float GetDepth(ivec2 ScreenLocation)
{
    vec2 OutTexCoord = vec2(ScreenLocation) / BufferSize;
    vec2 OutTrueTexCoord = vec2(ScreenLocation) / ScreenSize;
    
   return texture(DepthTexture, OutTexCoord).x;
}


void main()
{   
    if (GetDepth(ivec2(gl_FragCoord.xy)) < 1.0f)
        discard;
    FragColor = texture(skybox, TexCoords);
}