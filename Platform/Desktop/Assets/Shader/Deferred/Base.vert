#version 330 core
layout (location = 0) in vec3 Location;
layout (location = 1) in vec3 Normal;
layout (location = 2) in vec3 Color;
layout (location = 3) in vec2 TexCoord;

uniform mat4 ModelTransform;
uniform mat4 ViewTransform;
uniform mat4 ProjectionTransform;
uniform mat4 NormalTransform;

out vec2 OutTexCoord;
out vec3 OutColor;
out vec3 OutNormal;
out vec3 OutPosition;

void main()
{
    OutTexCoord = TexCoord;
    OutColor =  Color;
    OutNormal = normalize(mat3(NormalTransform) * Normal);
    // OutNormal = mat3(transpose(inverse(ModelTransform))) * Normal;

    OutPosition = Location.xyz;
    gl_Position = ProjectionTransform * ViewTransform * ModelTransform * vec4(Location, 1.0);
    
}