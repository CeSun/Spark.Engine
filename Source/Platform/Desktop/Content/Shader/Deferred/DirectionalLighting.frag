#version 330 core
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D ColorTexture;
uniform sampler2D NormalTexture;
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



void main()
{
    float specularStrength = 0.5f;

    
    float depth = texture(DepthTexture, OutTexCoord).r;
    vec3 WorldLocation = GetWorldLocation(vec3(OutTrueTexCoord, depth));
    float AO = texture(SSAOTexture, OutTexCoord).r;
    //vec3 WorldLocation =texture(DepthTexture, OutTexCoord).xyz;
    vec4 Color = vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
    vec3 Normal = (texture(NormalTexture, OutTexCoord).rgb * 2.0f) - 1.0f;

    Normal = normalize(Normal);
    
    vec4 tmpLightSpaceLocation = WorldToLight * vec4(WorldLocation, 1.0);
    vec3 LightSpaceLocation = (tmpLightSpaceLocation / tmpLightSpaceLocation.w).xyz;
    LightSpaceLocation = (LightSpaceLocation + 1.0) / 2.0;
    
	if (LightSpaceLocation.z > 1.0f)
		LightSpaceLocation.z = 1.0f;

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
