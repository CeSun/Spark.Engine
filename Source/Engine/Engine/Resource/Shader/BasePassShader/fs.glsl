#version 300 es
precision highp float;
//{MacroSourceCode}
#ifndef _DEPTH_ONLY_
layout (location = 0) out vec4 Buffer_BaseColor_AO;
layout (location = 1) out vec4 Buffer_Normal_Metalness_Roughness;
#endif

//{IncludeSourceCode}
uniform sampler2D Texture_BaseColor;
#ifndef _DEPTH_ONLY_
uniform sampler2D Texture_Normal;
uniform sampler2D Texture_Metalness;
uniform sampler2D Texture_Roughness;
uniform sampler2D Texture_Occlusion;
#endif

in vec2 texcoord;
#ifndef _DEPTH_ONLY_
in mat3 TBNTransform;
#endif

vec2 Normal3Dto2D(vec3 Normal);

void main()
{
	vec4 BaseColor = texture(Texture_BaseColor, texcoord);
#ifdef _BLENDMODE_MASKED_
	if (BaseColor.a < 0.01)
		discard;
#endif
#ifndef _DEPTH_ONLY_
	vec3 Normal = texture(Texture_Normal, texcoord).xyz;
	float Metalness = texture(Texture_Metalness, texcoord).x;
	float Roughness = texture(Texture_Roughness, texcoord).x;
	float AO = texture(Texture_Occlusion, texcoord).x;
	
	Normal = normalize(Normal* 2.0 - 1.0); 
	vec3 WorldNormal = normalize(TBNTransform * Normal);

	Buffer_BaseColor_AO = vec4(BaseColor.xyz, AO);
	Buffer_Normal_Metalness_Roughness = vec4(Normal3Dto2D(WorldNormal) * 0.5 + 0.5, Metalness, Roughness);
#endif
}