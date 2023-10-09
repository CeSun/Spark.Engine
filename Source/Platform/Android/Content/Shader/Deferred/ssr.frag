#version 300 es

precision highp float;
out vec3 glColor;

in vec2 OutTexCoord;
uniform vec2 TexCoordScale;
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
    vec3 WorldLocation = GetWorldLocation(vec3(OutTexCoord / TexCoordScale, depth));
    vec4 Color = vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
    vec3 Normal = (texture(NormalTexture, OutTexCoord).rgb * 2.0f) - 1.0f;
    float IsReflection = texture(ReflectionTexture, OutTexCoord).r;

    if (IsReflection < 1.0f)
    {
        glColor = texture(ColorTexture, OutTexCoord).xyz;
        return;
    }
    vec3 CameraDirection = normalize(WorldLocation - CameraLocation);

    vec3 Direction= normalize(reflect(CameraDirection, Normal));
    vec3 SpaceDirection = Direction;
    vec3 SkyboxColor = texture(SkyboxTexture, SpaceDirection).rgb;
    
    glColor = SkyboxColor;
    float RayLength = 20.0f;
    float MaxLoopTimes = 10.0f;
    float MidLoopTimes = 10.0f;
    float MinLoopTimes = 10.0f;
    float MaxLoopStep = RayLength / MaxLoopTimes;
    float MidLoopStep = MaxLoopStep / MidLoopTimes;
    float MinLoopStep = MidLoopStep / MinLoopTimes;

    for (float i = 1.0f; i <= MaxLoopTimes; i ++)
    {
        vec3 NewLocation = WorldLocation + (Direction * MaxLoopStep * i);
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

        vec3 NewUvd = (ScreenLocation.xyz + 1.0 ) / 2.0f;

        float TargetDepth = MyTexture(DepthTexture, NewUvd.xy).r;
		
		if (TargetDepth == 0.0f)
			break;
        if (TargetDepth <= NewUvd.z)
        {
			
            for (float j = 1.0f; j <= MidLoopTimes; j ++)
            {
                NewLocation = WorldLocation + (Direction * ((MaxLoopStep * (i - 1.0f)) +  j * MidLoopStep ));
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

                NewUvd = (ScreenLocation.xyz + 1.0 ) / 2.0f;

                TargetDepth = MyTexture(DepthTexture, NewUvd.xy).r;
		
		        if (TargetDepth == 0.0f)
			        break;
                if (TargetDepth <= NewUvd.z)
                {
                    
                    for (float k = 1.0f; k <= MinLoopTimes; k ++)
                    {
                        NewLocation = WorldLocation + (Direction * ((MaxLoopStep * (i - 1.0f)) +  (j -1.0f) * MidLoopStep + k * MinLoopStep));
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

                        NewUvd = (ScreenLocation.xyz + 1.0 ) / 2.0f;

                        TargetDepth = MyTexture(DepthTexture, NewUvd.xy).r;
                        float TargetBackDepth =  MyTexture(BackDepthTexture, NewUvd.xy).r;
		
		                if (TargetDepth == 0.0f)
			                break;
                        if (TargetDepth <= NewUvd.z && TargetBackDepth >= NewUvd.z)
                        {
                        	glColor = MyTexture(ColorTexture, NewUvd.xy).xyz;
                            return;
                        }
                    }

                }
            }
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
