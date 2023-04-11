#version 300 es
out vec3 glColor;

in vec2 OutTexCoord;
uniform sampler2D ColorTexture;

uniform bool horizontal;
float weight[5] = float[] (0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);
void main()
{             
    ivec2 Size = textureSize(ColorTexture, 0);
    vec2 tex_offset = 1.0 / vec2(float(Size.x), float(Size.y));


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