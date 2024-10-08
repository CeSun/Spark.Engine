#version 300 es
precision highp float;
//{MacroSourceCode}
layout (location = 0) out vec4 Buffer_Color;
//{IncludeSourceCode}


uniform sampler2D Buffer_BaseColor_AO;
uniform float IndirectLightIntensity;

in vec2 texCoord;


void main()
{
	vec4 BaseColor_AO = texture(Buffer_BaseColor_AO, texCoord);
	vec3 BaseColor = BaseColor_AO.xyz;
	float AO = BaseColor_AO.w;


	Buffer_Color = vec4(BaseColor * IndirectLightIntensity * AO, 1.0f);
}