#version 330 core
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;
uniform sampler2D DepthTexture;
uniform sampler2D NormalTexture;
uniform sampler2D NoiseTexture;
uniform mat4 VPInvert;
uniform mat4 ViewRotationTransform;
uniform vec3 samples[64];
int samplesLen = 64;

vec3 GetWorldLocation(vec3 ScreenLocation);
void main()
{
    float depth = texture(DepthTexture, OutTexCoord).r;
    vec3 WorldLocation = GetWorldLocation(vec3(OutTrueTexCoord, depth));
    vec3 Normal = (texture(NormalTexture, OutTexCoord).rgb * 2.0f) - 1.0f;
    Normal = (ViewRotationTransform * vec4(Normal, 1.0f)).xyz;


    vec2 size = textureSize(NoiseTexture, 0);
	vec2 uv = vec2(0.0f, 0.0f);
	uv.x = int(gl_FragCoord.x) % int(size.x);
	uv.y = int(gl_FragCoord.y) % int(size.y);
	uv.x = uv.x / size.x;
	uv.y = uv.y / size.y;


    for (int i = 0; i < 64; i++)
	{
		Normal += samples[i];
	}


	glColor = vec4(uv, 0.0f, 1.0f);
}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;

    return WorldLocation;
}
