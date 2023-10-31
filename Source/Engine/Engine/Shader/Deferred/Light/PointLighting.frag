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
uniform float FarPlan;
uniform float LightStrength;

const float PI=3.1415926f;



vec3 GetWorldLocation(vec3 ScreenLocation);
float ShadowCalculation(vec3 fragPos);
float[8] MicroGBufferDecoding(sampler2D MicroGBuffer, ivec2 ScreenLocation);
float GetShadowFromTexture(vec3 WorldLocation, sampler2D tex, mat4 WorldToLight);


float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness*roughness;
    float a2 = a*a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;

    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}


float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r*r) / 8.0;

    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}



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


vec3 CalcLightPoint(vec3 albedo, float AO, float metallic, float roughness, vec3 Normal, vec3 FragWorldLocation)
{
	vec3 N = Normal;
    vec3 V = normalize(CameraLocation - FragWorldLocation);

    // calculate reflectance at normal incidence; if dia-electric (like plastic) use F0 
    // of 0.04 and if it's a metal, use the albedo color as F0 (metallic workflow)    
    vec3 F0 = vec3(0.04); 
    F0 = mix(F0, albedo, metallic);

    // reflectance equation
    vec3 Lo = vec3(0.0);

	vec3 L = normalize(LightLocation - FragWorldLocation);
    vec3 H = normalize(V + L);
    float distance = length(LightLocation - FragWorldLocation);
    float attenuation = 1.0 / (distance * distance);
    vec3 radiance = LightColor * attenuation;

    // Cook-Torrance BRDF
    float NDF = DistributionGGX(N, H, roughness);   
    float G   = GeometrySmith(N, V, L, roughness);      
    vec3 F    = fresnelSchlick(max(dot(H, V), 0.0), F0);
           
    vec3 numerator    = NDF * G * F; 
    float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001; // + 0.0001 to prevent divide by zero
    vec3 specular = numerator / denominator;
        
    // kS is equal to Fresnel
    vec3 kS = F;
    // for energy conservation, the diffuse and specular light can't
    // be above 1.0 (unless the surface emits light); to preserve this
    // relationship the diffuse component (kD) should equal 1.0 - kS.
    vec3 kD = vec3(1.0) - kS;
    // multiply kD by the inverse metalness such that only non-metals 
    // have diffuse lighting, or a linear blend if partly metal (pure metals
    // have no diffuse light).
    kD *= 1.0 - metallic;	  

    // scale light by NdotL
    float NdotL = max(dot(N, L), 0.0);        

    // add to outgoing radiance Lo
    return (kD * albedo / PI + specular) * radiance * NdotL;  // note that we already multiplied the BRDF by the Fresnel (kS) so we won't multiply by kS again
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
    vec3 Normal = (Normal2DTo3D(Buffer2.xy* 2.0 - 1.0));
	float metallic = Buffer2.z;
	float roughness = Buffer2.w;
#else
	float Buffer1[8] = MicroGBufferDecoding(ColorTexture,  ivec2(gl_FragCoord.xy));
    vec3 Color = vec3(Buffer1[0], Buffer1[1], Buffer1[2]);
    float AO = Buffer1[3]; 
	float metallic = Buffer1[6]; 
	float roughness = Buffer1[7]; 
    vec3 Normal = (Normal2DTo3D(vec2(Buffer1[4], Buffer1[5])* 2.0 - 1.0));
#endif
    
#ifndef _MOBILE_
    AO += texture(SSAOTexture, OutTexCoord).r;
#endif
    Normal = normalize(Normal);


    float Shadow = ShadowCalculation(WorldLocation); 
	vec3 PBRColor = CalcLightPoint(Color, AO, metallic,roughness, Normal, WorldLocation);
   

    vec3 result = (PBRColor * (1.0 - Shadow))*  LightStrength;//vec4((Ambient + (Diffuse + Specular) * (1.0 - Shadow) ) * LightStrength * Color, 1.0f); 
    

    glColor = vec4(result, 1.0f);

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