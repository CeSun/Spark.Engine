#version 330 core
layout (location = 0) out vec3 BufferNormal;
layout (location = 1) out vec3 BufferColor;
layout (location = 2) out vec4 BufferDepth;

in vec2 OutTexCoord;
in vec3 OutColor;
in vec3 OutNormal;
in vec3 OutPosition;
in mat3 TBNMat;

uniform sampler2D Diffuse;
uniform sampler2D Normal;

void main()
{

    vec3 TextureNormal = texture(Normal, OutTexCoord).rgb;
    
    if (TextureNormal == vec3(0, 0, 0))
        TextureNormal = vec3(0.5, 0.5, 1); 
	TextureNormal = normalize(TextureNormal * 2.0 - 1.0);  
	TextureNormal = normalize(TBNMat * TextureNormal);
    
    
    BufferColor = texture(Diffuse, OutTexCoord).rgb;
    BufferNormal =  (TextureNormal + 1.0f) / 2.0f;
    BufferDepth = vec4(gl_FragCoord.z, 0, 0, 0);
}