#version 300 es
#extension GL_EXT_gpu_shader5 : enable
precision highp float;
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D ColorTexture;
uniform sampler2D CustomBuffer;
uniform sampler2D DepthTexture;
uniform sampler2D ShadowMapTextures0;
uniform sampler2D ShadowMapTextures1;
uniform sampler2D ShadowMapTextures2;
uniform sampler2D ShadowMapTextures3;
uniform sampler2D ShadowMapTextures4;
uniform sampler2D ShadowMapTextures5;
#ifndef _MOBILE_
uniform sampler2D SSAOTexture;
#endif
uniform mat4 VPInvert;

uniform mat4 WorldToLights[6];
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
float[8] MicroGBufferDecoding(sampler2D MicroGBuffer, ivec2 ScreenLocation);
float GetShadowFromTexture(vec3 WorldLocation, sampler2D tex, mat4 WorldToLight);


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
    float specularStrength = 0.5;
   
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
    vec3 fragToLight = normalize(WorldLocation - LightLocation);  
	if (abs(fragToLight.x) > abs(fragToLight.y) && abs(fragToLight.x) > abs(fragToLight.z))
	{
		if (fragToLight.x > 0.0f)
		{
			return GetShadowFromTexture(WorldLocation, ShadowMapTextures3, WorldToLights[3]);
		}
		else 
		{
			
			return GetShadowFromTexture(WorldLocation, ShadowMapTextures2, WorldToLights[2]);
		}
	}
	if (abs(fragToLight.y) > abs(fragToLight.x) && abs(fragToLight.y) > abs(fragToLight.z))
	{
		if (fragToLight.y > 0.0f)
		{
			return GetShadowFromTexture(WorldLocation, ShadowMapTextures4, WorldToLights[4]);
		}
		else 
		{
			return GetShadowFromTexture(WorldLocation, ShadowMapTextures5, WorldToLights[5]);
		}
	}
	if (abs(fragToLight.z) > abs(fragToLight.x) && abs(fragToLight.z) > abs(fragToLight.y))
	{
		if (fragToLight.z > 0.0f)
		{
			return GetShadowFromTexture(WorldLocation, ShadowMapTextures1, WorldToLights[1]);
		}
		else 
		{
			return GetShadowFromTexture(WorldLocation, ShadowMapTextures0, WorldToLights[0]);
		}
	}

	return 0.0f;
}

float GetShadowFromTexture(vec3 WorldLocation, sampler2D tex, mat4 WorldToLight)
{
	vec4 tmpLightSpaceLocation = WorldToLight * vec4(WorldLocation, 1.0);
	vec3 LightSpaceLocation = (tmpLightSpaceLocation / tmpLightSpaceLocation.w).xyz;
	LightSpaceLocation = (LightSpaceLocation + 1.0) / 2.0;
    
	if (LightSpaceLocation.z > 1.0f)
		LightSpaceLocation.z = 1.0f;

	#ifndef _MOBILE_
		float Shadow = 0.0;
		vec2 texelSize = 1.0f / vec2(textureSize(tex, 0));
		for(int x = -1; x <= 1; ++x)
		{
			for(int y = -1; y <= 1; ++y)
			{
				vec2 localUV = LightSpaceLocation.xy + vec2(x, y) * texelSize;
				
				localUV.x = clamp(localUV.x, 0.0f, 1.0f);
				localUV.y = clamp(localUV.y, 0.0f, 1.0f);
				float ShadowDepth = texture(tex, localUV).r; 
				Shadow += LightSpaceLocation.z > ShadowDepth ? 1.0 : 0.0 ;      
			}    
		}
		Shadow /= 9.0;
	#else
		float ShadowDepth = texture(tex, LightSpaceLocation.xy ).r; 
		float Shadow = LightSpaceLocation.z > ShadowDepth ? 1.0 : 0.0;
	#endif

	
	return Shadow;
}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    // ScreenLocation.z = ScreenLocation.z * -1;
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