#version 300 es
precision highp float;
//{MacroSourceCode}
layout (location = 0) out vec4 Buffer_Color;
//{IncludeSourceCode}

uniform sampler2D Buffer_FinalColor;

in vec2 texCoord;

// ACES色调映射函数
vec3 ACESFilmToneMapping(vec3 color);


void main()
{
	vec4 finalColor = texture(Buffer_FinalColor, texCoord);

	// vec3 ldrColor = finalColor.xyz / (finalColor.xyz + vec3(1.0));
    // vec3 ldrColor = ACESFilmToneMapping(finalColor.xyz);
	Buffer_Color = finalColor;//vec4(pow(finalColor.xyz, vec3(1.0/ 2.2)), finalColor.w);
}