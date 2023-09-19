#version 330 core
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D DepthTexture;
uniform sampler2D NormalTexture;
uniform sampler2D NoiseTexture;
uniform mat4 VPInvert;
uniform mat4 ViewRotationTransform;
uniform mat4 ViewTransform;
uniform mat4 ProjectionTransform;
uniform vec3 samples[64];
int samplesLen = 64;

vec3 GetWorldLocation(vec3 ScreenLocation);
void main()
{
    float depth = texture(DepthTexture, OutTexCoord).r;
    vec3 WorldLocation = GetWorldLocation(vec3(OutTrueTexCoord, depth));
    vec3 Normal = (texture(NormalTexture, OutTexCoord).rgb * 2.0f) - 1.0f;
    Normal = (ViewRotationTransform * vec4(Normal, 1.0f)).xyz;

    vec3 ViewLocation = (ViewTransform * vec4(WorldLocation, 1.0f)).xyz;
    vec2 size = textureSize(NoiseTexture, 0);
	vec2 uv = vec2(0.0f, 0.0f);
	uv.x = int(gl_FragCoord.x) % int(size.x);
	uv.y = int(gl_FragCoord.y) % int(size.y);
	uv.x = uv.x / size.x;
	uv.y = uv.y / size.y;

    vec3 RandomVec = texture(NoiseTexture, uv).xyz;
    RandomVec = normalize(RandomVec);
    vec3 Tangent = normalize(RandomVec -  Normal * dot(RandomVec, Normal));
    vec3 Bitangent = cross(Normal, Tangent);

    mat3 TBN = mat3(Tangent, Bitangent, Normal);
    float occlusion = 0.0;
    for (int i = 0; i < 64; i++)
	{
		vec3 sample =  TBN * samples[i];
        sample = ViewLocation + sample;

        vec4 NDC = ProjectionTransform * vec4(sample, 1.0f);
        if (NDC.x > NDC.w || NDC.x < -NDC.w)
            continue;
        if (NDC.y > NDC.w || NDC.y < -NDC.w)
            continue;
        if (NDC.z > NDC.w || NDC.z < -NDC.w)
            continue;
        NDC = NDC / NDC.w;
        float sampleDepth = (NDC.z + 1.0f ) / 2;

        uv = (NDC.xy + vec2(1.0f, 1.0f)) /2.0f;
        float targetDepth = texture(DepthTexture, uv).r;

        if (sampleDepth > depth)
            occlusion +=1.0f;
	}
    occlusion = 1.0 - (occlusion / samplesLen);

	glColor = vec4(occlusion, 0.0f, 0.0f, 1.0f); //vec4(uv, 0.0f, 1.0f);
}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;

    return WorldLocation;
}