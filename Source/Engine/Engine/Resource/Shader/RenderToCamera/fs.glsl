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
    vec3 ldrColor = ACESFilmToneMapping(finalColor.xyz);
	Buffer_Color = vec4(ldrColor.xyz, finalColor.w);
}