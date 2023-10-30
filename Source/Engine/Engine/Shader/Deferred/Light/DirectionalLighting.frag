#version 300 es

precision highp float;
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D ColorTexture;
uniform sampler2D CustomBuffer;
uniform sampler2D DepthTexture;
uniform sampler2D ShadowMapTexture;

uniform mat4 WorldToLight;
uniform mat4 VPInvert;
uniform vec3 LightDirection;
uniform vec3 LightColor;
uniform vec3 CameraLocation;
uniform float AmbientStrength;
uniform float LightStrength;

const float PI=3.1415926f;
const float LightDistance = 10.0f;

vec3 GetWorldLocation(vec3 ScreenLocation);
float[8] MicroGBufferDecoding(sampler2D MicroGBuffer, ivec2 ScreenLocation);


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

vec3 CalcLightDirectional(vec3 albedo, float AO, float metallic, float roughness, vec3 Normal, vec3 FragWorldLocation)
{   
    vec3 N = Normal;
    vec3 V = normalize(CameraLocation - FragWorldLocation);

    // calculate reflectance at normal incidence; if dia-electric (like plastic) use F0 
    // of 0.04 and if it's a metal, use the albedo color as F0 (metallic workflow)    
    vec3 F0 = vec3(0.04); 
    F0 = mix(F0, vec3(albedo), vec3(metallic));
	vec3 L = -1.0f * LightDirection;
    vec3 H = normalize(V + L);
    float attenuation = 1.0;
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

    kD *= 1.0 - metallic;	  

    // scale light by NdotL
    float NdotL = max(dot(N, L), 0.0);        

    // add to outgoing radiance Lo
    return (kD * albedo / PI + specular) * radiance * NdotL;  // note that we already multiplied the BRDF by the Fresnel (kS) so we won't multiply by kS again
}


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
	float metallic = Buffer2.z;
	float roughness = Buffer2.w;
#else
	float Buffer1[8] = MicroGBufferDecoding(ColorTexture,  ivec2(gl_FragCoord.xy));
    vec3 Color = vec3(Buffer1[0], Buffer1[1], Buffer1[2]);
    float AO = Buffer1[3]; 
	float metallic = Buffer1[6]; 
	float roughness = Buffer1[7]; 
    vec3 Normal = (Normal2DTo3D(vec2(Buffer1[4], Buffer1[5])));
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

	vec3 PBRColor = CalcLightDirectional(Color, AO, metallic,roughness, Normal, WorldLocation);
   

    vec3 result = AmbientStrength * LightColor  + (PBRColor * (1.0 - Shadow))* LightStrength;//vec4((Ambient + (Diffuse + Specular) * (1.0 - Shadow) ) * LightStrength * Color, 1.0f); 
    

    glColor = vec4(result, 1.0f);
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
	// a
	res[3] = Buffer.w;


	return res;


}

