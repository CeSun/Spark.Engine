#version 300 es

precision highp float;
out vec4 FragColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D ColorTexture;
uniform sampler2D DepthTexture;
uniform sampler2D SSAOTexture;
#ifndef _MOBILE_
uniform sampler2D CustomBuffer;
uniform mat4 VPInvert;
uniform vec3 CameraLocation;
uniform samplerCube irradianceMap;
uniform samplerCube prefilterMap;
uniform sampler2D brdfLUT;
#endif

vec3 GetWorldLocation(vec3 ScreenLocation);
vec3 Normal2DTo3D(vec2 Oct);
float[8] MicroGBufferDecoding(sampler2D MicroGBuffer, ivec2 ScreenLocation);
vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness);

void main()
{
	float depth = texture(DepthTexture, OutTexCoord).r;
	
#ifndef _MOBILE_
	float ssao = texture(SSAOTexture, OutTexCoord).r;
    vec3 WorldLocation = GetWorldLocation(vec3(OutTrueTexCoord, depth));
#else
	float ssao = 1.0f;
#endif
	
	if (depth == 1.0)
		discard;
#ifndef _MICRO_GBUFFER_
    vec4 Buffer1 = texture(ColorTexture, OutTexCoord);
    vec3 Color = Buffer1.rgb;
    float AO = Buffer1.a;
#ifndef _MOBILE_
    vec4 Buffer2 = texture(CustomBuffer, OutTexCoord);
    vec3 Normal = (Normal2DTo3D(Buffer2.xy* 2.0 - 1.0));
	float metallic = Buffer2.z;
	float roughness = Buffer2.w;
#endif
#else
	float Buffer1[8] = MicroGBufferDecoding(ColorTexture,  ivec2(gl_FragCoord.xy));
    vec3 Color = vec3(Buffer1[0], Buffer1[1], Buffer1[2]);
#ifndef _MOBILE_
    float AO = Buffer1[3]; 
	float metallic = Buffer1[6]; 
	float roughness = Buffer1[7]; 
    vec3 Normal = (Normal2DTo3D(vec2(Buffer1[4], Buffer1[5])* 2.0 - 1.0));
#endif
#endif

    Color = pow(Color, vec3(2.2));
#ifndef _MOBILE_

    vec3 V = normalize(CameraLocation - WorldLocation);
    Normal = normalize(Normal);
    vec3 R = reflect(-V, Normal); 

	vec3 F0 = vec3(0.04); 
    F0 = mix(F0, Color, metallic);

	vec3 F = fresnelSchlickRoughness(max(dot(Normal, V), 0.0), F0, roughness);
    
    vec3 kS = F;
    vec3 kD = 1.0 - kS;
    kD *= 1.0 - metallic;	  
    
    vec3 irradiance = texture(irradianceMap, Normal).rgb;
    vec3 diffuse      = irradiance * Color;
    
    // sample both the pre-filter map and the BRDF lut and combine them together as per the Split-Sum approximation to get the IBL specular part.
    const float MAX_REFLECTION_LOD = 4.0;
    vec3 prefilteredColor = textureLod(prefilterMap, R,  roughness * MAX_REFLECTION_LOD).rgb;   
	vec2 uv = vec2(max(dot(Normal, V), 0.0), roughness);
	uv.y = 1.0 - uv.y;
    vec2 brdf  = texture(brdfLUT,uv ).rg;
	brdf = pow(brdf, vec2(2.2));
    vec3 specular = prefilteredColor * (F * brdf.x + brdf.y);

    vec3 ambient = (kD * diffuse + specular) * AO  * ssao;
    
    FragColor = vec4(ambient , 1.0);
#else
	FragColor = vec4(Color * 0.03f * ssao, 1.0);
#endif
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
	// a
	res[3] = Buffer.w;


	return res;


}

#ifndef _MOBILE_
vec3 Normal2DTo3D(vec2 Oct)
{
	vec3 N = vec3( Oct, 1.0 - dot( vec2(1.0f), abs(Oct) ) );
    if( N.z < 0.0f )
    {
		vec2 add;
		if (N.x >= 0.0f)
			add.x = 1.0f;
		else
			add.x = -1.0f;

		if (N.y >= 0.0f)
			add.y = 1.0f;
		else
			add.y = -1.0f;
		N.xy = ( 1.0f - abs(N.yx) ) * add;
    }
    return normalize(N);
}

vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness)
{
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}   

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;

    return WorldLocation;
}
#endif