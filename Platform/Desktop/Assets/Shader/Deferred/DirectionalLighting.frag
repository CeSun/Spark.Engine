#version 330 core
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D ColorTexture;
uniform sampler2D NormalTexture;
uniform sampler2D DepthTexture;
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

    
    float depth = texture(DepthTexture, OutTexCoord).w;
    // vec3 ScreenLocation = vec3(gl_FragCoord.x / 800, gl_FragCoord.y / 600, depth);
    vec3 WorldLocation = GetWorldLocation(vec3(OutTrueTexCoord, depth));
    vec4 Color = vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
    vec3 Normal = (texture(NormalTexture, OutTexCoord).rgb * 2) - 1;

    Normal = normalize(Normal);

    
    vec3  Ambient = AmbientStrength * LightColor.rgb;


    // mfs
    float diff = max(dot(Normal, -1 * LightDirection), 0.0);
    vec3 Diffuse = diff * LightColor;

    // jmfs 
    vec3 CameraDirection = normalize(CameraLocation - WorldLocation);
    vec3 ReflectDirection = reflect(LightDirection, Normal);
    float spec = pow(max(dot(CameraDirection, ReflectDirection), 0.0), 32);

    vec3 Specular = specularStrength * spec * LightColor;

    glColor = vec4((Ambient + Diffuse + Specular ) * LightStrength * Color.rgb, 1); 

}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2 - vec3(1.0f, 1.0f, 1.0f);
    ScreenLocation.z = ScreenLocation.z * -1;
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;

    return WorldLocation;
}
