#version 300 es

precision highp float;
layout (location = 0) out vec4 GBuffer1;
layout (location = 1) out vec4 GBuffer2;
in vec2 OutTexCoord;
in vec3 OutColor;
in mat3 TBNMat;
in vec3 TbnCameraLocation;
in vec3 OutNormal;
in vec3 OutPosition;
in vec3 TbnPosition;

uniform sampler2D BaseColorTexture;
uniform sampler2D NormalTexture;
uniform sampler2D CustomTexture; // Metallic Roughness AO Parallax

vec2 Normal3Dto2D(vec3 Normal);

vec2 GetUVOffset(vec2 TexCoord, vec3 ViewDirection);

void main()
{
    vec3 ViewDirection =  normalize(TbnCameraLocation - TbnPosition);
    vec2 NewTexCoord = GetUVOffset(OutTexCoord, ViewDirection);

    if (NewTexCoord.x > 1.0f || NewTexCoord.x < 0.0f || NewTexCoord.y > 1.0f || NewTexCoord.y < 0.0f)
        discard;
    vec3 TextureNormal = texture(NormalTexture, NewTexCoord).xyz;
    vec4 color = texture(BaseColorTexture, NewTexCoord);
    vec3 custom = texture(CustomTexture, NewTexCoord).xyz;

    if (color.a < 0.1f)
        discard;
    if (TextureNormal == vec3(0, 0, 0))
        TextureNormal = vec3(0.5, 0.5, 1); 
	TextureNormal = normalize(TextureNormal * 2.0 - 1.0); 
    
    if (gl_FrontFacing == false)
        TextureNormal = vec3(1, 1, -1) * TextureNormal;

	vec3 Normal = normalize(TBNMat * TextureNormal);
    
    GBuffer1 = vec4(color.rgb,  custom.z);

    GBuffer2 =  vec4(Normal3Dto2D(Normal), custom.xy);
}


vec2 Normal3Dto2D(vec3 Normal)
{   
    vec2 res = vec2(Normal.x, Normal.y);

    res /= dot(vec3(1.0f, 1.0f, 1.0f), abs(Normal));

    if (Normal.z < 0.0f )
    {
        vec2 tmp = vec2(1.0f, 1.0f);
        if (res.x < 0.0f  || res.y < 0.0f )
        {
            tmp = vec2(-1.0f, -1.0f);
        }
        res = (vec2(1.0f, 1.0f) - abs(vec2(res.y, res.x))) * tmp;
    }
    return res;
}

vec2 GetUVOffset(vec2 TexCoord, vec3 ViewDirection)
{
    // number of depth layers
    const float numLayers = 10.0f;
    // calculate the size of each layer
    float layerDepth = 1.0f / numLayers;
    // depth of current layer
    float currentLayerDepth = 0.0;
    // the amount to shift the texture coordinates per layer (from vector P)
    vec2 P = ViewDirection.xy * 0.1; 
    vec2 deltaTexCoords = P / numLayers;
    vec2  currentTexCoords     = TexCoord;
    float currentDepthMapValue = texture(CustomTexture, currentTexCoords).w;

    while(currentLayerDepth < currentDepthMapValue)
    {
        // shift texture coordinates along direction of P
        currentTexCoords -= deltaTexCoords;
        // get depthmap value at current texture coordinates
        currentDepthMapValue = texture(CustomTexture, currentTexCoords).w;  
        // get depth of next layer
        currentLayerDepth += layerDepth;  
    }

    return currentTexCoords;

}