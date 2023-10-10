#version 300 es

precision highp float;

out vec4 Color;

uniform sampler2D Diffuse;
uniform sampler2D DepthTexture;
uniform mat4 VPInvert;

in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
in mat4 ModelInvertTransform;

vec3 GetWorldLocation(vec3 ScreenLocation);

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
	vec4 tColor = texture(Diffuse, uv);
	if (tColor.a < 0.1f)
		discard;
	Color = tColor;
	
}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;

    return WorldLocation;
}
