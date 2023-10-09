#version 300 es

precision highp float;
layout (location = 0) out vec3 BufferNormal;
layout (location = 1) out vec3 BufferColor;
layout (location = 2) out vec4 BufferCustom;

in vec2 OutTexCoord;
in vec3 OutColor;
in mat3 TBNMat;
in vec3 TbnCameraLocation;
in vec3 OutNormal;
in vec3 OutPosition;
in vec3 TbnPosition;

uniform sampler2D Diffuse;
uniform sampler2D Normal;
uniform sampler2D Parallax;

vec2 GetUVOffset(vec2 TexCoord, vec3 ViewDirection);

void main()
{
    vec3 ViewDirection =  normalize(TbnCameraLocation - TbnPosition);
    vec2 NewTexCoord = GetUVOffset(OutTexCoord, ViewDirection);

    if (NewTexCoord.x > 1.0f || NewTexCoord.x < 0.0f || NewTexCoord.y > 1.0f || NewTexCoord.y < 0.0f)
        discard;
    vec3 TextureNormal = texture(Normal, NewTexCoord).rgb;
    vec4 color = texture(Diffuse, NewTexCoord).rgba;
    if (color.a < 0.1f)
        discard;
    if (TextureNormal == vec3(0, 0, 0))
        TextureNormal = vec3(0.5, 0.5, 1); 
	TextureNormal = normalize(TextureNormal * 2.0 - 1.0); 
    
    if (gl_FrontFacing == false)
        TextureNormal = vec3(1, 1, -1) * TextureNormal;

	TextureNormal = normalize(TBNMat * TextureNormal);
    
    BufferCustom = vec4(0.0, 0.0, 0.0, 0.0);
    BufferColor = texture(Diffuse, NewTexCoord).rgb;
    BufferNormal =  (TextureNormal + 1.0f) / 2.0f;
}

vec2 GetUVOffset(vec2 TexCoord, vec3 ViewDirection)
{
    // number of depth layers
    const float numLayers = 10.0f;
    // calculate the size of each layer
    float layerDepth = 1.0 / numLayers;
    // depth of current layer
    float currentLayerDepth = 0.0;
    // the amount to shift the texture coordinates per layer (from vector P)
    vec2 P = ViewDirection.xy * 0.1; 
    vec2 deltaTexCoords = P / numLayers;
    vec2  currentTexCoords     = TexCoord;
    float currentDepthMapValue = texture(Parallax, currentTexCoords).r;

    while(currentLayerDepth < currentDepthMapValue)
    {
        // shift texture coordinates along direction of P
        currentTexCoords -= deltaTexCoords;
        // get depthmap value at current texture coordinates
        currentDepthMapValue = texture(Parallax, currentTexCoords).r;  
        // get depth of next layer
        currentLayerDepth += layerDepth;  
    }

    return currentTexCoords;

}