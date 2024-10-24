﻿const float PI = 3.14159265359;
// 3d法线转换到2d法线
vec2 Normal3Dto2D(vec3 Normal)
{   
    Normal.xy /= dot( vec3(1.0f), abs(Normal) );
    if( Normal.z <= 0.0f )
    {
        vec2 add;
        if (Normal.x >= 0.0f)
            add.x = 1.0f;
        else 
            add.x = -1.0f;
        if (Normal.y >= 0.0f)
            add.y = 1.0f;
        else 
            add.y = -1.0f;
        Normal.xy = ( 1.0f - abs(Normal.yx) ) * add ;
    }
    return Normal.xy;
}
// 2d法线转换3d法线
vec3 Normal2DTo3D(vec2 Oct)
{
	vec3 N = vec3( Oct, 1.0 - dot( vec2(1.0f), abs(Oct) ) );
    if( N.z < 0.0f )
    {
		vec2 add;
		if (N.x >= 0.0f)
			add.x = 1.0f;
		else
			add.x = -1.0f;

		if (N.y >= 0.0f)
			add.y = 1.0f;
		else
			add.y = -1.0f;
		N.xy = ( 1.0f - abs(N.yx) ) * add;
    }
    return normalize(N);
}

float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness*roughness;
    float a2 = a*a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;

    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}


float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r*r) / 8.0;

    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}
// 计算pbr直接光照
vec3 CalculatePbrLighting(vec3 baseColor, float metalness,float roughness, vec3 normal,  float lightAttenuation, vec3 lightColor, vec3 lightDirection, vec3 cameraDirection)
{
    vec3 F0 = vec3(0.04); 
    F0 = mix(F0, baseColor, metalness);

    vec3 V = normalize(-1.0 * cameraDirection);
    vec3 L = normalize(-1.0 * lightDirection);
    vec3 H = normalize(V + L);
    
    vec3 radiance = lightColor * lightAttenuation;

    float NDF = DistributionGGX(normal, H, roughness);
    float G = GeometrySmith(normal, V, L, roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - metalness;     

    vec3 nominator    = NDF * G * F;
    float denominator = 4.0 * max(dot(normal, V), 0.0) * max(dot(normal, L), 0.0) + 0.001; 
    vec3 specular     = nominator / denominator;

    float NdotL = max(dot(normal, L), 0.0); 

    return (kD * baseColor / PI + specular) * radiance * NdotL;
}


vec3 CalculateBlinnPhongLighting(vec3 baseColor, float metalness,float roughness, vec3 normal,  float lightAttenuation, vec3 lightColor, vec3 lightDirection, vec3 cameraDirection)
{
    vec3 lightDir = -1.0 * lightDirection;
    float diff = max(dot(normal, lightDir), 0.0);
    vec3 diffuse = diff * lightColor;
	
	float specularStrength = 0.5;
    vec3 viewDir = -1.0 * cameraDirection;
     vec3 halfwayDir = normalize(lightDir + viewDir);  
     float spec = pow(max(dot(normal, halfwayDir), 0.0), 16.0);

    vec3 specular = specularStrength * spec * lightColor;  
	return (diffuse + specular) * lightAttenuation * baseColor;
}

// 裁剪空间[-1, 1] 转换到 世界空间
vec3 calculateWorldPosition(vec3 clipSpacePosition, mat4 viewProjectionInverseMatrix)
{
    vec4 viewSpacePosition = viewProjectionInverseMatrix * vec4(clipSpacePosition, 1.0);
    viewSpacePosition /= viewSpacePosition.w; 
    return vec3(viewSpacePosition);
}


// 自定义 saturate 函数
float saturate(float value) {
    return clamp(value, 0.0, 1.0);
}

vec3 saturate(vec3 value) {
    return clamp(value, vec3(0.0), vec3(1.0));
}

vec4 saturate(vec4 value) {
    return clamp(value, vec4(0.0), vec4(1.0));
}

// ACES色调映射函数
vec3 ACESFilmToneMapping(vec3 color)
{
    // ACES色调映射公式
    float A = 2.51;
    float B = 0.03;
    float C = 2.43;
    float D = 0.59;
    float E = 0.14;

    // 计算色调映射
    vec3 mappedColor = (color * (A * color + B)) / (color * (C * color + D) + E);
    
    // 确保颜色在[0, 1]范围内
    return saturate(mappedColor);
}


/*
// 主要的光照合并函数
vec3 CombineLighting(vec3 directLight, vec3 indirectLight, vec3 normal, vec3 viewDir, 
                     float roughness, float metallic, vec3 albedo)
{
    float smoothness = 1.0 - roughness;
    float ssrIntensity = smoothness * smoothness;
    
    // 基础反射率
    vec3 F0 = mix(vec3(0.04), albedo, metallic);
    
    // 计算菲涅尔
    float NdotV = max(dot(normal, viewDir), 0.0);
    vec3 F = fresnelSchlick(NdotV, F0);
    
    // 分离漫反射和镜面反射
    vec3 kS = F;
    vec3 kD = (1.0 - kS) * (1.0 - metallic);
    
    // 直接光照
    vec3 diffuseDirect = kD * directLight * albedo / PI;
    vec3 specularDirect = kS * directLight;
    
    // 间接光照
    vec3 diffuseIndirect = kD * indirectLight * albedo;
    vec3 specularIndirect = kS * indirectLight * ssrIntensity;
    
    // 合并所有光照分量
    vec3 finalColor = diffuseDirect + specularDirect + diffuseIndirect + specularIndirect;
    
    // 注意：我们不在这里裁剪HDR值
    
    return finalColor;
}
}
*/

int getFaceIndex(vec3 direction)
{
    vec3 absDir = abs(direction);
    int faceIndex;
    
    if(absDir.x >= absDir.y && absDir.x >= absDir.z)
    {
        faceIndex = direction.x > 0.0 ? 0 : 1;
    }
    else if(absDir.y >= absDir.x && absDir.y >= absDir.z)
    {
        faceIndex = direction.y > 0.0 ? 2 : 3;
    }
    else
    {
        faceIndex = direction.z > 0.0 ? 4 : 5;
    }
    
    return faceIndex;
}