#version 330 core
layout (location = 0) in vec3 Location;
layout (location = 5) in vec2 TexCoord;
layout (location = 6) in vec4 BoneIds;
layout (location = 7) in vec4 BoneWeights;

uniform mat4 ModelTransform;
uniform mat4 ViewTransform;
uniform mat4 ProjectionTransform;
out vec2 OutTexCoord;
uniform mat4 AnimTransform[100];


void main()
{
    mat4 AnimMatrix = AnimTransform[int(BoneIds[0])] * BoneWeights[0];
    AnimMatrix += AnimTransform[int(BoneIds[1])] * BoneWeights[1];
    AnimMatrix += AnimTransform[int(BoneIds[2])] * BoneWeights[2];
    AnimMatrix += AnimTransform[int(BoneIds[3])] * BoneWeights[3];
    OutTexCoord = TexCoord;
    gl_Position = ProjectionTransform * ViewTransform * ModelTransform * AnimMatrix * vec4(Location, 1.0);
    
}