#version 300 es

precision highp float;

out vec4 Color;

uniform sampler2D BaseColorTexture;
uniform sampler2D NormalTexture;
uniform sampler2D CustomTexture;


uniform sampler2D DepthTexture;
uniform sampler2D GBuffer1;
uniform sampler2D GBuffer2;
uniform mat4 VPInvert;

in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
in mat4 ModelInvertTransform;

vec3 GetWorldLocation(vec3 ScreenLocation);
vec4 MicroGBufferEncoding(vec3 BaseColor, vec2 Normal, float r, float m, float ao, ivec2 ScreenLocation);

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


	vec4 Buffer1 =  texture(GBuffer1, OutTexCoord);
	vec4 Buffer2 =  texture(GBuffer2, OutTexCoord);

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