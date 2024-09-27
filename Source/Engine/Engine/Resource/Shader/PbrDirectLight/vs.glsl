#version 300 es
precision highp float;
//{MacroSourceCode}
layout (location = 0) in vec3 Position;
layout (location = 1) in vec2 TexCoord;
//{IncludeSourceCode}

out vec2 texCoord;

void main()
{
	texCoord = TexCoord;
	gl_Position = vec4(Position, 1.0f);
}