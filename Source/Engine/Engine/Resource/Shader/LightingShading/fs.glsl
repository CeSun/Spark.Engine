﻿#version 300 es
precision highp float;
//{MacroSourceCode}
layout (location = 0) out vec4 Buffer_Color;
//{IncludeSourceCode}

uniform sampler2D Buffer_BaseColor_AO;
uniform sampler2D Buffer_Normal_Metalness_Roughness;
uniform sampler2D Buffer_Depth;

in vec2 texCoord;

uniform vec3 CameraPosition;
uniform mat4 ViewProjectionInverse;

uniform vec3 LightColor;
uniform float LightStrength;

#if defined  _DIRECTIONAL_LIGHT_ || defined _SPOT_LIGHT_
uniform vec3 LightForwardDirection;
#endif

#if defined  _POINT_LIGHT_ || defined _SPOT_LIGHT_
uniform vec3 LightPosition;
uniform float LightFalloffRadius;
#endif

#ifdef _SPOT_LIGHT_
uniform float InnerCosine;
uniform float OuterCosine;
#endif

#ifdef _WITH_SHADOW_

#ifdef _DIRECTIONAL_LIGHT_
uniform sampler2D Buffer_ShadowMaps[_CSM_LEVEL_];
uniform mat4 LightViewProjections[_CSM_LEVEL_];
uniform float CSMFars[_CSM_LEVEL_];
uniform mat4 View;
#elif defined _SPOT_LIGHT_
uniform sampler2D Buffer_ShadowMap;
uniform mat4 LightViewProjection;
#elif defined _POINT_LIGHT_
uniform samplerCube Buffer_ShadowCubeMap;
uniform mat4 LightViewProjections[6];
#endif

#endif

vec3 Normal2DTo3D(vec2 Oct);
vec3 calculateWorldPosition(vec3 clipSpacePosition, mat4 viewProjectionInverseMatrix);
vec3 CalculatePbrLighting(vec3 baseColor, float metalness,float roughness, vec3 normal,  float lightAttenuation, vec3 lightColor, vec3 lightDirection, vec3 cameraDirection);
vec3 CalculateBlinnPhongLighting(vec3 baseColor, float metalness,float roughness, vec3 normal,  float lightAttenuation, vec3 lightColor, vec3 lightDirection, vec3 cameraDirection);

int getFaceIndex(vec3 direction);

void main()
{
	// 先对深度进行采样
	float depth = texture(Buffer_Depth, texCoord).x;
	vec4 BaseColor_AO = texture(Buffer_BaseColor_AO, texCoord);
	vec4 Normal_Metalness_Roughness = texture(Buffer_Normal_Metalness_Roughness, texCoord);

	
	vec3 BaseColor = BaseColor_AO.xyz;
	vec3 Normal = Normal2DTo3D(Normal_Metalness_Roughness.xy * 2.0 - vec2(1.0));
	float Metalness = Normal_Metalness_Roughness.z;
	float Roughness = Normal_Metalness_Roughness.w;
	float AO = BaseColor_AO.w;
	
	vec3 worldPosition = calculateWorldPosition(vec3(texCoord * 2.0 - 1.0, depth* 2.0 - 1.0), ViewProjectionInverse);
	vec3 cameraDirection = normalize(worldPosition - CameraPosition);
	
	// 摄像机方向
#if defined  _POINT_LIGHT_ || defined _SPOT_LIGHT_
    float distance = length(worldPosition - LightPosition);
    float attenuation = 1.0 / pow(distance/LightFalloffRadius, 2.0);
#else
	float attenuation = 1.0;
#endif

	// 光源方向
#if defined  _POINT_LIGHT_ || defined _SPOT_LIGHT_
	vec3 lightDirection = normalize(worldPosition - LightPosition);
#elif defined _DIRECTIONAL_LIGHT_
	vec3 lightDirection = LightForwardDirection;
#else
	vec3 lightDirection = vec3(1.0);
#endif

	vec3 Lo = CalculatePbrLighting(BaseColor, Metalness, Roughness, Normal, attenuation, LightColor, lightDirection, cameraDirection);

#ifdef _SPOT_LIGHT_
	float theta = dot(-1.0 * lightDirection, -1.0 * LightForwardDirection); 
    float epsilon = (InnerCosine - OuterCosine);
    float intensity = clamp((theta - OuterCosine) / epsilon, 0.0, 1.0);
	Lo *= intensity;
#endif

#ifdef _WITH_SHADOW_


	float shadowMapDepth = 1.0;
#ifdef _DIRECTIONAL_LIGHT_
	vec4 viewSpacePos = View * vec4(worldPosition, 1.0);
	float fragToCameraDistance = abs(viewSpacePos.z);
	int csmLevel = 0;

	for (csmLevel = 0 ; csmLevel < _CSM_LEVEL_; csmLevel++)
	{
		if (fragToCameraDistance < CSMFars[csmLevel])
			break;
	}

	mat4 LightViewProjection = LightViewProjections[csmLevel];
#endif
#ifdef _POINT_LIGHT_
	int matrixIndex = getFaceIndex(lightDirection);
	mat4 LightViewProjection = LightViewProjections[matrixIndex];
#endif

	vec4 lightSpacePosition = LightViewProjection * vec4(worldPosition, 1.0);
	lightSpacePosition = lightSpacePosition / lightSpacePosition.w;
	float currectShadowSpaceDepth = lightSpacePosition.z;

#if  defined _SPOT_LIGHT_
	vec2 lightTextureUV = lightSpacePosition.xy / 2.0 + 0.5;
	shadowMapDepth = texture(Buffer_ShadowMap, lightTextureUV).x * 2.0 - 1.0;
#elif defined  _DIRECTIONAL_LIGHT_
	vec2 lightTextureUV = lightSpacePosition.xy / 2.0 + 0.5;
	shadowMapDepth = texture(Buffer_ShadowMaps[csmLevel], lightTextureUV).x * 2.0 - 1.0;
#elif defined _POINT_LIGHT_
	shadowMapDepth = texture(Buffer_ShadowCubeMap, lightDirection).x * 2.0 - 1.0;
#endif
	
	if (shadowMapDepth
#ifndef _DIRECTIONAL_LIGHT_
	+ 0.005 
#endif
	<= currectShadowSpaceDepth)
		Lo = vec3(0.0);
	else
		Lo = Lo;
#endif


	Buffer_Color = vec4(Lo * LightStrength , 0.0f);
}

