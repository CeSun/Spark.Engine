#version 300 es
precision highp float;
//{MacroSourceCode}
layout (location = 0) out vec4 Buffer_Color;
//{IncludeSourceCode}

uniform sampler2D Buffer_BaseColor_AO;
uniform sampler2D Buffer_Normal_Metalness_Roughness;


in vec2 texCoord;

uniform vec3 CameraPosition;
uniform vec3 CameraDirection;


uniform vec3 LightColor;
uniform vec3 LightDirection;
uniform float LightStrength;

#ifdef _DIRECTIONAL_LIGHT_
uniform vec3 LightForward;
#endif

#if defined  _POINT_LIGHT_ || defined _SPOT_LIGHT_
uniform vec3 LightPosition;
#endif

#ifdef _SPOT_LIGHT_
uniform float InnerCosine;
uniform float OuterCosine;
#endif

vec3 Normal2DTo3D(vec2 Oct);

void main()
{
	vec4 BaseColor_AO = texture(Buffer_BaseColor_AO, texCoord);
	vec4 Normal_Metalness_Roughness = texture(Buffer_Normal_Metalness_Roughness, texCoord);
	
	vec3 BaseColor = BaseColor_AO.xyz;
	float AO = BaseColor_AO.w;
	vec3 Normal = Normal2DTo3D(Normal_Metalness_Roughness.xy);
	float Metalness = Normal_Metalness_Roughness.z;
	float Roughness = Normal_Metalness_Roughness.w;


}

