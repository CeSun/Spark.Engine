#version 300 es

precision highp float;
out float FragColor;

uniform vec2 TexCoordScale;

in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D DepthTexture;
uniform sampler2D GBuffer;
uniform sampler2D NoiseTexture;
uniform mat4 ProjectionTransform;
uniform mat4 InvertProjectionTransform;
uniform vec3 samples[64];


int samplesLen = 64;
float radius = 0.5;
float bias = 0.025;

vec3 GetViewLocation(vec3 ScreenLocation);

vec3 GetNormal(ivec2 ScreenLocation)
{
    ivec2 screenSize = ivec2(vec2(ScreenLocation) / OutTrueTexCoord);
    vec2 scale = OutTexCoord / OutTrueTexCoord;
    float grayscale = texture(GBuffer, OutTexCoord).x;

    vec2 pixelOffset = scale / vec2(screenSize);

    
    vec2 XTexcoord = OutTexCoord;
    vec2 YTexcoord = OutTexCoord;
    vec2 ZTexcoord = OutTexCoord;

    int xparity = (ScreenLocation.x % 2);
    int yparity = (ScreenLocation.y % 2);
    if (xparity + yparity == 0)
    {
        XTexcoord = OutTexCoord;
        YTexcoord = OutTexCoord + pixelOffset;
        ZTexcoord = vec2(OutTexCoord.x + pixelOffset.x, OutTexCoord.y);
    }
    else if (xparity + yparity == 2)
    {
        XTexcoord = OutTexCoord - pixelOffset;
        YTexcoord = OutTexCoord;
        ZTexcoord = vec2(OutTexCoord.x, OutTexCoord.y - pixelOffset.y);

    }
    else if (xparity == 1 && yparity == 0)
    {
        XTexcoord = vec2(OutTexCoord.x - pixelOffset.x, OutTexCoord.y);
        YTexcoord = vec2(OutTexCoord.x, OutTexCoord.y + pixelOffset.y);
        ZTexcoord = OutTexCoord;
    }
    else if (xparity == 0 && yparity == 1)
    {
        XTexcoord = vec2(OutTexCoord.x, OutTexCoord.y - pixelOffset.y);
        YTexcoord = vec2(OutTexCoord.x + pixelOffset.x, OutTexCoord.y);
        ZTexcoord = vec2(OutTexCoord.x + pixelOffset.x, OutTexCoord.y - pixelOffset.y);
    }
    
    float x = texture(GBuffer, XTexcoord).z;
    float y = texture(GBuffer, YTexcoord).z;
    float z = texture(GBuffer, ZTexcoord).z;
    return vec3(x, y, z);
}

void main()
{
	float Depth = texture(DepthTexture, OutTexCoord).x;
	if (Depth >= 1.0f)
		discard;

    vec3 Normal = normalize((GetNormal(ivec2(gl_FragCoord.xy)) * 2.0f) - 1.0f);


	vec3 FragViewLocation = GetViewLocation(vec3(OutTrueTexCoord, Depth));


	vec2 size = vec2(textureSize(NoiseTexture, 0));
	vec2 uv = vec2(gl_FragCoord.x / size.x, gl_FragCoord.y / size.y);

	vec3 RandomVec = normalize(texture(NoiseTexture, uv * TexCoordScale)).xyz;
	vec3 Tangent = normalize(RandomVec - Normal * dot(RandomVec, Normal));
	vec3 BitTanget = cross(Normal, Tangent);

	mat3 TBN = mat3(Tangent, BitTanget, Normal);

	float occlusion = 0.0;
	for(int i = 0; i < samplesLen; ++i)
	{
		// get sample position
        vec3 samplePos = TBN * samples[i]; // from tangent to view-space
        samplePos = FragViewLocation + samplePos * radius; 
        
        // project sample position (to sample texture) (to get position on screen/texture)
        vec4 offset = vec4(samplePos, 1.0);
        offset = ProjectionTransform * offset; // from view to clip-space
        offset.xyz /= offset.w; // perspective divide
        offset.xyz = offset.xyz * 0.5 + 0.5; // transform to range 0.0 - 1.0
        
		float sampleDepth =  texture(DepthTexture, offset.xy).x;
        // get sample depth
        sampleDepth = GetViewLocation(vec3(offset.xy, sampleDepth)).z; // get depth value of kernel sample
        
        // range check & accumulate
        float rangeCheck = smoothstep(0.0, 1.0, radius / abs(FragViewLocation.z - sampleDepth));
        occlusion += (sampleDepth >= samplePos.z + bias ? 1.0 : 0.0) * rangeCheck;     
	}
    occlusion = 1.0f - (occlusion / float(samplesLen));
    
    FragColor = occlusion;


}


vec3 GetViewLocation(vec3 ScreenLocation)
{
	ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);

	vec4 tmpViewLocation =  InvertProjectionTransform * vec4(ScreenLocation, 1.0f);

	return tmpViewLocation.xyz / tmpViewLocation.w;
	
}