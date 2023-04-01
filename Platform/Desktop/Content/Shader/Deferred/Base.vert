#version 330 core
layout (location = 0) in vec3 Location;
layout (location = 1) in vec3 Normal;
layout (location = 2) in vec3 Tangent;
layout (location = 3) in vec3 BitTangent;
layout (location = 4) in vec3 Color;
layout (location = 5) in vec2 TexCoord;

uniform mat4 ModelTransform;
uniform mat4 ViewTransform;
uniform mat4 ProjectionTransform;
uniform mat4 NormalTransform;

out vec2 OutTexCoord;
out vec3 OutColor;
out vec3 OutNormal;
out vec3 OutPosition;
out mat3 TBNMat;

void main()
{

    vec3 T = normalize(mat3(ModelTransform) * Tangent);
    vec3 B = normalize(mat3(ModelTransform) * BitTangent);
    OutNormal = normalize(mat3(ModelTransform) * Normal);
    TBNMat = mat3(T, B, OutNormal);


    OutTexCoord = TexCoord;
    OutColor =  Color;
    // OutNormal = mat3(transpose(inverse(ModelTransform))) * Normal;

    OutPosition = (ModelTransform * vec4(Location, 1.0)).xyz;
    gl_Position = ProjectionTransform * ViewTransform * ModelTransform * vec4(Location, 1.0);
    
}