#version 330 core

out vec4 Color;

uniform vec2 TexCoordScale;
uniform sampler2D Diffuse;
uniform sampler2D DepthTexture;
uniform mat4 VPInvert;

in mat4 ModelInvertTransform;
in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;

vec3 GetWorldLocation(vec3 ScreenLocation);

void main() 
{
	vec2 uv = OutTexCoord;
	float depth = texture(DepthTexture, uv).r;
	
	if (depth < gl_FragCoord.z)
		discard;
	vec3 WorldLocation = GetWorldLocation(vec3(uv, depth));

	vec4 ModelLocation = ModelInvertTransform * vec4(WorldLocation, 1.0f);
	ModelLocation = ModelLocation / ModelLocation.w;

	if (ModelLocation.x > 1 || ModelLocation.x < -1)
		discard;
	if (ModelLocation.y > 1 || ModelLocation.y < -1)
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
