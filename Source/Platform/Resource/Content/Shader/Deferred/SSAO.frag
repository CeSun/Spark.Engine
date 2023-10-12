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
vec3 OctahedronToUnitVector( vec2 normal2d )
{
    float x = 2.0f * normal2d.x / (1.0f + normal2d.x * normal2d.x + normal2d.y * normal2d.y);
    float y = 2.0f * normal2d.y / (1.0f + normal2d.x * normal2d.x + normal2d.y * normal2d.y);
    float z = (-1.0f  + normal2d.x * normal2d.x + normal2d.y * normal2d.y) / (1.0f + normal2d.x * normal2d.x + normal2d.y * normal2d.y);
    return vec3(x, y, z);
}

float[9] GetBufferValue(ivec2 ScreenLocation)
{
    float[9] rtl;
    ivec2 screenSize = ivec2(vec2(ScreenLocation) / OutTrueTexCoord);
    vec2 scale = OutTexCoord / OutTrueTexCoord;
    float grayscale = texture(GBuffer, OutTexCoord).x;

    vec2 pixelOffset = scale / vec2(screenSize);


    ivec2 start = ScreenLocation - ivec2(1, 1);

    vec2 rb= vec2(0.0f, 0.0f);
    vec3 normal = vec3(0.0f, 0.0f, 0.0f);
    vec2 mr= vec2(0.0f, 0.0f);

    vec2 counter = vec2(0.0f, 0.0f);
    
    for(int i = 0; i < 3; i ++)
    {
        for(int j = 0; j < 3; j ++)
        {
            ivec2 current = start + ivec2(i, j);
            int parity = (current.x % 2) + (current.y % 2);
            vec2 tempTexCoord = OutTexCoord + vec2(float(i - 1), float(j - 1)) * pixelOffset;
            vec4 data = texture(GBuffer, tempTexCoord);
            float tempGrayscale = data.x;

            if (abs(tempGrayscale - grayscale) > 0.1)
                continue;
            if (parity != 1)
            {
                counter.x = counter.x + 1.0f;
                rb.x += data.y;
                mr += data.za;
            }
            else 
            {
                counter.y = counter.y + 1.0f;
                rb.y += data.y;
                normal += OctahedronToUnitVector((data.za) * 2.0f -1.0f);
            }

        }
    }
    rb.x = rb.x / counter.x;
    rb.y = rb.y / counter.y;
    mr = mr / counter.y;
    normal = normal / counter.y;
    
    rtl[0] = rb.x;
    rtl[1] = (grayscale - (rb.x * 0.3f + rb.y * 0.11f)) / 0.59f;
    rtl[2] = rb.y;

    rtl[3] = normal.x;
    rtl[4] = normal.y;
    rtl[5] = normal.z;

    
    rtl[6] = mr.x;
    rtl[7] = mr.y;

    return rtl;
}
void main()
{
	float Depth = texture(DepthTexture, OutTexCoord).x;
	if (Depth >= 1.0f)
		discard;
        
    float v[9] = GetBufferValue(ivec2(gl_FragCoord.xy));
    vec3 Normal = vec3(v[3], v[4],v[5]);


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