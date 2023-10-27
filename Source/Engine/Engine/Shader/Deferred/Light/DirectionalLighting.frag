#version 300 es

precision highp float;
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D ColorTexture;
uniform sampler2D CustomBuffer;
uniform sampler2D DepthTexture;
uniform sampler2D ShadowMapTexture;
#ifndef _MOBILE_
uniform sampler2D SSAOTexture;
#endif

uniform mat4 WorldToLight;
uniform mat4 VPInvert;
uniform vec3 LightDirection;
uniform vec3 LightColor;
uniform vec3 CameraLocation;
uniform float AmbientStrength;
uniform float LightStrength;



vec3 GetWorldLocation(vec3 ScreenLocation);
float[8] MicroGBufferDecoding(sampler2D MicroGBuffer, ivec2 ScreenLocation);

vec3 Normal2DTo3D(vec2 Normal)
{
    float z = (1.0f -  dot(vec2(1.0f, 1.0f),abs(Normal)));
    vec3 n = vec3(Normal.x, Normal.y, z);
    if (n.z < 0.0f)
    {
        vec2 tmp = vec2(1.0f, 1.0f);
        if (n.x < 0.0f || n.y < 0.0f)
        {
            tmp = vec2(-1.0f, -1.0f);
        }
        vec2 xy = (vec2(1.0f, 1.0f) - abs(vec2 (n.y, n.x))) * tmp;
        n.x = xy.x;
        n.y = xy.y;
    }
    return normalize(n);
}

void main()
{
    float specularStrength = 0.5f;

    
    float depth = texture(DepthTexture, OutTexCoord).r;
    vec3 WorldLocation = GetWorldLocation(vec3(OutTrueTexCoord, depth));
    //vec3 WorldLocation =texture(DepthTexture, OutTexCoord).xyz;
#ifndef _MICRO_GBUFFER_
    vec4 Buffer1 = texture(ColorTexture, OutTexCoord);
    vec3 Color = Buffer1.rgb;
    float AO = Buffer1.a;
    vec4 Buffer2 = texture(CustomBuffer, OutTexCoord);
    vec3 Normal = (Normal2DTo3D(Buffer2.xy));
#else
	float Buffer1[8] = MicroGBufferDecoding(ColorTexture,  ivec2(gl_FragCoord.xy));
    vec3 Color = vec3(Buffer1[0], Buffer1[1], Buffer1[2]);
    float AO = Buffer1[3]; 
    vec3 Normal = (Normal2DTo3D(vec2(Buffer1[4], Buffer1[5])));
#endif
    
#ifndef _MOBILE_
    AO += texture(SSAOTexture, OutTexCoord).r;
#endif
    Normal = normalize(Normal);
    
    vec4 tmpLightSpaceLocation = WorldToLight * vec4(WorldLocation, 1.0);
    vec3 LightSpaceLocation = (tmpLightSpaceLocation / tmpLightSpaceLocation.w).xyz;
    LightSpaceLocation = (LightSpaceLocation + 1.0) / 2.0;
    
	if (LightSpaceLocation.z > 1.0f)
		LightSpaceLocation.z = 1.0f;


#ifndef _MOBILE_
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
#else
     float ShadowDepth = texture(ShadowMapTexture, LightSpaceLocation.xy ).r; 
     float Shadow = LightSpaceLocation.z > ShadowDepth ? 1.0 : 0.0;
#endif


    



    vec3  Ambient = AmbientStrength * AO * LightColor;


    // mfs
    float diff = max(dot(Normal, -1.0f * LightDirection), 0.0);
    vec3 Diffuse = diff * LightColor;

    // jmfs 
    vec3 CameraDirection = normalize(CameraLocation - WorldLocation);
    vec3 HalfVector = normalize((-LightDirection + CameraDirection));
    // vec3 ReflectDirection = reflect(LightDirection, Normal);
    float spec = pow(max(dot(Normal, HalfVector), 0.0), 16.0f);

    vec3 Specular = specularStrength * spec * LightColor;

    glColor = vec4((Ambient + (Diffuse + Specular) * (1.0 - Shadow) ) * LightStrength * Color, 1.0f); 

}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;

    return WorldLocation;
}

float[8] MicroGBufferDecoding(sampler2D MicroGBuffer, ivec2 ScreenLocation)
{
	float res[8];
	vec2 pixelOffset = OutTexCoord / vec2(ScreenLocation);
	vec4 Buffer = texture(MicroGBuffer, OutTexCoord);
	float gray = Buffer.x;
	
	int xparity = (ScreenLocation.x % 2);
    int yparity = (ScreenLocation.y % 2);

	if ((xparity == 1 && yparity == 1) || (xparity == 0 && yparity == 0))
	{
		Buffer = texture(MicroGBuffer, OutTexCoord);
		vec2 rb = Buffer.yz;
		res[0] = rb.x;
		res[1] = (gray - (rb.x * 0.3f + rb.y * 0.11f)) / 0.59f;;
		res[2] = rb.y;
	}
	else 
	{
		vec2 rb;
		float counter = 0.0f;
		for(int i = 0; i < 3; i += 1)
		{
			for(int j = 0; j < 3; j += 1)
			{
				if ((i + j ) % 2 == 0)
					continue;
				Buffer = texture(MicroGBuffer, OutTexCoord + vec2(pixelOffset.x * float(i) - pixelOffset.x, pixelOffset.y * float(j) - pixelOffset.y) );
				if (abs(Buffer.x - gray) > 0.1f)
					continue;
				rb += Buffer.yz;
				counter++;
			}
		}
		if (counter > 0.0f)
		{
			rb /= counter;
			res[0] = rb.x;
			res[1] = (gray - (rb.x * 0.3f + rb.y * 0.11f)) / 0.59f;;
			res[2] = rb.y;
		}
		else 
		{
			discard;
		}
	}

	
	vec2 leftUpUV = vec2(0.0, 0.0f);
	vec2 rightUpUV = vec2(0.0, 0.0f);
	vec2 leftDownUV = vec2(0.0, 0.0f);
	vec2 rightDownUV = vec2(0.0, 0.0f);
	
	if (xparity == 0 && yparity == 0)
	{
		leftUpUV = OutTexCoord;
	}

	if (xparity == 1 && yparity == 0)
	{
	
		leftUpUV = OutTexCoord - vec2(pixelOffset.x, 0.0f);
	}

	if (xparity == 0 && yparity == 1)
	{
		leftUpUV = OutTexCoord - vec2(0.0f, pixelOffset.y);
	}

	if (xparity == 1 && yparity == 1)
	{
		leftUpUV = OutTexCoord - vec2(pixelOffset.x, pixelOffset.y);
	}
	rightUpUV = leftUpUV + vec2(pixelOffset.x, 0.0f);
	leftDownUV = leftUpUV + vec2(0.0f, pixelOffset.y);
	rightDownUV = leftUpUV + vec2(pixelOffset.x, pixelOffset.y);

	Buffer = texture(MicroGBuffer, rightUpUV);
	res[4] = Buffer.y;
	res[5] = Buffer.z;
	
	Buffer = texture(MicroGBuffer, leftDownUV);
	//r
	res[6] = Buffer.y;
	// m
	res[7] = Buffer.z;
	// ao
	res[3] = Buffer.w;


	return res;


}

