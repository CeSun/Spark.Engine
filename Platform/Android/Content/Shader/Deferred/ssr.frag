#version 300 es
out vec3 glColor;

in vec2 OutTexCoord;
in vec2 TexCoordScale2;
uniform mat4 VPInvert;
uniform mat4 Projection;
uniform mat4 View;
uniform vec3 CameraLocation;
uniform sampler2D ColorTexture;
uniform sampler2D NormalTexture;
uniform sampler2D ReflectionTexture;
uniform sampler2D DepthTexture;
uniform samplerCube SkyboxTexture;
uniform sampler2D BackDepthTexture;

vec4 MyTexture(sampler2D Texture, vec2 Coord);
vec3 GetWorldLocation(vec3 ScreenLocation);

void main()
{
    float depth = texture(DepthTexture, OutTexCoord).r;
    vec3 WorldLocation = GetWorldLocation(vec3(OutTexCoord / TexCoordScale2, depth));
    vec4 Color = vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
    vec3 Normal = (texture(NormalTexture, OutTexCoord).rgb * 2.0f) - 1.0f;
    float IsReflection = texture(ReflectionTexture, OutTexCoord).r;

    if (IsReflection < 1.0)
    {
        glColor = texture(ColorTexture, OutTexCoord).xyz;
        return;
    }
    vec3 CameraDirection = normalize(WorldLocation - CameraLocation);

    vec3 Direction= normalize(reflect(CameraDirection, Normal));
    vec3 SpaceDirection = Direction;
    vec3 SkyboxColor = texture(SkyboxTexture, SpaceDirection).rgb;
    
    float MaxStep = 3.0;
    float MinStep = 0.05;
    
    glColor = SkyboxColor;
    for (int i = 1; i < 50; i ++)
    {
        vec3 NewLocation = WorldLocation + (Direction * MaxStep * float(i));
        vec4 ScreenLocation = Projection * View * vec4(NewLocation, 1.0) ;
        if (ScreenLocation.x >= ScreenLocation.w || ScreenLocation.y >= ScreenLocation.w || ScreenLocation.z >= ScreenLocation.w)
        {
            break;
        }
        if (ScreenLocation.x <= -ScreenLocation.w || ScreenLocation.y <= -ScreenLocation.w || ScreenLocation.z <= -ScreenLocation.w)
        {
            break;
        }
        ScreenLocation = ScreenLocation / ScreenLocation.w;

        vec3 NewUvd = (ScreenLocation.xyz + 1.0 ) / 2.0;

        float TargetDepth = MyTexture(DepthTexture, NewUvd.xy).r;
        float TargetBackDepth =  MyTexture(BackDepthTexture, NewUvd.xy).r;
		
		if (TargetDepth == 0.0)
			break;
        if (TargetDepth <= NewUvd.z)
        {
            for(int j = 1; j <= 60; j ++)
            {
                NewLocation = WorldLocation + (Direction * (MaxStep * (float(i) - 1.0) + MinStep * float(j)));
                ScreenLocation = Projection * View * vec4(NewLocation, 1.0) ;
                if (ScreenLocation.x >= ScreenLocation.w || ScreenLocation.y >= ScreenLocation.w || ScreenLocation.z >= ScreenLocation.w)
                {
                    break;
                }
                if (ScreenLocation.x <= -ScreenLocation.w || ScreenLocation.y <= -ScreenLocation.w || ScreenLocation.z <= -ScreenLocation.w)
                {
                    break;
                }
                ScreenLocation = ScreenLocation / ScreenLocation.w;

                NewUvd = (ScreenLocation.xyz + 1.0 ) / 2.0;


                TargetDepth = MyTexture(DepthTexture, NewUvd.xy).r;
                TargetBackDepth =  MyTexture(BackDepthTexture, NewUvd.xy).r;
		
		        if (TargetDepth == 0.0)
			        break;
                if (TargetDepth <= NewUvd.z && TargetBackDepth >= NewUvd.z)
                {
			        glColor = MyTexture(ColorTexture, NewUvd.xy).xyz;
                    return;
                }
            }
            return;
        }
    }
}



vec4 MyTexture(sampler2D Texture, vec2 Coord)
{
	return texture(Texture, Coord * TexCoordScale2);
}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;

    return WorldLocation;
}
