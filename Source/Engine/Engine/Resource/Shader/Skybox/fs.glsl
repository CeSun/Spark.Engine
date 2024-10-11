#version 300 es
precision highp float;
//{MacroSourceCode}
layout (location = 0) out vec4 Buffer_Color;
//{IncludeSourceCode}

in vec3 TexCoords;

uniform samplerCube TextureCube_Skybox;


void main()
{   
    vec3 Color = texture(TextureCube_Skybox, TexCoords).rgb;
    Buffer_Color = vec4(Color, 1.0f);
}