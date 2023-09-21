#version 330 core
layout (location = 0) in vec3 Location;
layout (location = 5) in vec2 TexCoord;
layout (location = 6) in mat4 ModelTransform;

out vec2 OutTexCoord;

void main()
{
    OutTexCoord = TexCoord;
    gl_Position = ModelTransform * vec4(Location, 1.0);
}