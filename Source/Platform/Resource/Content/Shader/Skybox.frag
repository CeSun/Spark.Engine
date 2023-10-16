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
    // todo 
    if (GetDepth(vec2(gl_FragCoord.xy + vec2(1.0f, 1.0f))) < 1.0f && GetDepth(vec2(gl_FragCoord.xy - vec2(1.0f, 1.0f))) < 1.0f)
        discard;
    FragColor = texture(skybox, TexCoords);
}