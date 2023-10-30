#version 300 es

precision highp float;
layout (location = 0) out vec4 GBuffer1;
#ifndef _MICRO_GBUFFER_
layout (location = 1) out vec4 GBuffer2;
#endif


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

vec4 MicroGBufferEncoding(vec3 BaseColor, vec2 Normal, float r, float m, float ao, ivec2 ScreenLocation);


void main()
{
    vec3 ViewDirection =  normalize(TbnCameraLocation - TbnPosition);
#ifdef _MOBILE_
    vec2 NewTexCoord = OutTexCoord;
#else
    vec2 NewTexCoord = GetUVOffset(OutTexCoord, ViewDirection);
#endif
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
    
#ifndef _MICRO_GBUFFER_
    GBuffer1 = vec4(color.rgb,  custom.z);
    GBuffer2 =  vec4(Normal3Dto2D(Normal) * 0.5 + 0.5, custom.xy);
#else
    GBuffer1 = MicroGBufferEncoding(color.rgb, Normal3Dto2D(Normal) * 0.5 + 0.5, custom.x, custom.y, custom.z, ivec2(gl_FragCoord.xy));
#endif
}


vec2 Normal3Dto2D(vec3 Normal)
{   
    Normal.xy /= dot( vec3(1.0f), abs(Normal) );
    if( Normal.z <= 0.0f )
    {
        vec2 add;
        if (Normal.x >= 0.0f)
            add.x = 1.0f;
        else 
            add.x = -1.0f;
        if (Normal.y >= 0.0f)
            add.y = 1.0f;
        else 
            add.y = -1.0f;
        Normal.xy = ( 1.0f - abs(Normal.yx) ) * add ;
    }
    return Normal.xy;
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


vec4 MicroGBufferEncoding(vec3 BaseColor, vec2 Normal, float r, float m, float ao, ivec2 ScreenLocation)
{
	vec4 result = vec4(0.0f, 0.0f, 0.0f, 0.0f);

	float gray = BaseColor.r * 0.3f + BaseColor.g * 0.59f + BaseColor.b * 0.11f;
	result.x = gray;
    int xparity = (ScreenLocation.x % 2);
    int yparity = (ScreenLocation.y % 2);
	if (xparity == 0 && yparity == 0)
	{
		result.y = BaseColor.r;
		result.z = BaseColor.b;
	}

	if (xparity == 1 && yparity == 0)
	{
		result.y = Normal.x;
		result.z = Normal.y;
	}

	if (xparity == 0 && yparity == 1)
	{
		result.y = r;
		result.z = m;
		result.w = ao;
	}

	if (xparity == 1 && yparity == 1)
	{
		result.y = BaseColor.r;
		result.z = BaseColor.b;
	}

	return result;
}