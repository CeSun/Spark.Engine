#version 300 es
precision highp float;
//{MacroSourceCode}
layout (location = 0) out vec4 Buffer_Color;
//{IncludeSourceCode}

uniform sampler2D Buffer_FinalColor;

in vec2 texCoord;

void main()
{
	vec4 finalColor = texture(Buffer_FinalColor, texCoord);

	vec3 ldrColor = finalColor.xyz / (finalColor.xyz + vec3(1.0));

	Buffer_Color = vec4(pow(ldrColor.xyz, vec3(1.0/ 2.2)), finalColor.w);
}