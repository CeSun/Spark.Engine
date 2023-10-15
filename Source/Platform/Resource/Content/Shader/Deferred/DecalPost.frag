#version 300 es

precision highp float;
layout (location = 0) out vec4 Buffer1;

in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D DecalTexture;
uniform sampler2D DecalDepthTexture;


void main()
{
	float depth = texture(DecalDepthTexture, OutTexCoord).r;
	if (depth >= 1.0f)
		discard;
	vec4 Color = texture(DecalTexture, OutTexCoord);
	if (Color.a < 0.1f)
		discard;
	Buffer1 = texture(DecalTexture, OutTexCoord);
}