#version 300 es

precision highp float;
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D GBuffer;
uniform sampler2D DepthTexture;
uniform sampler2D ShadowMapTexture;
uniform sampler2D SSAOTexture;

uniform mat4 WorldToLight;
uniform mat4 VPInvert;
uniform vec3 LightDirection;
uniform vec3 LightColor;
uniform vec3 CameraLocation;
uniform float AmbientStrength;
uniform float LightStrength;



vec3 GetWorldLocation(vec3 ScreenLocation);

vec3 GetColor(ivec2 ScreenLocation)
{
    ivec2 screenSize = ivec2(vec2(ScreenLocation) / OutTrueTexCoord);
    vec2 scale = OutTexCoord / OutTrueTexCoord;
    float grayscale = texture(GBuffer, OutTexCoord).x;

    vec2 pixelOffset = scale / vec2(screenSize);

    
    vec2 RTexcoord = OutTexCoord;
    vec2 BTexcoord = OutTexCoord;

    int xparity = (ScreenLocation.x % 2);
    int yparity = (ScreenLocation.y % 2);
    if (xparity + yparity == 0)
    {
        RTexcoord = OutTexCoord;
        BTexcoord = OutTexCoord + pixelOffset;
    }
    else if (xparity + yparity == 2)
    {
        RTexcoord = OutTexCoord - pixelOffset;
        BTexcoord = OutTexCoord;
    }
    else if (xparity == 1 && yparity == 0)
    {
        RTexcoord = vec2(OutTexCoord.x - pixelOffset.x, OutTexCoord.y);
        BTexcoord = vec2(OutTexCoord.x, OutTexCoord.y + pixelOffset.y);
    }
    else if (xparity == 0 && yparity == 1)
    {
        RTexcoord = vec2(OutTexCoord.x, OutTexCoord.y - pixelOffset.y);
        BTexcoord = vec2(OutTexCoord.x + pixelOffset.x, OutTexCoord.y);
    }
    
    float r = texture(GBuffer, RTexcoord).y;
    float b = texture(GBuffer, BTexcoord).y;
    float g = (grayscale - (r * 0.3f + b * 0.11f)) / 0.59f;
    return vec3(r, g, b);
}

vec3 GetNormal(ivec2 ScreenLocation)
{
    ivec2 screenSize = ivec2(vec2(ScreenLocation) / OutTrueTexCoord);
    vec2 scale = OutTexCoord / OutTrueTexCoord;
    float grayscale = texture(GBuffer, OutTexCoord).x;

    vec2 pixelOffset = scale / vec2(screenSize);

    
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
    float specularStrength = 0.5f;

    
    float depth = texture(DepthTexture, OutTexCoord).r;
    vec3 WorldLocation = GetWorldLocation(vec3(OutTrueTexCoord, depth));
    float AO = texture(SSAOTexture, OutTexCoord).r;
    //vec3 WorldLocation =texture(DepthTexture, OutTexCoord).xyz;
    vec4 Color = vec4(GetColor(ivec2(gl_FragCoord.xy)), 1.0f);//vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
    vec3 Normal = (GetNormal(ivec2(gl_FragCoord.xy)) * 2.0f) - 1.0f;

    Normal = normalize(Normal);
    
    vec4 tmpLightSpaceLocation = WorldToLight * vec4(WorldLocation, 1.0);
    vec3 LightSpaceLocation = (tmpLightSpaceLocation / tmpLightSpaceLocation.w).xyz;
    LightSpaceLocation = (LightSpaceLocation + 1.0) / 2.0;
    
	if (LightSpaceLocation.z > 1.0f)
		LightSpaceLocation.z = 1.0f;

    float Shadow = 0.0;
    vec2 texelSize = 1.0f / vec2(textureSize(ShadowMapTexture, 0));
    for(int x = -1; x <= 1; ++x)
    {
        for(int y = -1; y <= 1; ++y)
        {
            float ShadowDepth = texture(ShadowMapTexture, LightSpaceLocation.xy + vec2(x, y) * texelSize).r; 
            Shadow += LightSpaceLocation.z > ShadowDepth ? 1.0 : 0.0 ;      
        }    
    }
    Shadow /= 9.0;


    



    vec3  Ambient = AmbientStrength * AO * LightColor.rgb;


    // mfs
    float diff = max(dot(Normal, -1.0f * LightDirection), 0.0);
    vec3 Diffuse = diff * LightColor;

    // jmfs 
    vec3 CameraDirection = normalize(CameraLocation - WorldLocation);
    vec3 HalfVector = normalize((-LightDirection + CameraDirection));
    // vec3 ReflectDirection = reflect(LightDirection, Normal);
    float spec = pow(max(dot(Normal, HalfVector), 0.0), 16.0f);

    vec3 Specular = specularStrength * spec * LightColor;

    glColor = vec4((Ambient + (Diffuse + Specular) * (1.0 - Shadow) ) * LightStrength * Color.rgb, 1.0f); 

}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;

    return WorldLocation;
}
