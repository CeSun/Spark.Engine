#version 300 es
precision highp float;
//{MacroSourceCode}
layout (location = 0) out vec4 Buffer_Color;
//{IncludeSourceCode}

uniform sampler2D Buffer_FinalColor;

in vec2 texCoord;

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


void main()
{
	vec4 finalColor = texture(Buffer_FinalColor, texCoord);

	// vec3 ldrColor = finalColor.xyz / (finalColor.xyz + vec3(1.0));
    vec3 ldrColor = ACESFilmToneMapping(finalColor.xyz);
	Buffer_Color = vec4(pow(ldrColor.xyz, vec3(1.0/ 2.2)), finalColor.w);
}