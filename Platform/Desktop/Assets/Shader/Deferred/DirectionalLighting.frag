#version 330 core
out vec4 glColor;

in vec2 OutTexCoord;
uniform sampler2D ColorTexture;
uniform sampler2D NormalTexture;
uniform sampler2D DepthTexture;
uniform mat4 VPInvert;



vec3 GetWorldLocation(vec3 ScreenLocation);


void main()
{
    float depth = texture(DepthTexture, OutTexCoord).r;
    vec3 ScreenLocation = vec3(gl_FragCoord.xy, depth);
    vec3 WorldLocation = GetWorldLocation(ScreenLocation);
    vec4 Color = vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
    vec3 Normal = (texture(NormalTexture, OutTexCoord).rgb * 2) - 1;
    glColor = vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2 - vec3(1.0f, 1.0f, 1.0f);
    ScreenLocation.z = ScreenLocation.z * -1;
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;

    return WorldLocation;
}
