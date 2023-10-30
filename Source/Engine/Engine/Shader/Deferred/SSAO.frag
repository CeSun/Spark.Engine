#version 300 es

precision highp float;
out float FragColor;

uniform vec2 TexCoordScale;

in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D DepthTexture;
uniform sampler2D CustomBuffer;
uniform sampler2D NoiseTexture;
uniform mat4 ProjectionTransform;
uniform mat4 InvertProjectionTransform;
uniform vec3 samples[64];


int samplesLen = 64;
float radius = 0.5;
float bias = 0.025;

vec3 GetViewLocation(vec3 ScreenLocation);

vec3 Normal2DTo3D(vec2 Oct)
{
	vec3 N = vec3( Oct, 1.0 - dot( vec2(1.0f), abs(Oct) ) );
    if( N.z < 0.0f )
    {
		vec2 add;
		if (N.x >= 0.0f)
			add.x = 1.0f;
		else
			add.x = -1.0f;

		if (N.y >= 0.0f)
			add.y = 1.0f;
		else
			add.y = -1.0f;
		N.xy = ( 1.0f - abs(N.yx) ) * add;
    }
    return normalize(N);
}



void main()
{
	float Depth = texture(DepthTexture, OutTexCoord).x;
	if (Depth >= 1.0f)
		discard;


	vec3 Normal = Normal2DTo3D(texture(CustomBuffer, OutTexCoord).xy * 2.0 - 1.0);//normalize(texture(CustomBuffer, OutTexCoord).xyz * 2.0f - vec3(1.0f, 1.0f, 1.0f));

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