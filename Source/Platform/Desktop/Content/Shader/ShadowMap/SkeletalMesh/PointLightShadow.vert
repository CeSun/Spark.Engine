#version 300 es

precision highp float;
layout (location = 0) in vec3 position;
layout (location = 6) in vec4 BoneIds;
layout (location = 7) in vec4 BoneWeights;

uniform mat4 ModelTransform;
uniform mat4 AnimTransform[100];

void main()
{
    mat4 AnimMatrix = AnimTransform[int(BoneIds[0])] * BoneWeights[0];
    AnimMatrix += AnimTransform[int(BoneIds[1])] * BoneWeights[1];
    AnimMatrix += AnimTransform[int(BoneIds[2])] * BoneWeights[2];
    AnimMatrix += AnimTransform[int(BoneIds[3])] * BoneWeights[3];
    gl_Position = ModelTransform * AnimMatrix * vec4(position, 1.0);
}