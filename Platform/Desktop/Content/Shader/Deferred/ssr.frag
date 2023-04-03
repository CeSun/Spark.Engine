#version 330 core
out vec3 glColor;

in vec2 OutTexCoord;
uniform vec2 TexCoordScale;
uniform mat4 VPInvert;
uniform mat4 VP;

uniform sampler2D ColorTexture;
uniform sampler2D NormalTexture;
uniform sampler2D ReflectionTexture;
uniform sampler2D DepthTexture;


vec4 MyTexture(sampler2D Texture, vec2 Coord);
vec3 GetWorldLocation(vec3 ScreenLocation);

void main()
{
    float depth = texture(DepthTexture, OutTexCoord).r;
    vec3 WorldLocation = GetWorldLocation(vec3(OutTexCoord / TexCoordScale, depth));
    vec4 Color = vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
    vec3 Normal = (texture(NormalTexture, OutTexCoord).rgb * 2.0f) - 1.0f;
    float IsReflection = texture(ReflectionTexture, OutTexCoord).r;

    if (IsReflection < 1)
    {
        glColor = MyTexture(ColorTexture, OutTexCoord).xyz;
        return;
    }

    float Step = 0.1;
    for (int i = 0; i < 10; i ++)
    {
        vec3 NewLocation = WorldLocation + Normal * 0.1;
        vec4 ScreenLocation = vec4(NewLocation, 1.0) * VP;
        if (ScreenLocation.x > ScreenLocation.w || ScreenLocation.y > ScreenLocation.w || ScreenLocation.z > ScreenLocation.w)
        {
        
            break;
        }
        if (ScreenLocation.x < -ScreenLocation.w || ScreenLocation.y < -ScreenLocation.w || ScreenLocation.z < -ScreenLocation.w)
        {
            break;
        }
        ScreenLocation = ScreenLocation / ScreenLocation.w;

        vec3 NewUvd = (ScreenLocation.xyz + 1.0 ) / 2;
        vec4 TargetDepth = MyTexture(DepthTexture, NewUvd.xy);

        if (TargetDepth.z < NewUvd.z)
        {
            glColor = MyTexture(ColorTexture, NewUvd.xy).xyz;
            break;
        }
    }
}

vec4 MyTexture(sampler2D Texture, vec2 Coord)
{
	return texture(Texture, Coord * TexCoordScale);
}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;

    return WorldLocation;
}
