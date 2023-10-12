#version 300 es

precision highp float;
out vec4 FragColor;

in vec3 TexCoords;

uniform samplerCube skybox;
uniform sampler2D GBuffer;
uniform vec2 BufferSize;
uniform vec2 ScreenSize;

vec3 GetNormal(ivec2 ScreenLocation)
{
    vec2 OutTexCoord = vec2(ScreenLocation) / BufferSize;
    vec2 OutTrueTexCoord = vec2(ScreenLocation) / ScreenSize;
    vec2 scale = OutTexCoord / OutTrueTexCoord;

    vec2 pixelOffset = scale / vec2(ScreenSize);

    
    vec2 XTexcoord = OutTexCoord;
    vec2 YTexcoord = OutTexCoord;
    vec2 ZTexcoord = OutTexCoord;

    int xparity = (ScreenLocation.x % 2);
    int yparity = (ScreenLocation.y % 2);
    if (xparity + yparity == 0)
    {
        XTexcoord = OutTexCoord;
        YTexcoord = OutTexCoord + pixelOffset;
        ZTexcoord = vec2(OutTexCoord.x + pixelOffset.x, OutTexCoord.y);
    }
    else if (xparity + yparity == 2)
    {
        XTexcoord = OutTexCoord - pixelOffset;
        YTexcoord = OutTexCoord;
        ZTexcoord = vec2(OutTexCoord.x, OutTexCoord.y - pixelOffset.y);

    }
    else if (xparity == 1 && yparity == 0)
    {
        XTexcoord = vec2(OutTexCoord.x - pixelOffset.x, OutTexCoord.y);
        YTexcoord = vec2(OutTexCoord.x, OutTexCoord.y + pixelOffset.y);
        ZTexcoord = OutTexCoord;
    }
    else if (xparity == 0 && yparity == 1)
    {
        XTexcoord = vec2(OutTexCoord.x, OutTexCoord.y - pixelOffset.y);
        YTexcoord = vec2(OutTexCoord.x + pixelOffset.x, OutTexCoord.y);
        ZTexcoord = vec2(OutTexCoord.x + pixelOffset.x, OutTexCoord.y - pixelOffset.y);
    }
    
    float x = texture(GBuffer, XTexcoord).z;
    float y = texture(GBuffer, YTexcoord).z;
    float z = texture(GBuffer, ZTexcoord).z;
    return vec3(x, y, z);
}


void main()
{   
    vec3 Normal = GetNormal(ivec2(gl_FragCoord.xy));
    vec2 ScreenCoord = ((gl_FragCoord.xy + 0.5) / ScreenSize);
    ScreenCoord = ScreenCoord * (ScreenSize / BufferSize);;
    if (length(Normal) > 0.0f)
        discard;
    FragColor = texture(skybox, TexCoords);
}