#version 330 core
layout (location = 0) in vec3 Location;
layout (location = 1) in vec3 Normal;
layout (location = 2) in vec3 Color;
layout (location = 3) in vec2 TexCoord;

uniform mat4 ModelTransform;
uniform mat4 ViewTransform;
uniform mat4 ProjectionTransform;

out vec2 OutTexCoord;
out vec3 OutColor;

void main()
{
    OutTexCoord = TexCoord;
    OutColor =  Color;
    gl_Position = ProjectionTransform * ViewTransform * ModelTransform * vec4(Location, 1.0); // 注意我们如何把一个vec3作为vec4的构造器的参数
}