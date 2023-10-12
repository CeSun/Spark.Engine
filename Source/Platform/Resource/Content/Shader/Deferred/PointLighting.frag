#version 300 es

precision highp float;
out vec4 glColor;


in vec2 OutTexCoord;
in vec2 OutTrueTexCoord;

uniform sampler2D GBuffer;
uniform sampler2D DepthTexture;
uniform samplerCube ShadowMapTextue;
uniform sampler2D SSAOTexture;
uniform mat4 VPInvert;
uniform vec3 LightColor;
uniform vec3 LightLocation;
uniform vec3 CameraLocation;
uniform float AmbientStrength;
uniform float Constant;
uniform float Linear;
uniform float Quadratic;
uniform float FarPlan;
uniform float LightStrength;




vec3 GetWorldLocation(vec3 ScreenLocation);
float ShadowCalculation(vec3 fragPos);
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
    float specularStrength = 0.5;
   
    float depth = texture(DepthTexture, OutTexCoord).r;
    vec3 WorldLocation = GetWorldLocation(vec3(OutTrueTexCoord, depth));
    //vec3 WorldLocation =texture(DepthTexture, OutTexCoord).xyz;
    
    float v[9] = GetBufferValue(ivec2(gl_FragCoord.xy));
    vec4 Color = vec4(v[0], v[1],v[2], 1.0f);//vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
    vec3 Normal = vec3(v[3], v[4],v[5]);

    float AO = texture(SSAOTexture, OutTexCoord).r;

    float Distance    = length(LightLocation - WorldLocation);
    float Attenuation = 1.0 / (Constant + Linear * Distance + Quadratic * (Distance * Distance));


    Normal = normalize(Normal);

    vec3  Ambient = AmbientStrength * AO * Attenuation * LightColor.rgb;
    
    vec3 LightDirection = normalize(WorldLocation - LightLocation);
    // mfs
    float diff = max(dot(Normal, -1.0f * LightDirection), 0.0);
    vec3 diffuse = diff * Attenuation * LightColor;
    // jmfs 
    vec3 CameraDirection = normalize(CameraLocation - WorldLocation);
    vec3 HalfVector = normalize((-LightDirection + CameraDirection));
    // vec3 ReflectDirection = reflect(LightDirection, Normal);
    float spec = pow(max(dot(Normal, HalfVector), 0.0), 16.0f);

    vec3 specular = specularStrength * Attenuation * spec * LightColor;
    
    float shadow = ShadowCalculation(WorldLocation);  
    glColor = vec4((Ambient + (1.0f - shadow) * (diffuse + specular)) * LightStrength * Color.rgb, 1.0f); 

}

float ShadowCalculation(vec3 WorldLocation)
{
    // get vector between fragment position and light position
    vec3 fragToLight = WorldLocation - LightLocation;
    // ise the fragment to light vector to sample from the depth map    
    float closestDepth = texture(ShadowMapTextue, fragToLight).r;
    // it is currently in linear range between [0,1], let's re-transform it back to original depth value
    closestDepth *= FarPlan;
    // now get current linear depth as the length between the fragment and light position
    float currentDepth = length(fragToLight);
    // test for shadows
    float shadow = currentDepth > closestDepth ? 1.0 : 0.0;       

    return shadow;
}

vec3 GetWorldLocation(vec3 ScreenLocation)
{
    ScreenLocation = ScreenLocation * 2.0f - vec3(1.0f, 1.0f, 1.0f);
    // ScreenLocation.z = ScreenLocation.z * -1;
    vec4 tempWorldLocation = VPInvert * vec4(ScreenLocation, 1.0f);
    vec3 WorldLocation =  tempWorldLocation.xyz / tempWorldLocation.w;
    return WorldLocation;
}