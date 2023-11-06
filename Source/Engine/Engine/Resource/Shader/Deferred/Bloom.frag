#version 300 es

precision highp float;
out vec3 glColor;

in vec2 OutTexCoord;
uniform sampler2D ColorTexture;

uniform bool horizontal;
void main()
{             
    float weight[5] = float[] (0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);
    vec2 tex_offset = 1.0f / vec2(textureSize(ColorTexture, 0)); // gets size of single texel
    vec3 result = texture(ColorTexture, OutTexCoord).rgb * weight[0]; // current fragment's contribution
    if(horizontal)
    {
        for(int i = 1; i < 5; ++i)
        {
            result += texture(ColorTexture, OutTexCoord + vec2(tex_offset.x * float(i), 0.0)).rgb * weight[i];
            result += texture(ColorTexture, OutTexCoord - vec2(tex_offset.x * float(i), 0.0)).rgb * weight[i];
        }
    }
    else
    {
        for(int i = 1; i < 5; ++i)
        {
            result += texture(ColorTexture, OutTexCoord + vec2(0.0, tex_offset.y * float(i))).rgb * weight[i];
            result += texture(ColorTexture, OutTexCoord - vec2(0.0, tex_offset.y * float(i))).rgb * weight[i];
        }
    }
    glColor = result;
}