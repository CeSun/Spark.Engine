#version 330 core
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D ColorTexture;
uniform sampler2D NormalTexture;
uniform sampler2D DepthTexture;
uniform sampler2D ShadowMapTexture;
uniform mat4 VPInvert;
uniform vec3 LightColor;
uniform vec3 LightLocation;
uniform vec3 CameraLocation;
uniform float AmbientStrength;
uniform float Constant;
uniform float Linear;
uniform float Quadratic;
uniform float InnerCosine;
uniform float OuterCosine;
uniform vec3 ForwardVector;
uniform mat4 WorldToLight;
uniform float LightStrength;




vec3 GetWorldLocation(vec3 ScreenLocation);



void main()
{
    float specularStrength = 0.5;
   
    float depth = texture(DepthTexture, OutTexCoord).r;
    vec3 WorldLocation = GetWorldLocation(vec3(OutTrueTexCoord, depth));
    //vec3 WorldLocation =texture(DepthTexture, OutTexCoord).xyz;
    vec4 Color = vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
    vec3 Normal = (texture(NormalTexture, OutTexCoord).rgb * 2.0f) - 1.0f;
    Normal = normalize(Normal);
    vec3 LightDirection = normalize(WorldLocation - LightLocation);


    vec4 tmpLightSpaceLocation = WorldToLight * vec4(WorldLocation, 1.0);
    tmpLightSpaceLocation = tmpLightSpaceLocation / tmpLightSpaceLocation.w;
    vec3 LightSpaceLocation = (tmpLightSpaceLocation / tmpLightSpaceLocation.w).xyz;
    LightSpaceLocation = (LightSpaceLocation + 1.0) / 2.0;
    
    float Shadow = 0.0;
    vec2 texelSize = 1.0 / textureSize(ShadowMapTexture, 0);
    for(int x = -1; x <= 1; ++x)
    {
        for(int y = -1; y <= 1; ++y)
        {
            float ShadowDepth = texture(ShadowMapTexture, LightSpaceLocation.xy + vec2(x, y) * texelSize).r; 
            Shadow += LightSpaceLocation.z > ShadowDepth ? 1.0 : 0.0 ;      
        }    
    }
    Shadow /= 9.0;


    float Distance    = length(LightLocation - WorldLocation);
    float Attenuation = 1.0 / (Constant + Linear * Distance + Quadratic * (Distance * Distance));

    float Theta = dot(ForwardVector, LightDirection);
    float Epsilon = (InnerCosine - OuterCosine);
    
    float Intensity = clamp((Theta - OuterCosine) / Epsilon, 0.0, 1.0);

;

    vec3  Ambient = AmbientStrength * Attenuation * LightColor.rgb;
    
    // mfs
    float diff = max(dot(Normal, -1 * LightDirection), 0.0);
    vec3 diffuse = diff * Attenuation * Intensity * LightColor;
    // jmfs 
    vec3 CameraDirection = normalize(CameraLocation - WorldLocation);
    vec3 HalfVector = normalize((-LightDirection + CameraDirection));
    // vec3 ReflectDirection = reflect(LightDirection, Normal);
    float spec = pow(max(dot(Normal, HalfVector), 0.0), 16.0f);

    vec3 specular = specularStrength * Attenuation * Intensity * spec * LightColor;

    glColor = vec4((Ambient + (diffuse + specular)  * (1.0 - Shadow)) * LightStrength * Color.rgb, 1); 

}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    // ScreenLocation.z = ScreenLocation.z * -1;
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;
    return WorldLocation;
}