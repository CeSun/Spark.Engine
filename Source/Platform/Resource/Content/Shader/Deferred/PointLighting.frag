#version 300 es

precision highp float;
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;

uniform sampler2D GBuffer;
uniform sampler2D DepthTexture;
uniform samplerCube ShadowMapTextue;
uniform sampler2D SSAOTexture;
uniform mat4 VPInvert;
uniform vec3 LightColor;
uniform vec3 LightLocation;
uniform vec3 CameraLocation;
uniform float AmbientStrength;
uniform float Constant;
uniform float Linear;
uniform float Quadratic;
uniform float FarPlan;
uniform float LightStrength;




vec3 GetWorldLocation(vec3 ScreenLocation);
float ShadowCalculation(vec3 fragPos);

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
    float specularStrength = 0.5;
   
    float depth = texture(DepthTexture, OutTexCoord).r;
    vec3 WorldLocation = GetWorldLocation(vec3(OutTrueTexCoord, depth));
    //vec3 WorldLocation =texture(DepthTexture, OutTexCoord).xyz;
    vec4 Color = vec4(GetColor(ivec2(gl_FragCoord.xy)), 1.0f);//vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
    vec3 Normal = (GetNormal(ivec2(gl_FragCoord.xy)) * 2.0f) - 1.0f;
    float AO = texture(SSAOTexture, OutTexCoord).r;

    float Distance    = length(LightLocation - WorldLocation);
    float Attenuation = 1.0 / (Constant + Linear * Distance + Quadratic * (Distance * Distance));


    Normal = normalize(Normal);

    vec3  Ambient = AmbientStrength * AO * Attenuation * LightColor.rgb;
    
    vec3 LightDirection = normalize(WorldLocation - LightLocation);
    // mfs
    float diff = max(dot(Normal, -1.0f * LightDirection), 0.0);
    vec3 diffuse = diff * Attenuation * LightColor;
    // jmfs 
    vec3 CameraDirection = normalize(CameraLocation - WorldLocation);
    vec3 HalfVector = normalize((-LightDirection + CameraDirection));
    // vec3 ReflectDirection = reflect(LightDirection, Normal);
    float spec = pow(max(dot(Normal, HalfVector), 0.0), 16.0f);

    vec3 specular = specularStrength * Attenuation * spec * LightColor;
    
    float shadow = ShadowCalculation(WorldLocation);  
    glColor = vec4((Ambient + (1.0f - shadow) * (diffuse + specular)) * LightStrength * Color.rgb, 1.0f); 

}

float ShadowCalculation(vec3 WorldLocation)
{
    // get vector between fragment position and light position
    vec3 fragToLight = WorldLocation - LightLocation;
    // ise the fragment to light vector to sample from the depth map    
    float closestDepth = texture(ShadowMapTextue, fragToLight).r;
    // it is currently in linear range between [0,1], let's re-transform it back to original depth value
    closestDepth *= FarPlan;
    // now get current linear depth as the length between the fragment and light position
    float currentDepth = length(fragToLight);
    // test for shadows
    float shadow = currentDepth > closestDepth ? 1.0 : 0.0;       

    return shadow;
}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    // ScreenLocation.z = ScreenLocation.z * -1;
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;
    return WorldLocation;
}