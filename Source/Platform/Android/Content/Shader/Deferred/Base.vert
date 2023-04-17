#version 300 es
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
uniform vec3 CameraLocation;

out vec2 OutTexCoord;
out vec3 OutColor;
out vec3 OutNormal;
out vec3 OutPosition;
out mat3 TBNMat;
out vec3 TbnCameraLocation;
out vec3 TbnPosition;

void main()
{

    vec3 T = normalize(mat3(NormalTransform) * Tangent);
    vec3 B = normalize(mat3(NormalTransform) * BitTangent);
    OutNormal = normalize(mat3(NormalTransform) * Normal);
    TBNMat = mat3(T, B, OutNormal);

    mat3 TBNInvert = transpose(TBNMat);

    OutTexCoord = TexCoord;
    OutColor =  Color;
    // OutNormal = mat3(transpose(inverse(ModelTransform))) * Normal;

    OutPosition = (ModelTransform * vec4(Location, 1.0)).xyz;

    TbnPosition = TBNInvert * OutPosition;
    TbnCameraLocation = TBNInvert * CameraLocation;
    gl_Position = ProjectionTransform * ViewTransform * ModelTransform * vec4(Location, 1.0);
    
}