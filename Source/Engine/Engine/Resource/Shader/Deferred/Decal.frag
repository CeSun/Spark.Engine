#version 300 es

precision highp float;

out vec4 Color;

uniform sampler2D BaseColorTexture;
uniform sampler2D NormalTexture;
uniform sampler2D CustomTexture;


uniform sampler2D DepthTexture;
uniform sampler2D GBuffer1;
#ifndef _MICRO_GBUFFER_
uniform sampler2D GBuffer2;
#endif
uniform mat4 VPInvert;

in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
in mat4 ModelInvertTransform;

vec3 GetWorldLocation(vec3 ScreenLocation);
vec4 MicroGBufferEncoding(vec3 BaseColor, vec2 Normal, float r, float m, float ao, ivec2 ScreenLocation);

float[8] MicroGBufferDecoding(sampler2D MicroGBuffer, ivec2 ScreenLocation);

void main() 
{
	vec2 uv = OutTexCoord;
	float depth = texture(DepthTexture, uv).r;
	
	if (depth < gl_FragCoord.z)
		discard;
	vec3 WorldLocation = GetWorldLocation(vec3(OutTrueTexCoord, depth));

	vec4 ModelLocation = ModelInvertTransform * vec4(WorldLocation, 1.0f);
	ModelLocation = ModelLocation / ModelLocation.w;

	if (ModelLocation.x > 1.0f || ModelLocation.x < -1.0f)
		discard;
	if (ModelLocation.y > 1.0f || ModelLocation.y < -1.0f)
		discard;
	if (ModelLocation.z > 1.0f || ModelLocation.z < -1.0f)
		discard;
	
	uv = (ModelLocation.xy + vec2(1.0f, 1.0f)) / 2.0f;
	vec4 tColor = texture(BaseColorTexture, uv);
	if (tColor.a < 0.1f)
		discard;


	#ifndef _MICRO_GBUFFER_
		vec4 Buffer1 =  texture(GBuffer1, OutTexCoord);
		vec4 Buffer2 =  texture(GBuffer2, OutTexCoord);
	#else
		float data[8] = MicroGBufferDecoding(GBuffer1, ivec2(gl_FragCoord.xy));
		vec4 Buffer1 = vec4(data[0], data[1], data[2], data[3]);
		vec4 Buffer2 = vec4(data[4], data[5], data[6], data[7]);
	#endif
		Color = MicroGBufferEncoding(tColor.rgb, Buffer2.xy, Buffer2.z, Buffer2.w, Buffer1.w, ivec2(gl_FragCoord.xy));

	
}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;

    return WorldLocation;
}

vec4 MicroGBufferEncoding(vec3 BaseColor, vec2 Normal, float r, float m, float ao, ivec2 ScreenLocation)
{
	vec4 result = vec4(0.0f, 0.0f, 0.0f, 0.0f);

	float gray = BaseColor.r * 0.3f + BaseColor.g * 0.59f + BaseColor.b * 0.11f;
	result.x = gray;
    int xparity = (ScreenLocation.x % 2);
    int yparity = (ScreenLocation.y % 2);
	if (xparity == 0 && yparity == 0)
	{
		result.y = BaseColor.r;
		result.z = BaseColor.b;
	}

	if (xparity == 1 && yparity == 0)
	{
		result.y = Normal.x;
		result.z = Normal.y;
	}

	if (xparity == 0 && yparity == 1)
	{
		result.y = r;
		result.z = m;
		result.w = ao;
	}

	if (xparity == 1 && yparity == 1)
	{
		result.y = BaseColor.r;
		result.z = BaseColor.b;
	}

	return result;
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