#version 300 es

precision highp float;
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D ColorTexture;
uniform sampler2D CustomBuffer;
uniform sampler2D DepthTexture;
uniform samplerCube ShadowMapTextue;
#ifndef _MOBILE_
uniform sampler2D SSAOTexture;
#endif
uniform mat4 VPInvert;
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
    // get vector between fragment position and light position
    vec3 fragToLight = WorldLocation - LightLocation;
    // ise the fragment to light vector to sample from the depth map    
    float closestDepth = texture(ShadowMapTextue, fragToLight).r;
    // it is currently in linear range between [0,1], let's re-transform it back to original depth value
    closestDepth *= FarPlan;
    // now get current linear depth as the length between the fragment and light position
    float currentDepth = length(fragToLight);
    // test for shadows
    float shadow = currentDepth > closestDepth ? 1.0 : 0.0;       

    return shadow;
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