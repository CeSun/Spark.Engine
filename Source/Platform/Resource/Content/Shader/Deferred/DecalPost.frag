#version 300 es

precision highp float;
layout (location = 0) out vec4 Buffer1;
layout (location = 1) out vec4 Buffer2;

in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform vec2 TexCoordScale;
uniform sampler2D DecalTexture;
uniform sampler2D DecalDepthTexture;


float[8] MicroGBufferDecoding(sampler2D MicroGBuffer, ivec2 ScreenLocation);
vec3 Normal2DTo3D(vec2 Normal);


void main()
{
	float depth = texture(DecalDepthTexture, OutTexCoord).r;
	if (depth >= 1.0f)
		discard;
	float res[8] = MicroGBufferDecoding(DecalTexture, ivec2(gl_FragCoord.xy));
	Buffer1 = vec4(res[0],res[1],res[2], res[3]);
	Buffer2 = vec4(res[4],res[5],res[6], res[7]);
}

float[8] MicroGBufferDecoding(sampler2D MicroGBuffer, ivec2 ScreenLocation)
{
	float res[8];
	vec2 pixelOffset = OutTexCoord / vec2(ScreenLocation);
	vec4 Buffer = texture(MicroGBuffer, OutTexCoord);
	float gray = Buffer.x;
	vec2 leftUpUV = vec2(0.0, 0.0f);
	vec2 rightUpUV = vec2(0.0, 0.0f);
	vec2 leftDownUV = vec2(0.0, 0.0f);
	vec2 rightDownUV = vec2(0.0, 0.0f);
	

	int xparity = (ScreenLocation.x % 2);
    int yparity = (ScreenLocation.y % 2);

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

	

	Buffer = texture(MicroGBuffer, leftUpUV);
	vec2 rb = Buffer.yz;
	Buffer = texture(MicroGBuffer, rightDownUV);
	rb += Buffer.yz;

	rb /= 2.0f;


	res[0] = rb.x;
	res[1] = (gray - (rb.x * 0.3f + rb.y * 0.11f)) / 0.59f;;
	res[2] = rb.y;


	
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
