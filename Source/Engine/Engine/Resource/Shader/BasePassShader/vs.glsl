#version 300 es
precision highp float;
//{MacroSourceCode}
layout (location = 0) in vec3 Position;
layout (location = 1) in vec3 Normal;
layout (location = 2) in vec3 Tangent;
layout (location = 3) in vec3 BitTangent;
layout (location = 4) in vec2 TexCoord;
#ifdef _SKELETAL_MESH_
layout (location = 5) in vec4 BoneIds;
layout (location = 6) in vec4 BoneWeights;
#endif
//{IncludeSourceCode}

uniform mat4 model, view, projection;

#ifdef _SKELETAL_MESH_
uniform mat4 animTransform[100];
#endif

out vec2 texcoord;
#ifndef _DEPTH_ONLY_
out mat3 TBNTransform;
#endif
out vec3 outPos;
void main()
{
#ifndef _DEPTH_ONLY_
    mat3 normalMatrix = transpose(inverse(mat3(model)));
    vec3 T = normalize(mat3(normalMatrix) * Tangent);
    vec3 B = normalize(mat3(normalMatrix) * BitTangent);
    vec3 N = normalize(mat3(normalMatrix) * Normal);
    TBNTransform = mat3(T, B, N);
#endif

#ifdef _SKELETAL_MESH_
mat4 AnimMatrix = animTransform[int(BoneIds[0])] * BoneWeights[0];
    AnimMatrix += animTransform[int(BoneIds[1])] * BoneWeights[1];
    AnimMatrix += animTransform[int(BoneIds[2])] * BoneWeights[2];
    AnimMatrix += animTransform[int(BoneIds[3])] * BoneWeights[3];
#endif


texcoord = TexCoord;
// calc final position

	gl_Position = projection * view * model *
#ifdef _SKELETAL_MESH_
    AnimMatrix *
#endif
    vec4(Position, 1.0);

    outPos = (model *
#ifdef _SKELETAL_MESH_
    AnimMatrix *
#endif
    vec4(Position, 1.0)).xyz;
}